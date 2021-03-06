﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;
using System.Linq.Expressions;

using Utilities;

using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Execution;

using MEFEditor.TypeSystem.Runtime.Building;

namespace MEFEditor.TypeSystem.Runtime
{
    /// <summary>
    /// Represents definitions of types that are stored directly in DirectInstance.
    /// </summary>
    /// <typeparam name="DirectType">DirectType represented by this definition</typeparam>
    public class DirectTypeDefinition<DirectType> : DirectTypeDefinition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectTypeDefinition{DirectType}"/> class.
        /// </summary>
        public DirectTypeDefinition()
            : base(typeof(DirectType))
        {
        }

        /// <summary>
        /// Gets strongly typed representation
        /// of stored direct (native) value.
        /// </summary>
        /// <value>The direct value.</value>
        protected DirectType ThisValue
        {
            get
            {
                var directValue = This.DirectValue;
                if (directValue == null)
                    return default(DirectType);

                return (DirectType)directValue;
            }
        }
    }

    /// <summary>
    /// Represents definitions of types that are stored directly in DirectInstance.
    /// </summary>
    public class DirectTypeDefinition : RuntimeTypeDefinition
    {
        /// <summary>
        /// Method generators added explicitly to direct type
        /// <remarks>They can replace direct type methods</remarks>.
        /// </summary>
        private readonly List<RuntimeMethodGenerator> _explicitGenerators = new List<RuntimeMethodGenerator>();

        /// <summary>
        /// <see cref="Type"/> representation of direct type.
        /// </summary>
        internal readonly Type DirectType;

        /// <summary>
        /// Wrapped type representation of direct type.
        /// </summary>
        /// <value>The type of the wrapped direct.</value>
        internal Type WrappedDirectType { get { return getWrappedType(DirectType); } }

        /// <summary>
        /// Type descriptor that is current definition forced to use.
        /// </summary>
        public TypeDescriptor ForcedInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectTypeDefinition"/> class.
        /// </summary>
        /// <param name="directType">Direct type.</param>
        public DirectTypeDefinition(Type directType)
        {
            DirectType = directType;
            IsInterface = DirectType.IsInterface;
        }

        /// <summary>
        /// Gets the sub chains.
        /// </summary>
        /// <returns>IEnumerable&lt;InheritanceChain&gt;.</returns>
        internal override IEnumerable<InheritanceChain> GetSubChains()
        {
            return GetSubChains(DirectType);
        }

        /// <summary>
        /// Type info of current DirectType (or generic definition if TypeDefinition is marked with IsGeneric).
        /// </summary>
        /// <value>The type information.</value>
        public override TypeDescriptor TypeInfo
        {
            get
            {
                if (ForcedInfo != null)
                    return ForcedInfo;

                var definingType = DirectType;
                if (DirectType.ContainsGenericParameters)
                {
                    definingType = DirectType.GetGenericTypeDefinition();
                }

                return TypeDescriptor.Create(definingType);
            }
        }

        /// <summary>
        /// Add explicit method to direct type
        /// <remarks>Added method can replace existing method in direct type, if it has same method signature</remarks>.
        /// </summary>
        /// <param name="method">Added direct method which is invoked on method call.</param>
        /// <param name="methodInfo">Info of added method.</param>
        /// <param name="implementTypes">The implement types.</param>
        protected void AddMethod(DirectMethod method, TypeMethodInfo methodInfo, params Type[] implementTypes)
        {
            _explicitGenerators.Add(new RuntimeMethodGenerator(method, methodInfo, implementTypes.ToArray()));
        }

        /// <summary>
        /// Get all methods defined for direct type (including explicit methods).
        /// </summary>
        /// <returns>Defined methods.</returns>
        internal override IEnumerable<RuntimeMethodGenerator> GetMethods()
        {
            //TODO resolve method replacing
            return generateDirectMethods(DirectType).Union(_explicitGenerators);
        }

        #region Direct methods generation

        /// <summary>
        /// Generate direct method for given type
        /// <remarks>Only direct methods which are in direct cover are generated</remarks>.
        /// </summary>
        /// <param name="type">Type which methods will be generated.</param>
        /// <returns>Generated methods.</returns>
        private IEnumerable<RuntimeMethodGenerator> generateDirectMethods(Type type)
        {
            foreach (var method in generatePublicMethods(type))
            {
                yield return method;
            }

            foreach (var method in generateConstructorMethods(type))
            {
                yield return method;
            }
        }

        /// <summary>
        /// Generate constructor methods for given type
        /// <remarks>Only instance constructors are generated (because static constructors cannot be wrapped)</remarks>.
        /// </summary>
        /// <param name="type">Type which constructors will be generated.</param>
        /// <returns>Generated constructor methods.</returns>
        private IEnumerable<RuntimeMethodGenerator> generateConstructorMethods(Type type)
        {
            var wrappedType = getWrappedType(type);

            foreach (var ctor in wrappedType.GetConstructors())
            {
                if (!areParamsInDirectCover(ctor))
                    continue;

                var directMethod = generateDirectCtor(ctor);
                var paramsInfo = getParametersInfo(ctor);

                var returnInfo = TypeDescriptor.Void;
                var info = new TypeMethodInfo(
                    TypeInfo, Naming.CtorName,
                    returnInfo, paramsInfo,
                    false, TypeDescriptor.NoDescriptors);
                yield return new RuntimeMethodGenerator(directMethod, info, new Type[0]);
            }
        }

        /// <summary>
        /// Generate public static/instance methods for given type.
        /// </summary>
        /// <param name="type">Type which methods will be generated.</param>
        /// <returns>Generated methods.</returns>
        private IEnumerable<RuntimeMethodGenerator> generatePublicMethods(Type type)
        {
            var wrappedType = WrappedDirectType;
            var implementedTypesMap = createImplementedTypesMap(wrappedType);

            var wrappedMethods = getPublicMethods(wrappedType);

            for (var i = 0; i < wrappedMethods.Length; ++i)
            {
                var wrappedMethod = wrappedMethods[i];

                if (!isInDirectCover(wrappedMethod) || wrappedMethod.CallingConvention == CallingConventions.VarArgs)
                    continue;

                var implementedTypes = implementedTypesMap.Get(wrappedMethod);

                var builder = new MethodBuilder(this, wrappedMethod.Name, false);
                if (wrappedMethod.IsStatic)
                {
                    builder.ThisObjectExpression = null;
                }
                else
                {
                    builder.ThisObjectExpression = builder.ArgumentInstanceExpression(0);
                }

                builder.ImplementedTypes.UnionWith(implementedTypes);
                builder.AdapterFor(wrappedMethod);
                yield return builder.Build();
            }
        }

        /// <summary>
        /// Gets the public methods.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>MethodInfo[].</returns>
        private static MethodInfo[] getPublicMethods(Type type)
        {
            if (!type.IsInterface)
                //inherited methods are included
                return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            //for interfaces we have to include all children interfaces
            var ifaces = type.GetInterfaces().Union(new[] { type });
            var methods = new List<MethodInfo>();

            foreach (var iface in ifaces)
            {
                var ifaceMethods = iface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                methods.AddRange(ifaceMethods);
            }
            return methods.ToArray();
        }

        /// <summary>
        /// Gets wrapped form of given type. Wrapping is
        /// done for generic definitions so their generic parameters
        /// are replaced by <see cref="InstanceWrap"/>.
        /// </summary>
        /// <param name="type">Type to be wrapped.</param>
        /// <returns>Wrapped type.</returns>
        private static Type getWrappedType(Type type)
        {
            if (type.IsGenericTypeDefinition)
            {
                var argumentsCount = type.GetGenericArguments().Length;
                var wrappedArguments = Enumerable.Repeat(typeof(InstanceWrap), argumentsCount).ToArray();

                return type.MakeGenericType(wrappedArguments);
            }
            else
            {
                return type;
            }
        }

        /// <summary>
        /// Create mapping of method info to types that are implemented by info.
        /// </summary>
        /// <param name="type">Type which mapping is created.</param>
        /// <returns>Created mapping.</returns>
        private static MultiDictionary<MethodInfo, Type> createImplementedTypesMap(Type type)
        {
            //Get method mapping
            var implementedTypesMap = new MultiDictionary<MethodInfo, Type>();
            if (!type.IsInterface)
            {
                foreach (var implementedInterface in type.GetInterfaces())
                {
                    var map = type.GetInterfaceMap(implementedInterface);
                    foreach (var method in map.TargetMethods)
                    {
                        implementedTypesMap.Add(method, map.InterfaceType);
                    }
                }
            }
            return implementedTypesMap;
        }

        /// <summary>
        /// Get parameters info for given method base.
        /// </summary>
        /// <param name="method">Base method which parameters will be created.</param>
        /// <returns>Created parameters info.</returns>
        private static ParameterTypeInfo[] getParametersInfo(MethodBase method)
        {
            var paramsInfo = new List<ParameterTypeInfo>();
            foreach (var param in method.GetParameters())
            {
                var paramInfo = ParameterTypeInfo.From(param);
                paramsInfo.Add(paramInfo);
            }
            return paramsInfo.ToArray();
        }

        /// <summary>
        /// Generate direct method for given constructor.
        /// </summary>
        /// <param name="constructor">Constructor which method is generated.</param>
        /// <returns>Generated method.</returns>
        private DirectMethod generateDirectCtor(ConstructorInfo constructor)
        {
            var contextType = typeof(AnalyzingContext);
            var contextParameter = Expression.Parameter(contextType, "context");
            var inputParameters = new ParameterExpression[] { contextParameter };

            var args = getArgumentExpressions(constructor, contextParameter);
            var thisInstance = getArgumentInstance(0, contextParameter);
            var constructed = Expression.New(constructor, args);

            var ctorMethod = Expression.Call(contextParameter, contextType.GetMethod("Initialize"), thisInstance, constructed);
            return Expression.Lambda<DirectMethod>(ctorMethod, inputParameters).Compile();
        }

        /// <summary>
        /// Get argument expressions for given method. Argument expression get value from context.CurrentArguments.
        /// </summary>
        /// <param name="method">Method which arguments.</param>
        /// <param name="contextParameter">Parameter with context object.</param>
        /// <returns>Argument expressions.</returns>
        private IEnumerable<Expression> getArgumentExpressions(MethodBase method, ParameterExpression contextParameter)
        {
            var parameters = method.GetParameters();
            var argumentExpressions = new List<Expression>();
            for (int i = 0; i < parameters.Length; ++i)
            {
                var parameter = parameters[i];
                var argument = getArgument(i + 1, parameter.ParameterType, contextParameter);
                argumentExpressions.Add(argument);
            }
            return argumentExpressions;
        }

        /// <summary>
        /// Get argument instance according to index
        /// <remarks>No conversions nor wrapping is made</remarks>.
        /// </summary>
        /// <param name="index">Zero based index of arguments - zero arguments belongs to this instance.</param>
        /// <param name="contextParameter">Parameter with context object.</param>
        /// <returns>Argument instance.</returns>
        private Expression getArgumentInstance(int index, ParameterExpression contextParameter)
        {
            var contextType = typeof(AnalyzingContext);
            var argsArray = Expression.Property(contextParameter, contextType.GetProperty("CurrentArguments"));
            return Expression.ArrayAccess(argsArray, Expression.Constant(index));
        }

        /// <summary>
        /// Get argument expression according to index.
        /// </summary>
        /// <param name="index">Zero based index of arguments - zero arguments belongs to this instance.</param>
        /// <param name="resultType">Expected type of result - wrapping, direct value obtaining is processed.</param>
        /// <param name="contextParameter">Parameter with context object.</param>
        /// <returns>Argument expression.</returns>
        private Expression getArgument(int index, Type resultType, ParameterExpression contextParameter)
        {
            var argumentInstance = getArgumentInstance(index, contextParameter);
            var instanceWrapType = typeof(InstanceWrap);

            if (resultType == instanceWrapType)
            {
                //wrapp as InstanceWrap
                var ctor = instanceWrapType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).First();
                return Expression.New(ctor, new Expression[] { argumentInstance });
            }
            else
            {
                //unwrap to direct instance
                return Expression.Convert(Expression.PropertyOrField(argumentInstance, "DirectValue"), resultType);
            }
        }

        #endregion

        #region Direct type services

        /// <summary>
        /// Determine that method is in direct cover.
        /// </summary>
        /// <param name="method">Tested method.</param>
        /// <returns>True if method is in direct cover, false otherwise.</returns>
        private bool isInDirectCover(MethodInfo method)
        {
            return areParamsInDirectCover(method) && isInDirectCover(method.ReturnType);
        }

        /// <summary>
        /// Determine that parameters of method are in direct cover.
        /// </summary>
        /// <param name="method">Method whic parameters will be tested.</param>
        /// <returns>True if method parameters are in direct cover, false otherwise.</returns>
        private bool areParamsInDirectCover(MethodBase method)
        {
            foreach (var parameter in method.GetParameters())
            {
                if (!isInDirectCover(parameter.ParameterType))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determine that type is in direct cover.
        /// </summary>
        /// <param name="type">Tested type.</param>
        /// <returns>True if type is in direct cover, false otherwise.</returns>
        private bool isInDirectCover(Type type)
        {
            return ContainingAssembly.IsInDirectCover(type);
        }

        #endregion
    }
}
