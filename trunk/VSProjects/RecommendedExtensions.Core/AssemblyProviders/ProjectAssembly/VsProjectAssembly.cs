﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;

using EnvDTE;
using VSLangProj;
using VSLangProj80;

using MEFEditor.Analyzing;
using MEFEditor.TypeSystem;
using MEFEditor.TypeSystem.Transactions;

using MEFEditor.Interoperability;

using RecommendedExtensions.Core.AssemblyProviders.ProjectAssembly.Traversing;
using RecommendedExtensions.Core.AssemblyProviders.ProjectAssembly.MethodBuilding;

namespace RecommendedExtensions.Core.AssemblyProviders.ProjectAssembly
{
    /// <summary>
    /// Assembly provider implementation for <see cref="Project" /> assemblies loaded from Visual Studio solutions.
    /// </summary>
    public abstract class VsProjectAssembly : AssemblyProvider
    {
        /// <summary>
        /// Represented VsProject assembly.
        /// </summary>
        private readonly VSProject _assemblyProject;

        /// <summary>
        /// Searcher of <see cref="CodeElement" /> objects.
        /// </summary>
        protected readonly CodeElementSearcher Searcher;

        /// <summary>
        /// <see cref="CodeClass" /> set that will be discovered when change transaction is closed.
        /// </summary>
        private readonly HashSet<CodeClass> _toDiscover = new HashSet<CodeClass>();

        /// <summary>
        /// Provider of element names.
        /// </summary>
        private readonly CodeElementNamesProvider _namesProvider;

        /// <summary>
        /// Events for handling references.
        /// </summary>
        private readonly ReferencesEvents _referenceEvents;

        /// <summary>
        /// <see cref="Project" /> represented by current assembly.
        /// </summary>
        /// <value>The project.</value>
        public Project Project { get { return _assemblyProject.Project; } }

        /// <summary>
        /// Services that are available for assembly.
        /// </summary>
        public readonly VisualStudioServices VS;

        /// <summary>
        /// Build method info.
        /// </summary>
        public readonly MethodInfoBuilder InfoBuilder;

        /// <summary>
        /// Build method items.
        /// </summary>
        public readonly MethodItemBuilder ItemBuilder;

        /// <summary>
        /// Root <see cref="CodeElement" /> objects of represented <see cref="Project" />.
        /// </summary>
        /// <value>The root elements.</value>
        public IEnumerable<CodeElement> RootElements
        {
            get
            {
                foreach (var node in VS.GetRootElements(_assemblyProject))
                {
                    yield return node.Element;
                }
            }
        }

        /// <summary>
        /// Initialize new instance of <see cref="VsProjectAssembly" /> from given <see cref="Project" />.
        /// </summary>
        /// <param name="assemblyProject">Project that will be represented by initialized assembly.</param>
        /// <param name="vs">The visual studio services.</param>
        /// <param name="namesProvider">The <see cref="CodeElement"/> names provider.</param>
        /// <param name="infoBuilder">The method signature information builder.</param>
        /// <param name="itemBuilder">The method item builder.</param>
        public VsProjectAssembly(Project assemblyProject, VisualStudioServices vs,
            CodeElementNamesProvider namesProvider, MethodInfoBuilder infoBuilder, MethodItemBuilder itemBuilder)
        {
            VS = vs;
            _assemblyProject = assemblyProject.Object as VSProject;
            _referenceEvents = _assemblyProject.Events.ReferencesEvents;
            _namesProvider = namesProvider;
            Searcher = new CodeElementSearcher(this);

            InfoBuilder = infoBuilder;
            ItemBuilder = itemBuilder;

            InfoBuilder.Initialize(this);
            ItemBuilder.Initialize(this);

            OnTypeSystemInitialized += initializeAssembly;
        }

        #region Services that are customizable for concrete languages

        /// <summary>
        /// Provider of method parsing for assembly.
        /// </summary>
        /// <param name="activation">Activation for assembly parser.</param>
        /// <param name="emitter">Emitter where parsed instructions are emitted.</param>
        public abstract void ParsingProvider(ParsingActivation activation, EmitterBase emitter);

        /// <summary>
        /// Parses the given value to native object.
        /// </summary>
        /// <param name="valueRepresentation">The value representation.</param>
        /// <param name="contextType">Context type where value has been found.</param>
        /// <param name="contextElement">The context element where has been found.</param>
        /// <returns>Parsed object if available, <c>null</c> otherwise.</returns>
        public abstract object ParseValue(string valueRepresentation, TypeDescriptor contextType, CodeElement contextElement);

