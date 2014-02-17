﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;
using System.ComponentModel.Composition;

using Analyzing;

namespace TypeSystem
{
    /// <summary>
    /// Definition of naming conventions used by TypeSystem. Provide MethodID creation methods
    /// which using is required for compatibility with TypeSystem.
    /// 
    /// MethodID format:
    ///     MethodName: Name &lt generic arguments &gt
    ///     MethodPath: TypeFullname {PathDelimiter} MethodName
    ///     MethodIdentifier: MethodPath {PartDelimiter} ParametersDescription
    /// </summary>
    public static class Naming
    {
        /// <summary>
        /// Fullname of ExportAttributeType
        /// </summary>
        public static readonly string ExportAttribute = typeof(ExportAttribute).FullName;

        /// <summary>
        /// Fullname of ImportAttributeType
        /// </summary>
        public static readonly string ImportAttribute = typeof(ImportAttribute).FullName;

        /// <summary>
        /// Fullname of ImportAttributeType
        /// </summary>
        public static readonly string ImportManyAttribute = typeof(ImportManyAttribute).FullName;
        
        /// <summary>
        /// Fullname of CompositionPointattribute
        /// </summary>
        public static readonly string CompositionPointAttribute = typeof(MEFEditor.CompositionPointAttribute).FullName;

        /// <summary>
        /// Delimiter of method identifier parts
        /// </summary>
        public const char PartDelimiter = ';';

        /// <summary>
        /// Delimiter of parts within method path
        /// </summary>
        public const char PathDelimiter = '.';

        /// <summary>
        /// Name of constructor method
        /// </summary>
        public const string CtorName = "#ctor";

        /// <summary>
        /// Name of static (class) constructor method
        /// </summary>
        public const string ClassCtorName = "#cctor";

        /// <summary>
        /// Prefix of getter methods
        /// </summary>
        public const string GetterPrefix = "get_";

        /// <summary>
        /// Prefix of setter methods
        /// </summary>
        public const string SetterPrefix = "set_";

        /// <summary>
        /// Creates non-generic, non-dynamic MethodID from given name and parameters
        /// </summary>
        /// <typeparam name="DefiningType">Type where method is defined</typeparam>
        /// <param name="methodName">Name of method</param>
        /// <param name="parameters">Parameters' types</param>
        /// <returns>Created MethodID</returns>
        public static MethodID Method<DefiningType>(string methodName, params Type[] parameters)
        {
            var path = typeof(DefiningType).FullName + "." + methodName;
            return method(path, paramDescription(parameters), false);
        }

        /// <summary>
        /// Creates non-generic MethodID from given name and parameters, defined on declaringType
        /// </summary>
        /// <param name="declaringType">Type where method is declared</param>
        /// <param name="methodName">Name of method</param>
        /// <param name="needsDynamicResolution">Determine that method needs dynamic resolution</param>
        /// <param name="parameters">Parameters' types</param>
        /// <returns>Created MethodID</returns>
        public static MethodID Method(InstanceInfo declaringType, string methodName, bool needsDynamicResolution, params ParameterTypeInfo[] parameters)
        {
            var path = declaringType.TypeName + "." + methodName;

            return method(path, paramDescription(parameters), needsDynamicResolution);
        }

        /// <summary>
        /// Creates generic MethodID from given name, parameters and methodTypeArguments
        /// </summary>
        /// <param name="declaringType">Type where method is declared</param>
        /// <param name="methodName">Name of method</param>
        /// <param name="needsDynamicResolution">Determine that method needs dynamic resolution</param>
        /// <param name="parameters">Parameters' types</param>
        /// <param name="methodTypeArguments">Type arguments of method</param>
        /// <returns>Created MethodID</returns>
        public static MethodID GenericMethod(InstanceInfo declaringType, string methodName, bool needsDynamicResolution, TypeDescriptor[] methodTypeArguments, params ParameterTypeInfo[] parameters)
        {
            var typeNames = from argument in methodTypeArguments select argument.TypeName;
            var genericMethodName = methodName + "<" + string.Join(",", typeNames.ToArray()) + ">";

            var useGenericMethodName = methodTypeArguments.Length > 0;
            methodName = useGenericMethodName ? genericMethodName : methodName;

            return Method(declaringType, methodName, needsDynamicResolution, parameters);
        }

        #region Method name operations

        /// <summary>
        /// Get name of method
        /// </summary>
        /// <param name="method">Method which name is obtained</param>
        /// <returns>Method name</returns>
        public static string GetMethodName(MethodID method)
        {
            if (method == null)
                return null;

            string path, description;
            GetParts(method, out path, out description);

            return GetMethodName(path);
        }

