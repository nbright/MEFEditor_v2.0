﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Execution;
using MEFEditor.TypeSystem;
using Utilities;

namespace RecommendedExtensions.Core.TypeDefinitions.CompositionEngine
{

    delegate InstanceRef ExportLoader();


    /// <summary>
    /// Worker which is used for MEF composition simulation.
    /// </summary>
    class CompositionWorker
    {
        /// <summary>
        /// Instances, which prerequisities are currently satisfied - used for circular dependency checking
        /// </summary>
        HashSet<ComponentRef> _currentPrereqInstances = new HashSet<ComponentRef>();

        ComponentStorage _componentsStorage;
        List<Join> _joins = new List<Join>();
        bool _failed;

        CompositionContext _context;

        /// <summary>
        /// imports to its collected exports, if ! _failed, are there all needed exports for defined imports, after constructor call
        /// </summary>
        MultiDictionary<JoinPoint, JoinPoint> _storage = new MultiDictionary<JoinPoint, JoinPoint>();

        /// <summary>
        /// Create composition worker which compose given instances.
        /// </summary>
        /// <param name="context">Services available for interpreting.</param>
        public CompositionWorker(CompositionContext context)
        {
            _context = context;
            _componentsStorage = new ComponentStorage(context);

            if (_componentsStorage.Failed)
            {
                _failed = true;
                return;
            }

            foreach (var inst in _componentsStorage.GetComponents())
            {
                if (inst.ComponentInfo == null)
                    //there is nothing to import
                    continue;

                if (!satisfyComponent(inst))
                {
                    //composition has failed
                    _failed = true;
                    return;
                }
            }
        }


        private bool satisfyComponent(ComponentRef inst)
        {
            if (inst.IsSatisfied)
                //instance has already been satisfied
                return true;

            if (inst.ComposingFailed)
                //instance has been proceeded, but failed
                return false;


            var satisfied = satisfyPreImports(inst) && satisfyNormImports(inst);
            return satisfied;
        }

        /// <summary>
        /// Can be satisfied only after Prerequisities imports has been satisfied
        /// </summary>
        /// <param name="inst"></param>
        /// <returns></returns>
        private bool satisfyNormImports(ComponentRef inst)
        {
            if (!inst.HasSatisfiedPreImports)
                throw new NotSupportedException("InternalError:Cant satisfy imports before prerequisities are satisfied");

            foreach (var import in inst.Imports)
            {
                if (import.IsPrerequisity)
                    //satisfy only normal imports, because prerequisities has to be satisfied now
                    continue;

                if (!satisfyImport(inst, import))
                    //composition failed
                    return false;
            }

            inst.IsSatisfied = true;
            return true;
        }

        /// <summary>
        /// Prerequisity imports has to be satisfied before component construction
        /// </summary>
        /// <param name="inst"></param>
        /// <returns></returns>
        private bool satisfyPreImports(ComponentRef inst)
        {
            if (inst.IsConstructed)
                return true;

            if (inst.ComposingFailed)
                //has been already proceeded
                return false;

            if (_currentPrereqInstances.Contains(inst))
                //circular dependency
                return false;

            var preImports = new List<ComponentRef>();
            bool satisfied = true;

            _currentPrereqInstances.Add(inst); //avoid dependency cycling

            foreach (var import in inst.Imports)
            {
                if (!import.IsPrerequisity)
                    //satisfy only prerequisities
                    continue;

                if (satisfyImport(inst, import))
                    //import was satisfied
                    continue;

                satisfied = false;
                break;
            }

            _currentPrereqInstances.Remove(inst);

            if (!satisfied)
            {
                return false;
            }

            if (!constructInstance(inst))
                return false;

            inst.HasSatisfiedPreImports = true;
            return true;
        }