        /// <summary>
        /// Translate given path according to aliases and language type conventions
        /// <remarks>Namespace lookup is not used</remarks>.
        /// </summary>
        /// <param name="path">Path to be translated.</param>
        /// <returns>Resulting descriptor.</returns>
        public virtual string TranslatePath(string path)
        {
            return path;
        }

        /// <summary>
        /// Determine that given name is possible for given <see cref="CodeElement" />.
        /// </summary>
        /// <param name="possibleName">Tested name.</param>
        /// <param name="element">Element which is tested for possible name.</param>
        /// <returns><c>true</c> if element can have possible name, <c>false</c> otherwise.</returns>
        public bool IsPossibleName(string possibleName, CodeElement element)
        {
            var signature = PathInfo.GetSignature(possibleName);

            var names = generatePossibleNames(element);
            return names.Contains(signature);
        }

        /// <summary>
        /// Get names that are matching to given node and searchedName constraint.
        /// </summary>
        /// <param name="node">Node which names are matched.</param>
        /// <param name="searchedName">Constraint on searched name if <c>null</c> no constraint is specified.</param>
        /// <returns>Matching names.</returns>
        internal IEnumerable<string> GetMatchingNames(CodeElement node, string searchedName)
        {
            var names = generatePossibleNames(node);
            if (searchedName == null)
                //there is no constraint
                return names;

            if (names.Contains(searchedName))
                return new[] { searchedName };
            else
                return new string[0];
        }

        #endregion

        /// <summary>
        /// Get namespaces that are valid for file where given <see cref="CodeElement" /> is defined.
        /// </summary>
        /// <param name="element">Element which namespaces will be returned.</param>
        /// <returns>Valid namespaces for file with given element.</returns>
        public IEnumerable<string> GetNamespaces(CodeElement element)
        {
            var namespaces = VS.GetNamespaces(element.ProjectItem);
            return namespaces;
        }

        /// <summary>
        /// Get namespaces that are valid for given <see cref="TypeDescriptor" />.
        /// </summary>
        /// <param name="type">Type which namespaces will be returned.</param>
        /// <returns>Valid namespaces for given method.</returns>
        public static IEnumerable<string> GetImplicitNamespaces(TypeDescriptor type)
        {
            var implicitNamespaces = new HashSet<string>();

            //add empty namespace
            implicitNamespaces.Add("");

            //each part creates implicit namespace
            var parts = Naming.SplitGenericPath(type.TypeName);

            var buffer = new StringBuilder();
            foreach (var part in parts)
            {
                if (buffer.Length > 0)
                {
                    //add trailing char
                    buffer.Append(Naming.PathDelimiter);
                }

                buffer.Append(part);

                implicitNamespaces.Add(buffer.ToString());
            }

            return implicitNamespaces;
        }

        #region Initialization routines

        /// <summary>
        /// Initialize assembly.
        /// </summary>
        private void initializeAssembly()
        {
            try
            {
                var outputType = VS.GetOutputType(_assemblyProject.Project);
                var extension = outputType == "2" ? ".dll" : ".exe";
                var name = getAssemblyName();
                var outputPath = VS.GetOutputPath(_assemblyProject.Project);

                var assemblyDir = Path.GetDirectoryName(getAssemblyFullPath());
                FullPathMapping = Path.GetFullPath(Path.Combine(assemblyDir, outputPath, name + extension));

                hookChangesHandler();
                initializeReferences();
            }
            catch (Exception ex)
            {
                VS.LogException(ex, "Initilizing assembly {0} failed", Name);
            }
        }

        /// <summary>
        /// Hook handler that will recieve change events in project.
        /// </summary>
        private void hookChangesHandler()
        {
            try
            {
                VS.RegisterElementAdd(_assemblyProject, onAdd);
                VS.RegisterElementRemove(_assemblyProject, onRemove);
                VS.RegisterElementChange(_assemblyProject, onChange);
                _referenceEvents.ReferenceAdded += (r) => addReference(r as Reference3);
                _referenceEvents.ReferenceRemoved += (r) => removeReference(r as Reference3);

                TypeServices.RegisterInvalidationHandler(onNameInvalidation);
            }
            catch (Exception ex)
            {
                VS.LogException(ex, "Hooking changes handlers in assembly {0} failed", Name);
            }
        }

