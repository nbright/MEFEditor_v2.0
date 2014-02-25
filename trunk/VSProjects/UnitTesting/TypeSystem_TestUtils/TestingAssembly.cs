﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

using Analyzing;
using Analyzing.Editing;
using TypeSystem;
using TypeSystem.Runtime;

using Mono.Cecil;

using AssemblyProviders.CSharp;
using AssemblyProviders.CSharp.Compiling;
using AssemblyProviders.CIL;

using UnitTesting.Analyzing_TestUtils;
using UnitTesting.Analyzing_TestUtils.Environment;

namespace UnitTesting.TypeSystem_TestUtils
{
    public delegate void ResultAction(AnalyzingResult result);

    public class TestingAssembly : AssemblyProvider
    {
        /// <summary>
        /// Methods contained in current assembly
        /// </summary>
        HashedMethodContainer _methods = new HashedMethodContainer();

        /// <summary>
        /// Testing simulation of edit actions
        /// </summary>
        List<EditAction> _editActions = new List<EditAction>();

        /// <summary>
        /// Testing simulation of user actions
        /// </summary>
        List<ResultAction> _userActions = new List<ResultAction>();

        /// <summary>
        /// Actions that are processed before runtime build
        /// </summary>
        List<Action> _beforeRuntimeBuildActions = new List<Action>();

        /// <summary>
        /// Actions that are processed after runtime builded
        /// </summary>
        List<Action> _afterRuntimeActions = new List<Action>();

        /// <summary>
        /// Current assembly collection
        /// </summary>
        internal readonly TestAssemblyCollection Assemblies;

        /// <summary>
        /// Method loader used by assembly
        /// </summary>
        public readonly AssemblyLoader Loader;

        /// <summary>
        /// because of accessing runtime adding services for testing purposes
        /// </summary>
        public readonly RuntimeAssembly Runtime;

        /// <summary>
        /// Settings available for machine
        /// </summary>
        public readonly MachineSettings Settings;

        /// <summary>
        /// Current application domain
        /// </summary>
        public AppDomainServices AppDomain { get { return Loader.AppDomain; } }

        /// <summary>
        /// Testing simulation of user actions
        /// </summary>
        public IEnumerable<ResultAction> UserActions { get { return _userActions; } }

        /// <summary>
        /// Testing simulation of edit actions
        /// </summary>
        public IEnumerable<EditAction> EditActions { get { return _editActions; } }

        /// <summary>
        /// Current machine
        /// </summary>
        public readonly Machine Machine;

        /// <summary>
        /// Determine that assembly has been already builded. Methods can be added even after builded.
        /// </summary>
        public bool IsBuilded { get; private set; }

        public TestingAssembly(MachineSettings settings)
        {
            Settings = settings;
            Runtime = settings.Runtime;
            Assemblies = new TestAssemblyCollection(Runtime, this);
            Machine = SettingsProvider.CreateMachine(Settings);

            Loader = new AssemblyLoader(Assemblies, Settings);
        }

        public void Build()
        {
            if (IsBuilded)
                throw new NotSupportedException("Runtime can't be builded");

            IsBuilded = true;

            foreach (var beforeAction in _beforeRuntimeBuildActions)
            {
                beforeAction();
            }

            Runtime.BuildAssembly();

            foreach (var afterAction in _afterRuntimeActions)
            {
                afterAction();
            }
        }

        #region Adding methods to current assembly

        public TestingAssembly AddMethod(string methodPath, string code, MethodDescription description)
        {
            var methodInfo = buildDescription(description, methodPath);
            var genericParameters = new PathInfo(methodPath).GenericArgs;

            var sourceCode = "{" + code + "}";
            var method = new ParsedGenerator(methodInfo, sourceCode, genericParameters, TypeServices);

            addMethod(method, methodInfo, description.Implemented);

            return this;
        }

        public TestingAssembly AddMethod(string methodPath, DirectMethod source, MethodDescription description)
        {
            var methodInfo = buildDescription(description, methodPath);

            var method = new DirectGenerator(source);
            addMethod(method, methodInfo, description.Implemented);

            return this;
        }

        public TestingAssembly AddMethod(string methodPath, MethodInfo sourceMethod, MethodDescription description)
        {
            var methodInfo = buildDescription(description, methodPath);

            var source = new CILMethod(sourceMethod);
            var method = new CILGenerator(methodInfo, source, TypeServices);
            addMethod(method, methodInfo, description.Implemented);

            return this;
        }

        public TestingAssembly AddMethod(string methodPath, MethodDefinition sourceMethod, MethodDescription description)
        {
            var methodInfo = buildDescription(description, methodPath);

            var source = new CILMethod(sourceMethod);
            var method = new CILGenerator(methodInfo, source, TypeServices);
            addMethod(method, methodInfo, description.Implemented);

            return this;
        }

        #endregion

        #region Assembly reference handling