        /// <summary>
        /// Satisfy given import. Found exports are added int storage
        /// </summary>
        /// <param name="component"></param>
        /// <param name="import"></param>
        /// <returns></returns>
        bool satisfyImport(ComponentRef component, Import import)
        {
            if (import.IsPrerequisity && component.IsConstructed)
                //instance is already constructed - dont need satisfy via importing constructor
                return true;

            var candidates = getExportCandidates(import);
            //determine that import has any candidates 
            //(even those, that cannot be used for import because of missing initialization)

            //filter candidates that are initialized now (circular dependency)
            var initializedCandidates = candidates.Except(_currentPrereqInstances);
            var candidatesInProgress = candidates.Intersect(_currentPrereqInstances);
            var hasInitializedCandidates = initializedCandidates.Any();

            if (!hasInitializedCandidates && !import.AllowDefault)
                return noInitializedCandidatesError(component, import, candidatesInProgress);

            if (initializedCandidates.Count() > 1 && !import.AllowMany)
                return tooManyCandidatesError(component, import, initializedCandidates);

            return satisfyFromCandidates(component, import, initializedCandidates);
        }

        private bool satisfyFromCandidates(ComponentRef component, Import import, IEnumerable<ComponentRef> candidates)
        {
            var importPoint = component.GetPoint(import);
            foreach (var candidate in candidates)
            {
                if (!satisfyPreImports(candidate))
                {
                    setError(importPoint, "Cannot satisfy import, because depending component cannot be instantiated");
                    makeErrorJoins(importPoint, candidate, "This export cannot be provided before prerequisity imports are satisfied");

                    //satisfy all needed requirments
                    return false;
                }

                if (!satisfyImport(component, import, candidate))
                    //errors were set
                    return false;
            }

            if (!import.IsPrerequisity)
                // else it will be set via importing constructor
                setImport(importPoint);

            return true;
        }

        /// <summary>
        /// find exports which satisfies import from exportsProvider (exportsProvider has been instatiated and its exports has to match into import)
        /// found exports are added into storage
        /// 
        /// provide type checking of join, on fail set errors and return false
        /// </summary>
        /// <param name="component"></param>
        /// <param name="import"></param>
        /// <param name="exportsProvider"></param>
        private bool satisfyImport(ComponentRef component, Import import, ComponentRef exportsProvider)
        {
            Debug.Assert(exportsProvider.IsConstructed, "candidate has to be constructed before providing exports");

            var importPoint = component.GetPoint(import);
            foreach (var exportPoint in exportsProvider.ExportPoints)
            {
                if (match(import, exportPoint))
                {
                    var join = new Join(importPoint, exportPoint);
                    _joins.Add(join);

                    if (!typeCheck(join)) return false;
                    _storage.Add(importPoint, exportPoint);
                }
            }

            return true;
        }

        private bool tooManyCandidatesError(ComponentRef component, Import import, IEnumerable<ComponentRef> candidates)
        {
            var importPoint = component.GetPoint(import);
            setError(importPoint, "There are more than one matching component for export satisfiyng");
            foreach (var expProvider in candidates)
                makeErrorJoins(importPoint, expProvider, "Matching export in ambiguous component");
            return false;
        }

        private bool noInitializedCandidatesError(ComponentRef component, Import import, IEnumerable<ComponentRef> candidatesInProgress)
        {
            var importPoint = component.GetPoint(import);

            string error = "Can't satisfy import";
            if (candidatesInProgress.Any())
            {
                error += ", there are circular dependencies in prerequisity imports";

                foreach (var candidate in candidatesInProgress)
                {
                    makeErrorJoins(importPoint, candidate, "Can't get export because of unsatisfied prerequisities");
                }
            }
            else
                error = noMatchingExportError(importPoint, error);

            //set error to import
            setError(importPoint, error);


            if (import.IsPrerequisity)
                setWarning(component.ExportPoints, "Because of unsatisfied prerequisity import, exports cannot be provided");

            return false;
        }

