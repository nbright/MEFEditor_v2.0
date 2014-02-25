﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TypeSystem;

namespace AssemblyProviders.ProjectAssembly
{
    /// <summary>
    /// Activation describing method that could be parsed
    /// </summary>
    public class ParsingActivation
    {
        /// <summary>
        /// Source code of parsed method
        /// </summary>
        internal readonly string SourceCode;

        /// <summary>
        /// <see cref="TypeMethodInfo"/> describing generated method
        /// </summary>
        internal readonly TypeMethodInfo Method;

        /// <summary>
        /// Generic parameters of method path. They are used for translating source codes according to
        /// generic parameters.
        /// </summary>
        internal readonly IEnumerable<string> GenericParameters;


        public ParsingActivation(string sourceCode, TypeMethodInfo method, IEnumerable<string> genericParameters)
        {
            if (sourceCode == null)
                throw new ArgumentNullException("sourceCode");

            if (method == null)
                throw new ArgumentNullException("method");

            if (genericParameters == null)
                throw new ArgumentNullException("genericParameterrs");

            SourceCode = sourceCode;
            Method = method;

            //create defensive copy
            GenericParameters = genericParameters.ToArray();
        }
    }
}