        /// <summary>
        /// Set references according to project referencies.
        /// </summary>
        private void initializeReferences()
        {
            try
            {
                StartTransaction("Collecting references");
                addReferences();
                CommitTransaction();
            }
            catch (Exception ex)
            {
                VS.LogException(ex, "Initializing references for {0} failed", Name);
            }
        }

        /// <summary>
        /// Add references to current assembly.
        /// </summary>
        private void addReferences()
        {
            foreach (Reference3 reference in _assemblyProject.References)
            {
                addReference(reference);
            }
        }

        /// <summary>
        /// Adds reference according to given <see cref="Reference3"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        private void addReference(Reference3 reference)
        {
            var sourceProject = reference.SourceProject;

            if (sourceProject == null)
            {
                //there is not source project for the reference
                //we has to add reference according to path
                AddReference(reference.Path);
            }
            else
            {
                //we can add reference through referenced source project
                AddReference(sourceProject);
            }
        }
        
        /// <summary>
        /// Removes reference according to given <see cref="Reference3"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        private void removeReference(Reference3 reference)
        {
            var sourceProject = reference.SourceProject;

            if (sourceProject == null)
            {
                //there is not source project for the reference
                //we has to add reference according to path
                RemoveReference(reference.Path);
            }
            else
            {
                //we can add reference through referenced source project
                RemoveReference(sourceProject);
            }
        }

        /// <summary>
        /// Create <see cref="ComponentSearcher" /> with initialized event handlers.
        /// </summary>
        /// <returns>Created <see cref="ComponentSearcher" />.</returns>
        private ComponentSearcher createComponentSearcher()
        {
            var searcher = new ComponentSearcher(this, TypeServices);
            searcher.OnNamespaceEntered += reportSearchProgress;
            searcher.OnComponentFound += ComponentDiscovered;
            return searcher;
        }

        /// <summary>
        /// Reports search progress to TypeSystem.
        /// </summary>
        /// <param name="e">Name of currently processed namespace.</param>
        private void reportSearchProgress(CodeNamespace e)
        {
            ReportProgress(e.FullName);
        }

        #endregion

        #region Changes handlers

        /// <summary>
        /// Handler called for every invalidated name.
        /// </summary>
        /// <param name="name">Invalidated name.</param>
        private void onNameInvalidation(string name)
        {
            if (name == null)
                return;

            //get components defined in current assembly
            var components = TypeServices.GetComponents(this).ToArray();

            //check for components that may be invalidated.
            foreach (var component in components)
            {
                foreach (var componentName in component.DependencyNames)
                {
                    if (componentName.StartsWith(name) || name.EndsWith(componentName))
                    {
                        //component is invalidated
                        var componentClass = GetTypeNode(component.ComponentType.TypeName) as CodeClass;
                        if (componentClass != null)
                        {
                            _toDiscover.Add(componentClass);
                            ComponentRemoveDiscovered(component.ComponentType.TypeName);
                        }

                        break;
                    }
                }

            }

            if (_toDiscover.Count > 0)
                requireComponentDiscovering();
        }

        /// <summary>
        /// Handler called for element that has been added.
        /// </summary>
        /// <param name="node">Affected element node.</param>
        private void onAdd(ElementNode node)
        {
            var fullnames = GetFullNames(node.Element).ToArray();
            node.SetTag("Names", fullnames);

            //every component will be reported through add
            //adding member within component is reported as component change
            if (node.Element is CodeClass)
            {
                _toDiscover.Add(node.Element as CodeClass);
                requireComponentDiscovering();
            }

            //there may be indirect dependency
            foreach (var fullname in fullnames)
            {
                //code change within method
                ReportInvalidation(fullname);
            }
        }

        /// <summary>
        /// Handler called for element that has been changed.
        /// </summary>
        /// <param name="node">Affected element node.</param>
        private void onChange(ElementNode node)
        {
            var fn = node.Element as CodeFunction;
            if (fn == null)
            {
                onRemove(node);
                onAdd(node);
            }
            else
            {
                var fullnames = node.GetTag("Names") as IEnumerable<string>;
                if (fullnames == null)
                {
                    fullnames = GetFullNames(node.Element);
                    node.SetTag("Names", fullnames);
                }

                if (node == null)
                    return;

                foreach (var fullname in fullnames)
                {
                    //code change within method
                    ReportInvalidation(fullname);
                }
            }
        }