        /// <summary>
        /// Resolve error for unsatisfiable import. Make error joins and set warnings for contract matching export.
        /// </summary>        
        /// <param name="impPoint">Unsatisfiable import.</param>
        /// <param name="errorPrefix">Error prefix.</param>
        /// <returns>Return error description according to given prefix.</returns>
        private string noMatchingExportError(JoinPoint impPoint, string errorPrefix)
        {
            var import = impPoint.Point as Import;
            var exps = getExports(import);
            if (exps.Length == 0)
            {
                //there are no matching components/or there is problem in meta data filtering.
                exps = getExports(import, false);
                if (exps.Length == 0)
                    errorPrefix += " because there are no exports matching contract";
                else
                {
                    errorPrefix += " because of incompatible meta data";
                    makeErrorJoins(impPoint, exps, "Incompatible exported metadata");
                }
            }
            else
            {
                errorPrefix += " because all matching components have ambiguous exports";
                makeErrorJoins(impPoint, exps, "Ambiguous export");
            }
            return errorPrefix;
        }

        /// <summary>
        /// make error joins between matching exports (used for showing errors)
        /// </summary>
        /// <param name="imp"></param>
        /// <param name="exportProvider"></param>
        /// <param name="expWarning"></param>
        private void makeErrorJoins(JoinPoint imp, ComponentRef exportProvider, string expWarning)
        {
            var exps = new List<JoinPoint>();
            foreach (var exp in exportProvider.ExportPoints)
            {
                if (!match(imp.Point as Import, exp)) continue;
                exps.Add(exp);
            }

            makeErrorJoins(imp, exps, expWarning);
        }

        private void makeErrorJoins(JoinPoint imp, IEnumerable<JoinPoint> exps, string expWarning)
        {
            foreach (var exp in exps)
            {
                var join = new Join(imp, exp);
                join.IsErrorJoin = true;
                _joins.Add(join);
                setWarning(exp, expWarning);
            }
        }



        private bool typeCheck(Join join)
        {
            var imp = join.Import;
            var exp = join.Export;
            var impType = imp.ContractType;
            var expType = exp.ContractType;

            var importItem = imp.ImportItemType;
            var importId = imp.AllowMany ? "Import item" : "Import";

            if (!_context.IsOfType(expType, importItem))
            {
                setError(imp, string.Format("{0} is of type {1}, so it cannot accept export of type {2}", importId, importItem.TypeName, expType.TypeName));
                setWarning(exp, "Export contract doesn't provide type safe identification");
                join.IsErrorJoin = true;
                return false;
            }
            return true;
        }


        /// <summary>
        /// Return components which are candidates for import satisfying
        /// </summary>
        /// <param name="import"></param>
        /// <returns></returns>
        IEnumerable<ComponentRef> getExportCandidates(Import import)
        {
            var candidates = _componentsStorage.GetComponents(import.Contract);
            var result = new List<ComponentRef>();
            foreach (var candidate in candidates)
            {
                var matchingExps = new List<JoinPoint>();
                foreach (var exp in candidate.ExportPoints)
                {
                    if (match(import, exp))
                        matchingExps.Add(exp);
                }

                switch (matchingExps.Count) //continue, when candidate doesnt match to import
                {
                    case 0://if no matching export - no candidate                        
                        continue;
                    case 1:
                        break; //all imports can accept one export
                    default: //more than one
                        if (import.AllowMany)
                            break;
                        continue;
                }
                //here are all candidates matching export                
                result.Add(candidate);
            }
            return result;
        }

        /// <summary>
        /// Determine if import is matching to given export.
        /// </summary>
        /// <param name="import">Import to test.</param>
        /// <param name="export">Export to test.</param>
        /// <param name="metaDataTest">Determine if meta data should be used for filtering.</param>
        /// <returns>True if import can be satisfied from export.</returns>
        private bool match(Import import, JoinPoint export, bool metaDataTest = true)
        {
            var importMeta = import.ImportTypeInfo.MetaDataType;

            bool metaDataMatch = true;
            if (importMeta != null && metaDataTest)
            {
                var exportMeta = (export.Point as Export).Meta;

                metaDataMatch = testMetaData(importMeta, exportMeta);
            }

            return import.Contract == export.Contract && metaDataMatch;
        }


