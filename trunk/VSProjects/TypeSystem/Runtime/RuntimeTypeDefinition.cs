﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;
using Analyzing.Execution;

using TypeSystem.Runtime.Building;

using Drawing;

namespace TypeSystem.Runtime
{
    /// <summary>
    /// Value provider for getting objects for edits
    /// </summary>
    /// <returns></returns>
    public delegate object ValueProvider();

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

        protected virtual void draw(DrawingServices services)
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

        internal void Draw(Instance thisInstance, DrawingContext context)
        {
            This = thisInstance;

            try
            {
                //TODO inheritance drawing
                var services = new DrawingServices(This,context);
                draw(services);
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

        protected void RewriteArg(int argIndex, string editName, ValueProvider valueProvider)
        {
            throw new NotImplementedException();
        }

        protected void AddArg(int argIndex, string editName, ValueProvider valueProvider)
        {
            throw new NotImplementedException();
        }
    }
}