        public TestingAssembly RegisterAssembly(AssemblyProvider testAssembly)
        {
            afterRuntimeAction(() =>
            {
                TypeServices.RegisterAssembly(testAssembly);
                var runtime = testAssembly as RuntimeAssembly;
                if (runtime != null)
                    runtime.BuildAssembly();
            });

            return this;
        }

        public TestingAssembly AddReference(AssemblyProvider assembly)
        {
            afterRuntimeAction(() =>
            {
                Assemblies.Add(assembly);
            });

            return this;
        }

        public TestingAssembly RemoveReference(AssemblyProvider assembly)
        {
            afterRuntimeAction(() =>
            {
                Assemblies.Remove(assembly);
            });

            return this;
        }

        #endregion

        #region Runtime preparation

        public TestingAssembly AddToRuntime<T>()
            where T : DataTypeDefinition
        {
            beforeRuntimeAction(() =>
            {
                var runtimeTypeDef = Activator.CreateInstance<T>();
                Runtime.AddDefinition(runtimeTypeDef);
            });

            return this;
        }

        public TestingAssembly AddDirectToRuntime<T>()
        {
            beforeRuntimeAction(() =>
            {
                SettingsProvider.AddDirectType(Runtime, typeof(T));
            });

            return this;
        }

        /// <summary>
        /// Generic parameters has to be satisfiable by Instance
        /// </summary>
        /// <param name="genericType">Type which generic arguments will be substituted by WrappedInstance</param>
        /// <returns></returns>
        public TestingAssembly AddWrappedGenericToRuntime(Type genericType)
        {
            beforeRuntimeAction(() =>
            {
                SettingsProvider.AddDirectType(Runtime, genericType);
            });

            return this;
        }

        #endregion

        #region Testing Simulation of user IO

        public TestingAssembly UserAction(ResultAction action)
        {
            _userActions.Add(action);

            return this;
        }

        public TestingAssembly RunEditAction(string variable, string editName)
        {
            var editAction = EditAction.Edit(new VariableName(variable), editName);
            _editActions.Add(editAction);
            return this;
        }

        public TestingAssembly RunRemoveAction(string variable)
        {
            var editAction = EditAction.Remove(new VariableName(variable));
            _editActions.Add(editAction);
            return this;
        }

        public string GetSource(MethodID method, ExecutionView view)
        {
            var parsedGenerator = _methods.AccordingId(method) as ParsedGenerator;

            if (parsedGenerator == null)
                return "Source not available for " + method;

            return parsedGenerator.Source.Code(view);
        }

        public void SetSource(MethodID method, string sourceCode)
        {
            var parsedGenerator = _methods.AccordingId(method) as ParsedGenerator;
            parsedGenerator.SourceCode = sourceCode;
        }

        #endregion

        #region Assembly provider implementatation


        protected override string getAssemblyFullPath()
        {
            return "//TestingAssembly";
        }

        protected override string getAssemblyName()
        {
            return "TestingAssembly";
        }

        public override SearchIterator CreateRootIterator()
        {
            requireBuilded();
            return new HashIterator(_methods);
        }

        public override GeneratorBase GetMethodGenerator(MethodID method)
        {
            requireBuilded();
            return _methods.AccordingId(method);
        }

        public override GeneratorBase GetGenericMethodGenerator(MethodID method, PathInfo searchPath)
        {
            requireBuilded();
            return _methods.AccordingGenericId(method, searchPath);
        }

        public override MethodID GetImplementation(MethodID method, TypeDescriptor dynamicInfo)
        {
            requireBuilded();
            return _methods.GetImplementation(method, dynamicInfo);
        }

        public override MethodID GetGenericImplementation(MethodID methodID, PathInfo methodSearchPath, PathInfo implementingTypePath)
        {
            requireBuilded();
            return _methods.GetGenericImplementation(methodID, methodSearchPath, implementingTypePath);
        }

        public override InheritanceChain GetInheritanceChain(PathInfo typePath)
        {
            requireBuilded();
            //for testing we dont need to handle inheritance chain
            return null;
        }

        #endregion

        #region Private utils

        private void requireBuilded()
        {
            if (!Runtime.IsBuilded)
            {
                throw new NotSupportedException("Operation cannot be processed when assembly is not builded");
            }
        }

        private void afterRuntimeAction(Action action)
        {
            if (Runtime.IsBuilded)
            {
                // runtime is builded action can be done
                action();
            }
            else
            {
                //wait until runtime is builded
                _afterRuntimeActions.Add(action);
            }
        }

        private void beforeRuntimeAction(Action action)
        {
            if (Runtime.IsBuilded)
                throw new NotSupportedException("Cannot add action after runtime is builded");

            _beforeRuntimeBuildActions.Add(action);
        }

        private TypeMethodInfo buildDescription(MethodDescription description, string methodPath)
        {
            var info = description.CreateInfo(methodPath);
            return info;
        }

        private void addMethod(GeneratorBase method, TypeMethodInfo info, IEnumerable<InstanceInfo> implementedTypes)
        {
            var implemented = implementedTypes.ToArray();
            _methods.AddItem(new MethodItem(method, info), implemented);
        }

        #endregion

    }
}