        /// <summary>
        /// Test if metadataType can be satisfied from metadata
        /// </summary>
        /// <param name="metadataType"></param>
        /// <param name="meta"></param>
        /// <returns></returns>
        private bool testMetaData(InstanceInfo metadataType, MetaExport meta)
        {
            foreach (var getter in _context.GetOverloads(metadataType))
            {
                if (meta == null)
                    //cannot satisfy metaDataType
                    return false;

                if (getter.Parameters.Length != 0)
                    //is not valid getter method
                    continue;

                var name = getter.MethodName;
                if (!name.StartsWith(Naming.GetterPrefix))
                    //is not valid getter name
                    continue;

                name = name.Substring(Naming.GetterPrefix.Length);

                var getterType = getter.ReturnType;
                var item = meta.GetItem(name);

                if (item == null)
                    return false;

                var metaToSet = findMetaData(getterType, item.Data, item.IsMultiple);
                if (metaToSet == null)
                    //not matching types
                    return false;
            }
            return true;
        }


        private object findMetaData(InstanceInfo getterType, IEnumerable<object> metaData, bool isMultiple)
        {
            if (metaData == null)
                return null;

            int metaDataCount = 0;

            foreach (var item in metaData)
            {
                ++metaDataCount;

                if (item == null)
                    //null is not supported metadata info
                    return null;

                var typeInfo = TypeDescriptor.Create(item.GetType());
                if (!testTypeArrayMatch(typeInfo, getterType, isMultiple))
                    //not matching type
                    return null;
            }

            if (metaDataCount != 1 && !isMultiple)
                //metadata item is not multiple - we cannot load
                //multiple matching data
                return null;

            if (!isMultiple)
                return metaData.First();

            return metaData.ToArray();
        }

        private bool testTypeArrayMatch(InstanceInfo testedType, InstanceInfo targetType, bool arrayTest)
        {
            if (arrayTest)
                return _context.IsOfType(targetType, "Array<" + testedType.TypeName + ",1>");
            else
                return _context.IsOfType(testedType, targetType);
        }


        /// <summary>
        /// return all matching imports in all components
        /// </summary>
        /// <param name="import">Import to get exports for.</param>
        /// <param name="metaDataTest">Determine if metadata are used for export filtering.</param>
        /// <returns>Available exports for import.</returns>
        JoinPoint[] getExports(Import import, bool metaDataTest = true)
        {
            var result = new List<JoinPoint>();
            var candidates = _componentsStorage.GetComponents(import.Contract);
            foreach (var candidate in candidates)
                foreach (var exp in candidate.ExportPoints)
                    if (match(import, exp, metaDataTest)) result.Add(exp);

            return result.ToArray();
        }


        /// <summary>
        /// enqueue setter call which satisfy import from exports
        /// </summary>
        /// <param name="import"></param>    
        private void setImport(JoinPoint import)
        {
            var exps = _storage.Get(import);
            if (exps == null) return; //allow default doesnt require setter

            foreach (var exp in exps)
                _joins.Add(new Join(import, exp));

            callSetter(import);
        }

        private void callSetter(JoinPoint import) //call setter
        {
            var imp = import.Point as Import;

            var export = createExport(import);

            if (export != null)
                import.Instance.Call(imp.Setter, export);
        }
        /// <summary>
        /// enqueue call importing/default constructor on instance
        /// </summary>
        /// <param name="inst"></param>        
        private bool constructInstance(ComponentRef inst)
        {
            if (!inst.HasImportingConstructor)
            {
                setError(inst.ExportPoints, "Cannot provide exports because of missing importing or parameter less constructor");
                setError(inst.ImportPoints, "Cannot set imports, because of missing importing or parameter less constructor");
                _failed = true;
                return false;
            }

            callImportingConstructor(inst);
            return true;
        }

        private void callImportingConstructor(ComponentRef component)
        {
            var args = new List<InstanceRef>();

            foreach (var import in component.Imports)
                if (import.Setter == null) //has to be satisfied via importing constructor
                {
                    var imp = component.GetPoint(import);
                    args.Add(createExport(imp));
                }

            component.Construct(component.ComponentInfo.ImportingConstructor, args.ToArray());
        }

