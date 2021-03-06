﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Cecil;

using MEFEditor.TypeSystem;
using MEFEditor.TypeSystem.TypeParsing;

namespace RecommendedExtensions.Core.Languages.CIL
{
    /// <summary>
    /// Helper for building TypeDescriptor from TypeReferences
    /// </summary>
    class TypeReferenceHelper
    {
        /// <summary>
        /// According parameter substitutions available for building
        /// </summary>
        internal readonly Dictionary<GenericParameter, TypeDescriptor> Substitutions = new Dictionary<GenericParameter, TypeDescriptor>();

        /// <summary>
        /// Build TypeDescriptor from given TypeReference. Available substitutions are used.
        /// </summary>
        /// <param name="type">TypeReference which descriptor is builded</param>
        /// <returns>Built TypeDescriptor if available, <c>null</c> otherwise</returns>
        internal TypeDescriptor BuildDescriptor(TypeReference type)
        {
            if (type == null)
                return null;

            var adapted = new TypeReferenceAdapter(type);
            return TypeHierarchyDirector.BuildDescriptor(adapted, resolveSubstitution);
        }

        /// <summary>
        /// Resolve substitution for generic parameter
        /// </summary>
        /// <param name="parameterAdapter">Generic parameter which substitution will be retrieved</param>
        /// <returns>Resolved substitution</returns>
        private TypeDescriptor resolveSubstitution(TypeAdapterBase parameterAdapter)
        {
            var adapted = parameterAdapter as TypeAdapterBase<TypeReference>;
            var adaptedReference = adapted.AdaptedType as GenericParameter;

            TypeDescriptor result;
            if (!Substitutions.TryGetValue(adaptedReference, out result))
            {
                result = TypeDescriptor.GetParameter(Substitutions.Count); //TODO determine correct ordering
                Substitutions[adaptedReference] = result;
            }

            return result;
        }
    }
}