        /// <summary>
        /// Get name of method from method path
        /// </summary>
        /// <param name="methodPath">Path of method</param>
        /// <returns>Name of method</returns>
        public static string GetMethodName(string methodPath)
        {
            if (methodPath == null)
                return null;

            var nameStart = GetLastNonNestedPathDelimiterIndex(methodPath);
            if (nameStart < 0)
                return null;

            return methodPath.Substring(nameStart + 1);
        }

        /// <summary>
        /// Get index of last path delimiter within given method path
        /// </summary>
        /// <param name="methodPath">Method path where delimiter is searche</param>
        /// <returns>Index of path delimiter if found, -1 otherwise</returns>
        public static int GetLastNonNestedPathDelimiterIndex(string methodPath)
        {
            var nesting = 0;
            for (var i = methodPath.Length - 1; i > 0; --i)
            {
                var ch = methodPath[i];
                switch (ch)
                {
                    //note that we are walking backward
                    case '<':
                        --nesting;
                        continue;

                    case '>':
                        ++nesting;
                        continue;

                    case PathDelimiter:
                        if (nesting == 0)
                        {
                            //zero nesting and last delimiter
                            return i;
                        }

                        continue;
                }
            }

            return -1;
        }

        /// <summary>
        /// Get parsed method identifier parts
        /// </summary>
        /// <param name="method">Parsed method</param>
        /// <param name="path">Method path part output</param>
        /// <param name="paramDescription">Parameter description part output</param>
        public static void GetParts(MethodID method, out string path, out string paramDescription)
        {
            var parts = method.MethodString.Split(new char[] { PartDelimiter }, 2);

            path = parts[0];
            paramDescription = parts[1];
        }

        #endregion

        #region Declaring type operations

        /// <summary>
        /// Parse out declaring type fullname from given method
        /// </summary>
        /// <param name="method">Method from where type is parsed out</param>
        /// <returns>Declaring type fullname</returns>
        public static string GetDeclaringType(MethodID method)
        {
            if (method == null)
                return null;

            string path, description;
            GetParts(method, out path, out description);

            return GetDeclaringType(path);
        }

        /// <summary>
        /// Parse out declaring type fullname from given method path
        /// </summary>
        /// <param name="methodPath">Method path from where type is parsed out</param>
        /// <returns>Declaring type fullname</returns>
        public static string GetDeclaringType(string methodPath)
        {
            if (methodPath == null)
                return null;

            var nameStart = GetLastNonNestedPathDelimiterIndex(methodPath);
            if (nameStart < 0)
                return null;

            return methodPath.Substring(0, nameStart);
        }

        /// <summary>
        /// Parse out method path from given method
        /// </summary>
        /// <param name="method">MethodID from where path is parsed out</param>
        /// <returns>Method path info</returns>
        public static PathInfo GetMethodPath(MethodID method)
        {
            string path, paramDescr;
            Naming.GetParts(method, out path, out paramDescr);

            return new PathInfo(path);
        }

        /// <summary>
        /// Create MethodID from given changedMethod that has declaring type defined by typeName
        /// </summary>
        /// <param name="typeName">Type that will be declaring type of resulting MethodID</param>
        /// <param name="changedMethod">Method which declaring type will be changed</param>
        /// <param name="needsDynamicResolution">Determine that method needs dynamic resolution</param>
        /// <returns>Created MethodID</returns>
        public static MethodID ChangeDeclaringType(string typeName, MethodID changedMethod, bool needsDynamicResolution)
        {
            string path, description;
            GetParts(changedMethod, out path, out description);

            //TODO when description will contain parameter types, generic translation is needed
            var methodName = GetMethodName(path);
            return method(typeName + "." + methodName, description, needsDynamicResolution);
        }

        #endregion

        #region Private utilities


        /// <summary>
        /// Creates non-generic MethodID from given methodPath and paramDescription
        /// </summary>
        /// <param name="methodPath">Path of method</param>
        /// <param name="needsDynamicResolution">Determine that method needs dynamic resolution</param>
        /// <param name="paramDescription">Description of method parameters</param>
        /// <returns>Created MethodID</returns>
        private static MethodID method(string methodPath, string paramDescription, bool needsDynamicResolution)
        {
            var identifier = methodPath + PartDelimiter + paramDescription;
            return new MethodID(identifier, needsDynamicResolution);
        }

        /// <summary>
        /// Create description of parameters
        /// </summary>
        /// <param name="parameters">Parameters which description is needed</param>
        /// <returns>Description of parameters</returns>
        private static string paramDescription(params object[] parameters)
        {
            var parCount = parameters == null ? 0 : parameters.Length;
            return parCount.ToString();
        }

        #endregion
    }
}