        /// <summary>
        /// Create export for given import, from exports available in storage
        /// Is lazy determine, if export will be wrapped into lazys
        /// </summary>
        /// <param name="imp"></param>
        /// <returns></returns>
        private InstanceRef createExport(JoinPoint imp)
        {
            var exps = _storage.Get(imp);
            switch (exps.Count())
            {
                case 0:
                    return null;
                case 1:
                    if (imp.AllowMany) return callManyExportGetter(imp, exps);
                    else return callExportGetter(imp, exps.First());

                default:
                    return callManyExportGetter(imp, exps);
            }
        }

        private InstanceRef callExportGetter(JoinPoint import, JoinPoint export)
        {
            var exp = export.Point as Export;
            var imp = import.Point as Import;
            var importInfo = imp.ImportTypeInfo;

            InstanceRef exportedInstanceRef;
            if (exp.Getter == null)
            {
                //self export
                exportedInstanceRef = export.Instance;
            }
            else
            {
                //export from getter
                exportedInstanceRef = export.Instance.CallWithReturn(exp.Getter);
            }

            if (!importInfo.IsItemLazy)
                return exportedInstanceRef;

            //we have to create lazy export
            if (importInfo.MetaDataType == null)
            {
                //only wrap instance to lazy object
                return _context.CallDirectWithReturn((c, args) =>
                {
                    //prepare exported data
                    var exportInstance = args[0];
                    var lazy = new Lazy<InstanceWrap>(() => new InstanceWrap(exportInstance));

                    //create instance
                    return c.Machine.CreateDirectInstance(lazy);
                }, exportedInstanceRef);
            }

            //we have to include metadata also
            return _context.CallDirectWithReturn((c, args) =>
            {
                //prepare exported data
                var exportInstance = args[0];
                var metaInstance = createMetaDataInstance(c, importInfo.MetaDataType, exp.Meta);
                var lazy = new Lazy<InstanceWrap, InstanceWrap>(() => new InstanceWrap(exportInstance), new InstanceWrap(metaInstance));

                //create instance
                return c.Machine.CreateDirectInstance(lazy);
            }, exportedInstanceRef);
        }

        private Instance createMetaDataInstance(AnalyzingContext analyzingContext, TypeDescriptor metaDataType, MetaExport metaExport)
        {
            var instance = analyzingContext.Machine.CreateInstance(metaDataType);

            //register call handler for instance, that 
            //will handle all method calls
            _context.RegisterCallHandler(instance, (c) =>
            {
                var method = Naming.GetMethodName(c.CurrentCall.Name);
                var key = method.Substring(Naming.GetterPrefix.Length);
                var item = metaExport.GetItem(key);

                var metaDataInstance = createMetaItemInstance(analyzingContext, item);
                c.Return(metaDataInstance);
            });

            return instance;
        }

        private Instance createMetaItemInstance(AnalyzingContext analyzingContext, MetaItem item)
        {
            var metaDataObject = item.IsMultiple ? item.Data.ToArray() : item.Data.First();

            return createInstance(analyzingContext, metaDataObject);
        }

        private Instance createInstance(AnalyzingContext context, object directObject)
        {
            if (directObject is Array)
            {
                var enumerable = directObject as System.Collections.IEnumerable;
                var array = new MEFEditor.TypeSystem.Runtime.Array<InstanceWrap>(enumerable, context);

                return context.Machine.CreateDirectInstance(array);
            }
            else
            {
                return context.Machine.CreateDirectInstance(directObject);
            }
        }

        private InstanceRef callManyExportGetter(JoinPoint import, IEnumerable<JoinPoint> exps)
        {
            var exportValues = new List<InstanceRef>();

            foreach (var exp in exps)
                exportValues.Add(callExportGetter(import, exp));


            InstanceRef iCollectionToSet;
            MethodID addMethod;

            if (isICollectionImport(import, out iCollectionToSet, out addMethod))
            {
                //import will be filled by adding instances            
                if (iCollectionToSet == null)
                {
                    //there is no collection which could be set.
                    setError(import, "Cannot get ICollection object, for importing exports, because of missing getter.");
                    return null;
                }

                var arguments = new[] { iCollectionToSet }.Union(exportValues);

                _context.CallDirect((c, args) =>
                    fillICollection(args[0], args.Skip(1).ToArray(), import, addMethod, c),
                    arguments.ToArray());

                //there is no returned value because import is filled via add
                return null;
            }
            else
            {
                //import will be filled with an array
                var arr = _context.CreateArray(import.ImportItemType, exportValues);
                if (!_context.IsOfType(import.ContractType, arr.Type))
                {
                    setError(import, "Import type cannot handle multiple exports");
                    return null;
                }

                return arr;
            }
        }

