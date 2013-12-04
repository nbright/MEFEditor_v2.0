﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;
using Analyzing.Execution;

using Analyzing.Editing;

using TypeSystem.DrawingServices;
using TypeSystem.Runtime.Building;

using Drawing;

namespace TypeSystem.Runtime
{

    /// <summary>
    /// Base class for runtime type definitions
    /// <remarks>Its used for defining analyzing types</remarks>
    /// </summary>
    public abstract class RuntimeTypeDefinition
    {
        /// <summary>
        /// Determine that defined type is generic
        /// </summary>
        public bool IsGeneric;

        /// <summary>
        /// Determine that defined type is interface
        /// </summary>
        public bool IsInterface;

        /// <summary>
        /// Available type services
        /// </summary>
        protected TypeServices Services { get; private set; }

        protected EditsProvider Edits { get { return Context.Edits; } }

        /// <summary>
        /// Component info of type (null if type is not a component)
        /// </summary>
        internal protected ComponentInfo ComponentInfo { get; protected set; }

        /// <summary>
        /// Assembly where type builded from this definition is present
        /// </summary>
        internal protected RuntimeAssembly ContainingAssembly { get; private set; }

        /// <summary>
        /// Context available for currently invoked call (Null, when no call is invoked)
        /// </summary>
        internal protected AnalyzingContext Context { get; private set; }

        /// <summary>
        /// Arguments available for currently invoked call
        /// </summary>
        internal protected Instance[] CurrentArguments { get { return Context.CurrentArguments; } }

        internal protected Instance This { get; private set; }

        abstract internal InstanceInfo TypeInfo { get; }

        abstract internal IEnumerable<RuntimeMethodGenerator> GetMethods();

        protected InstanceInfo GetTypeInfo()
        {
            return TypeInfo;
        }

        protected virtual void draw(InstanceDrawer drawer)
        {
        }

        internal void Initialize(RuntimeAssembly containingAssembly, TypeServices typeServices)
        {
            if (containingAssembly == null)
                throw new ArgumentNullException("runtimeAssembly");

            if (typeServices == null)
                throw new ArgumentNullException("typeServices");

            Services = typeServices;
            ContainingAssembly = containingAssembly;
        }

        /// <summary>
        /// Unwrap given instance into type T
        /// <remarks>Is called from code emitted by expression tree</remarks>
        /// </summary>
        /// <typeparam name="T">Type to which instance will be unwrapped</typeparam>
        /// <param name="instance">Unwrapped instance</param>
        /// <returns>Unwrapped data</returns>
        internal protected virtual T Unwrap<T>(Instance instance)
        {
            if (typeof(T).IsArray)
            {
                var arrayDef = instance.DirectValue as Array<InstanceWrap>;
                return arrayDef.Unwrap<T>();
            }
            else
            {
                return (T)instance.DirectValue;
            }
        }

        /// <summary>
        /// Wrap given data of type T into instance
        /// <remarks>Is called from code emitted by expression tree</remarks>
        /// </summary>
        /// <typeparam name="T">Type from which instance will be wrapped</typeparam>
        /// <param name="context">Data to be wrapped</param>
        /// <returns>Instance wrapping given data</returns>
        internal protected virtual Instance Wrap<T>(AnalyzingContext context, T data)
        {
            var machine = context.Machine;
            if (typeof(T).IsArray)
            {
                var array = new Array<InstanceWrap>((System.Collections.IEnumerable)data, context);
                return machine.CreateDirectInstance(array, InstanceInfo.Create<T>());
            }
            else
            {
                return machine.CreateDirectInstance(data);
            }
        }

        internal void Invoke(AnalyzingContext context, DirectMethod methodToInvoke)
        {
            Context = context;

            try
            {
                This = CurrentArguments[0];
                methodToInvoke(context);
            }
            finally
            {
                This = null;
                Context = null;
            }
        }


        internal void Draw(DrawedInstance toDraw)
        {
            This = toDraw.WrappedInstance;

            try
            {
                draw(toDraw.InstanceDrawer);
            }
            finally
            {
                This = null;
            }
        }

        internal virtual InstanceInfo GetInstanceInfo(Type type)
        {
            //TODO consider generic params
            return new InstanceInfo(type);
        }

        protected void AsyncCall<TResult>(Instance calledObject, string callName, Action<TResult> callback)
        {
            var searcher = Services.CreateSearcher();
            searcher.ExtendName(calledObject.Info.TypeName);

            searcher.Dispatch(callName);

            if (!searcher.HasResults)
                throw new KeyNotFoundException("Cannot found method: " + callName + ", on " + calledObject);

            var foundMethods = searcher.FoundResult.ToArray();
            if (foundMethods.Length > 1)
                throw new NotSupportedException("Cannot process async call on ambiguous call: " + callName + ", on" + calledObject);

            var callGenerator = new DirectedGenerator((e) =>
            {
                var arg1 = e.GetTemporaryVariable();

                e.AssignArgument(arg1, calledObject.Info, 1);
                e.Call(foundMethods[0].MethodID, arg1, Arguments.Values());

                var callReturn = e.GetTemporaryVariable();
                e.AssignReturnValue(callReturn, InstanceInfo.Create<object>());

                e.DirectInvoke((context) =>
                {
                    var callValue = context.GetValue(new VariableName(callReturn));
                    var unwrapped=Unwrap<TResult>(callValue);
                    Invoke(context, (c) => callback(unwrapped));
                });
            });


            Context.DynamicCall(callName, callGenerator, This, calledObject);
        }

        protected void ReportChildAdd(int childArgIndex, string childDescription, bool removeOnlyArg = false)
        {
            var child = CurrentArguments[childArgIndex];
            var editName = ".exclude";

            if (removeOnlyArg)
            {
                Edits.AttachRemoveArgument(This, child, childArgIndex, editName);
            }
            else
            {
                Edits.AttachRemoveCall(This, child, editName);
            }
        }

        protected void AddCallEdit(CallProvider accepter)
        {
            Edits.AddCall(This, ".accept", accepter);
        }

        protected void RewriteArg(int argIndex, string editName, ValueProvider valueProvider)
        {
            if (CurrentArguments.Length >= argIndex)
                return;

            Edits.ChangeArgument(This, argIndex, editName, valueProvider);
        }

        protected void AddArg(int argIndex, string editName, ValueProvider valueProvider)
        {
            if (CurrentArguments.Length < argIndex)
                return;


            Edits.AppendArgument(CurrentArguments[0], editName, valueProvider);
        }
    }
}