        /// <summary>
        /// Handler called for element that has been removed.
        /// </summary>
        /// <param name="node">Affected element node.</param>
        private void onRemove(ElementNode node)
        {
            var fullnames = node.GetTag("Names") as IEnumerable<string>;
            if (fullnames == null)
                return;

            foreach (var fullname in fullnames)
            {
                ReportInvalidation(fullname);
                ComponentRemoveDiscovered(fullname);
            }
        }

        /// <summary>
        /// Require discovering action after current transaction is completed.
        /// </summary>
        private void requireComponentDiscovering()
        {
            Transactions.AttachAfterAction(Transactions.CurrentTransaction, new TransactionAction(flushDiscovering, "FlushDiscovering", (t) => t.Name == "FlushDiscovering", this));
        }

        /// <summary>
        /// Flush discovering of elements collected through changes handlers.
        /// </summary>
        private void flushDiscovering()
        {
            try
            {
                foreach (var element in _toDiscover)
                {
                    VS.Log.Message("Discovering components in {0}", element.FullName);
                    var searcher = createComponentSearcher();
                    searcher.VisitElement(element as CodeElement);
                }
            }
            catch (Exception ex)
            {
                VS.LogException(ex, "Discovering components in {0} failed", Name);
            }
            finally
            {
                _toDiscover.Clear();
            }
        }

        #endregion

        #region Assembly provider implementation

        /// <summary>
        /// Force to load components - suppose that no other components from this assembly are registered.
        /// <remarks>Can be called multiple times when changes in references are registered</remarks>.
        /// </summary>
        /// <inheritdoc />
        protected override void loadComponents()
        {
            var searcher = createComponentSearcher();

            //search components in whole project
            searcher.VisitProject(Project);
        }

        /// <summary>
        /// Gets the name of the assembly.
        /// </summary>
        /// <returns>Assembly name.</returns>
        /// <inheritdoc />
        protected override string getAssemblyName()
        {
            return _assemblyProject.Project.Name;
        }

        /// <summary>
        /// Gets the assembly full path.
        /// </summary>
        /// <returns>Assembly full path.</returns>
        /// <inheritdoc />
        protected override string getAssemblyFullPath()
        {
            return _assemblyProject.Project.FullName;
        }

        /// <summary>
        /// Gets the method generator.
        /// </summary>
        /// <param name="methodID">The method identifier.</param>
        /// <returns>GeneratorBase.</returns>
        /// <inheritdoc />
        public override GeneratorBase GetMethodGenerator(MethodID methodID)
        {
            var item = getMethodItem(methodID);

            if (item == null)
                return null;

            return item.Generator;
        }

        /// <summary>
        /// Gets the generic method generator.
        /// </summary>
        /// <param name="methodID">The method identifier.</param>
        /// <param name="searchPath">The search path.</param>
        /// <returns>GeneratorBase.</returns>
        /// <inheritdoc />
        public override GeneratorBase GetGenericMethodGenerator(MethodID methodID, PathInfo searchPath)
        {
            var item = getMethodItemFromGeneric(methodID, searchPath);
            if (item == null)
                return null;

            return item.Generator;
        }

        /// <summary>
        /// Creates the root iterator. That is used for
        /// searching method definitions.
        /// </summary>
        /// <returns>SearchIterator.</returns>
        /// <inheritdoc />
        public override SearchIterator CreateRootIterator()
        {
            return new CodeElementIterator(this);
        }

        /// <summary>
        /// Gets the implementation.
        /// </summary>
        /// <param name="methodID">The method identifier.</param>
        /// <param name="dynamicInfo">The dynamic information.</param>
        /// <param name="alternativeImplementer">The alternative implementer.</param>
        /// <returns>MethodID.</returns>
        /// <inheritdoc />
        public override MethodID GetImplementation(MethodID methodID, TypeDescriptor dynamicInfo, out TypeDescriptor alternativeImplementer)
        {
            alternativeImplementer = null;

            //get method implemented on type described by dynamicInfo
            var path = new PathInfo(dynamicInfo.TypeName);
            var node = GetTypeNode(path.Signature);
            if (node == null)
                //type is not defined here
                return null;

            //only base type (not interfaces) could implement the method
            var implementingMethod = Naming.ChangeDeclaringType(dynamicInfo.TypeName, methodID, false);
            var item = getMethodItem(implementingMethod);
            if (item == null)
            {
                var baseType = node.Bases.Item(1) as CodeType;
                alternativeImplementer = InfoBuilder.CreateDescriptor(baseType);
                return null;
            }

            return item.Info.MethodID;
        }