        private void fillICollection(Instance importCollection, Instance[] exports, JoinPoint import, MethodID addMethod, AnalyzingContext c)
        {
            if (exports.Length == 0)
                //there is nothing to export
                return;

            //test if import can be filled
            if (_context.IsNull(importCollection))
            {
                setWarning(import, "ICollection import was not initialized after component creation");
                return;
            }

            //fill with exports
            for (var i = 0; i < exports.Length; ++i)
            {
                var export = exports[i];
                c.DynamicCall(addMethod, importCollection, export);
            }
        }

        private bool isICollectionImport(JoinPoint import, out InstanceRef iCollectionToSet, out MethodID addMethod)
        {
            addMethod = null;
            iCollectionToSet = null;

            //Detect if import type is of ICollection
            var imp = import.Point as Import;
            var itemType = imp.ImportTypeInfo.ItemType;
            var collectionTypeName = string.Format("System.Collections.Generic.ICollection<{0}>", itemType.TypeName);
            var collectionType = TypeDescriptor.Create(collectionTypeName);

            var isICollection = _context.IsOfType(imp.ImportTypeInfo.ImportType, collectionType);
            if (!isICollection)
                //cannot resolve type as ICollection
                return false;

            var collectionAddMethod = _context.GetMethod(collectionType, "Add").MethodID;
            addMethod = collectionAddMethod;

            iCollectionToSet = getImportInstance(import);
            return true;
        }

        private InstanceRef getImportInstance(JoinPoint import)
        {
            var imp = import.Point as Import;

            var setter = imp.Setter;
            var setterName = Naming.GetMethodName(setter);
            if (setterName == null || !setterName.StartsWith(Naming.SetterPrefix))
                //cannot find setter -> cannot get getter
                return null;

            var getterName = Naming.GetterPrefix + setterName.Substring(Naming.SetterPrefix.Length);
            var instType = import.Instance.Type;

            var getter = _context.TryGetMethod(instType, getterName);
            if (getter == null)
                //cannot resolve getter overload
                return null;


            return import.Instance.CallWithReturn(getter.MethodID);
        }

        /// <summary>
        /// Set error for given joinPoints
        /// </summary>
        /// <param name="points"></param>
        /// <param name="error"></param>
        private void setError(IEnumerable<JoinPoint> points, string error)
        {
            foreach (var joinPoint in points) setError(joinPoint, error);
        }

        private void setError(JoinPoint point, string error)
        {
            _failed = true;
            if (point.Error != null)
            {
                if (point.Error.Contains(error))
                    //same error has already been set.
                    return;
                point.Error += "\n" + error;
            }
            else point.Error = error;
        }


        private void setWarning(IEnumerable<JoinPoint> points, string warning)
        {
            foreach (var point in points) setWarning(point, warning);
        }

        private void setWarning(JoinPoint point, string warning)
        {
            if (point.Warning != null)
            {
                if (point.Warning.Contains(warning))
                    //same warning has already been set.
                    return;
                point.Warning += "\n" + warning;
            }
            else point.Warning = warning;
        }

        /// <summary>
        /// Simulate composition. If composition doesn't failed, call appropriate constructors/setters and satisfy all imports of all available components.
        /// </summary>
        /// <returns>CompositionResult collected during composition simulation.</returns>
        internal CompositionResult GetResult()
        {
            var joins = _joins.ToArray();
            var points = _componentsStorage.GetPoints();

            if (_componentsStorage.Failed)
                return new CompositionResult(_context, joins, points, _context.Generator, _componentsStorage.Error);

            string error = null;
            if (_failed)
            {
                error = "Composition failed because there were some errors";
            }

            return new CompositionResult(_context, joins, points, _context.Generator, error);
        }
    }
}
