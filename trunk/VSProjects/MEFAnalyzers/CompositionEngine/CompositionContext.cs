﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;
using TypeSystem;
using Utilities;


namespace MEFAnalyzers.CompositionEngine
{

    /// <summary>
    /// Context of composition provides access to type and emitting services
    /// </summary>
    public class CompositionContext
    {
        private readonly TypeServices _services;

        private readonly HashSet<ComponentRef> _componentRefs = new HashSet<ComponentRef>();

        private readonly Dictionary<InstanceRef, string> _instanceStorages = new Dictionary<InstanceRef, string>();

        internal readonly CompositionGenerator Generator = new CompositionGenerator();

        internal IEnumerable<ComponentRef> Components { get { return _componentRefs; } }

        internal CompositionContext(TypeServices services)
        {
            _services = services;
        }

        internal void AddConstructedComponents(params Instance[] components)
        {
            for (int i = 0; i < components.Length; ++i)
            {
                var component = components[i];
                var info = _services.GetComponentInfo(component);
                var componentRef = new ComponentRef(this, component, true, info);

                _componentRefs.Add(componentRef);
                addArgumentComponent(i, component, componentRef);
            }
        }

        /// <summary>
        /// Add component which will be available at given argument index in composition call
        /// </summary>
        /// <param name="argumentIndex"></param>
        /// <param name="component"></param>
        /// <param name="componentRef"></param>
        private void addArgumentComponent(int argumentIndex, Instance component, ComponentRef componentRef)
        {
            var storage = string.Format("arg_{0}", argumentIndex);
            emit((e) => e.AssignArgument(storage, component.Info, (uint)argumentIndex));
            _instanceStorages.Add(componentRef, storage);
        }

        /// <summary>
        /// Determine that testedType is type(C# is operator type is testedType)
        /// </summary>
        /// <param name="testedType"></param>
        /// <param name="testedType"></param>
        /// <returns></returns>
        internal bool IsOfType(InstanceInfo testedType, string type)
        {
            if (testedType.TypeName == type)
                return true;

            throw new NotImplementedException();
        }

        internal bool IsOfType(InstanceInfo testedtype, InstanceInfo type)
        {
            return IsOfType(testedtype, type.TypeName);
        }

        internal InstanceRef CreateArray(InstanceInfo itemType, IEnumerable<InstanceRef> instances)
        {
            var instArray = instances.ToArray();

            var arrayInfo = new InstanceInfo(string.Format("Array<{0},1>", itemType.TypeName));
            var intParam = ParameterTypeInfo.Create("p", InstanceInfo.Create<int>());
            var ctorID = Naming.Method(arrayInfo, "#ctor", intParam);
            var setID = Naming.Method(arrayInfo, "set_Item", intParam, ParameterTypeInfo.Create("p2", itemType));

            var arrayStorage = getFreeStorage("arr");

            emit((e) =>
            {
                //array constrution
                e.AssignNewObject(arrayStorage, arrayInfo);
                var lengthVar = e.GetTemporaryVariable("len");
                e.AssignLiteral(lengthVar, instArray.Length);
                e.Call(ctorID, arrayStorage, Arguments.Values(lengthVar));

                //set instances to appropriate indexes
                var arrIndex = e.GetTemporaryVariable("set");
                for (int i = 0; i < instArray.Length; ++i)
                {
                    var instStorage = getStorage(instArray[i]);
                    e.AssignLiteral(arrIndex, i);
                    e.Call(setID, arrayStorage, Arguments.Values(arrIndex, instStorage));
                }
            });

            var array = new InstanceRef(this, arrayInfo, true);
            _instanceStorages[array] = arrayStorage;

            return array;
        }


        internal IEnumerable<TypeMethodInfo> GetOverloads(InstanceInfo type, string methodName)
        {
            var searcher = _services.CreateSearcher();
            searcher.SetCalledObject(type);
            searcher.Dispatch(methodName);

            return searcher.FoundResult;
        }

        internal TypeMethodInfo GetMethod(InstanceInfo type, string methodName)
        {
            var method = TryGetMethod(type, methodName);
            if (method == null)
                throw new NotSupportedException("Cannot get " + methodName + " method for " + type.TypeName);

            return method;
        }

        internal TypeMethodInfo TryGetMethod(InstanceInfo type, string methodName)
        {
            var overloads = GetOverloads(type, methodName);
            if (overloads.Count() != 1)
            {
                return null;
            }

            return overloads.First();
        }

        internal void Call(InstanceRef calledInstance, MethodID methodID, InstanceRef[] arguments)
        {
            checkNull(methodID);
            var inst = getStorage(calledInstance);
            var args = getArgumentStorages(arguments);

            emit((e) => e.Call(methodID, inst, args));
        }

        internal InstanceRef CallWithReturn(InstanceRef calledInstance, MethodID methodID, InstanceRef[] arguments)
        {
            checkNull(methodID);
            var inst = getStorage(calledInstance);
            var args = getArgumentStorages(arguments);


            var resultStorage = getFreeStorage("ret");
            //TODO determine result type
            var resultInstance = new InstanceRef(this, null, true);

            _instanceStorages.Add(resultInstance, resultStorage);

            emit((e) =>
            {
                e.Call(methodID, inst, args);
                e.AssignReturnValue(resultStorage, resultInstance.Type);
            });

            return resultInstance;
        }

        private string getStorage(InstanceRef instance)
        {
            return _instanceStorages[instance];
        }

        private Arguments getArgumentStorages(InstanceRef[] arguments)
        {
            var argVars = (from arg in arguments select getStorage(arg)).ToArray();
            return Arguments.Values(argVars);
        }

        private void checkNull(MethodID methodID)
        {
            if (methodID == null)
            {
                throw new ArgumentNullException("methodID");
            }
            //everything is OK
        }

        private void emit(EmitAction action)
        {
            Generator.EmitAction(action);
        }

        private string getFreeStorage(string nameHint)
        {
            //TODO check for presence
            return string.Format("{0}_{1}", nameHint, _instanceStorages.Count);
        }

        internal MethodID TryGetImplementation(InstanceInfo type, MethodID abstractMethod)
        {            
            return _services.TryGetImplementation(type, abstractMethod);
        }
    }

}