        /// <summary>
        /// Gets identifier of implementing method for given abstract method.
        /// </summary>
        /// <param name="methodID">The abstract method identifier.</param>
        /// <param name="methodSearchPath">The method search path.</param>
        /// <param name="implementingTypePath">The implementing type path.</param>
        /// <param name="alternativeImplementer">The alternative implementer which can define requested method.</param>
        /// <returns>Identifier of implementing method.</returns>
        /// <inheritdoc />
        public override MethodID GetGenericImplementation(MethodID methodID, PathInfo methodSearchPath, PathInfo implementingTypePath, out PathInfo alternativeImplementer)
        {
            alternativeImplementer = null;
            //get method implemented on type described by dynamicInfo
            var implementingMethod = Naming.ChangeDeclaringType(implementingTypePath.Name, methodID, false);

            var item = getMethodItemFromGeneric(implementingMethod, methodSearchPath);
            if (item == null)
                return null;

            return item.Info.MethodID;
        }

        /// <summary>
        /// Gets inheritance chain for type described by given path.
        /// </summary>
        /// <param name="typePath">The type path.</param>
        /// <returns>InheritanceChain.</returns>
        /// <inheritdoc />
        public override InheritanceChain GetInheritanceChain(PathInfo typePath)
        {
            var typeNode = GetTypeNode(typePath.Signature);

            if (typeNode == null)
                //type hasn't been found
                return null;

            var chain = createInheritanceChain(typeNode);

            if (chain.Type.HasParameters)
            {
                //generic specialization is needed
                chain = chain.MakeGeneric(typePath.GenericArgs);
            }

            return chain;
        }

        #endregion

        #region Method building helpers

        /// <summary>
        /// Get all possible fullnames for given element.
        /// </summary>
        /// <param name="codeElement">Element which fullnames are requested.</param>
        /// <returns>Possible fullnames.</returns>
        protected IEnumerable<string> GetFullNames(CodeElement codeElement)
        {
            var elementFullname = TranslatePath(codeElement.FullName);
            var path = new PathInfo(elementFullname);

            var nsPath = path.PrePathSignature;
            var names = generatePossibleNames(codeElement);
            foreach (var name in names)
            {
                if (nsPath == "")
                    yield return name;
                else
                    yield return nsPath + "." + name;
            }
        }

        /// <summary>
        /// Generate possible names of given element.
        /// </summary>
        /// <param name="element">Element which names will be generated.</param>
        /// <returns>Generated names.</returns>
        private HashSet<string> generatePossibleNames(CodeElement element)
        {
            _namesProvider.ReportedNames.Clear();
            _namesProvider.VisitElement(element);

            var names = _namesProvider.ReportedNames;
            return names;
        }

        /// <summary>
        /// Find node of type specified by given typePathSignature.
        /// </summary>
        /// <param name="typePathSignature">Path to type in signature form.</param>
        /// <returns>Found node if any, <c>null</c> otherwise.</returns>
        protected CodeType GetTypeNode(string typePathSignature)
        {
            return Searcher.Search(typePathSignature).FirstOrDefault() as CodeType;
        }

        /// <summary>
        /// Get <see cref="MethodItem" /> for method described by given methodID.
        /// </summary>
        /// <param name="methodID">Description of desired method.</param>
        /// <returns><see cref="MethodItem" /> for given methodID specialized by genericPath if found, <c>false</c> otherwise.</returns>
        private MethodItem getMethodItem(MethodID methodID)
        {
            return getMethodItemFromGeneric(methodID, Naming.GetMethodPath(methodID));
        }

        /// <summary>
        /// Create <see cref="InheritanceChain" /> enumeration from given typeNodes.
        /// </summary>
        /// <param name="typeNodes">The type nodes.</param>
        /// <returns>IEnumerable&lt;InheritanceChain&gt;.</returns>
        private IEnumerable<InheritanceChain> createInheritanceChains(CodeElements typeNodes)
        {
            var chains = new List<InheritanceChain>();
            foreach (CodeElement typeNode in typeNodes)
            {
                var descriptor = InfoBuilder.CreateDescriptor(typeNode);
                var chain = TypeServices.GetChain(descriptor);
                chains.Add(chain);
            }

            return chains;
        }

        /// <summary>
        /// Find nodes that can be base of methods with given signature
        /// <remarks>Methods can be generated from <see cref="CodeVariable" />, <see cref="CodeFunction" />, <see cref="CodeProperty" /></remarks>.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Found methods.</returns>
        private IEnumerable<CodeElement> findNodesWithMatchingNames(PathInfo path)
        {
            VS.Log.Message("Searching method nodes for {0}", path.Signature);

            var prePathSignature = path.PrePathSignature;

            var typeNodes = Searcher.Search(prePathSignature);
            foreach (var typeNode in typeNodes)
            {

                //even type nodes can generate methods
                var lastPart = path.LastPartSignature;
                if (IsPossibleName(lastPart, typeNode))
                    yield return typeNode;

                //test children
                var children = typeNode.Children();
                foreach (CodeElement child in children)
                {
                    if (IsPossibleName(lastPart, child))
                        yield return child;
                }
            }
        }

        /// <summary>
        /// Create <see cref="InheritanceChain" /> from given typeNode.
        /// </summary>
        /// <param name="typeNode">Type node which inheritance chain will be created.</param>
        /// <returns>Created <see cref="InheritanceChain" />.</returns>
        private InheritanceChain createInheritanceChain(CodeType typeNode)
        {
            var subChains = new List<InheritanceChain>();

            var baseChains = createInheritanceChains(typeNode.Bases);
            subChains.AddRange(baseChains);

            var classNode = typeNode as CodeClass;
            if (classNode != null)
            {
                var interfaceChains = createInheritanceChains(classNode.ImplementedInterfaces);
                subChains.AddRange(interfaceChains);
            }

            var typeDescriptor = InfoBuilder.CreateDescriptor(typeNode);
            return TypeServices.CreateChain(typeDescriptor, subChains);
        }

        /// <summary>
        /// Build <see cref="MethodItem" /> from given generic methodNode.
        /// </summary>
        /// <param name="methodNode">Node from which <see cref="MethodItem" /> is builded.</param>
        /// <param name="methodGenericPath">Arguments for generic method.</param>
        /// <returns>Builded <see cref="MethodItem" />.</returns>
        private MethodItem buildGenericMethod(CodeElement methodNode, PathInfo methodGenericPath)
        {
            var name = methodGenericPath.LastPartSignature;

            var methodItem = ItemBuilder.Build(methodNode, name);
            if (methodGenericPath != null && methodGenericPath.HasGenericArguments)
                //make generic specialization
                methodItem = methodItem.Make(methodGenericPath);

            VS.Log.Message("Building method {0}", methodItem.Info.Path.Name);
            return methodItem;
        }


        /// <summary>
        /// Get <see cref="MethodItem" /> for generic method described by given methodID with specialization according to
        /// generic Path.
        /// </summary>
        /// <param name="methodID">Description of desired method.</param>
        /// <param name="methodGenericPath">Method path specifiing generic substitutions.</param>
        /// <returns><see cref="MethodItem" /> for given methodID specialized by genericPath if found, <c>null</c> otherwise.</returns>
        private MethodItem getMethodItemFromGeneric(MethodID methodID, PathInfo methodGenericPath)
        {
            try
            {
                //from given nodes find the one with matching id
                foreach (var node in findNodesWithMatchingNames(methodGenericPath))
                {
                    //create generic specialization 
                    var methodItem = buildGenericMethod(node, methodGenericPath);

                    if (methodItem.Info.MethodID.MethodString == methodID.MethodString)
                        //we have found matching generic specialization 
                        //omit dynamic resolution flag, because it is transitive-and it dont need to be handled
                        return methodItem;
                }
            }
            catch (NullReferenceException ex)
            {
                //code model can generate unexpected exceptions
                VS.LogException(ex, "Code model in Visual Studio causes NullReferenceException in {0}", Name);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                //code model can generate unexpected exceptions
                VS.LogException(ex, "Code model in Visual Studio causes COMException in {0}", Name);
            }

            return null;
        }

        #endregion
    }
}
