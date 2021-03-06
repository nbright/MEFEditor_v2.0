﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Cecil;

using MEFEditor.Analyzing;
using MEFEditor.TypeSystem;

namespace RecommendedExtensions.Core.AssemblyProviders.CILAssembly
{
    class TypeModuleIterator : SearchIterator
    {
        readonly string _currentPath;

        readonly CILAssembly _assembly;

        internal TypeModuleIterator(CILAssembly assembly)
        {
            _assembly = assembly;
        }

        private TypeModuleIterator(string currentPath, CILAssembly assembly)
        {
            _currentPath = currentPath;
            _assembly = assembly;
        }

        public override SearchIterator ExtendName(string suffix)
        {
            string extendedPath = null;
            if (_currentPath == null || _currentPath == "")
            {
                extendedPath = suffix;
            }
            else
            {
                extendedPath = _currentPath + "." + suffix;
            }

            if (!_assembly.MayInclude(extendedPath))
                //namespace is incompatible
                return null;

            return new TypeModuleIterator(extendedPath, _assembly);
        }

        public override IEnumerable<TypeMethodInfo> FindMethods(string searchedName)
        {
            //TODO search generics properly

            var typeFullName = _currentPath;
            if (typeFullName == "" || typeFullName == null)
                return null;

            var methods = _assembly.GetMethods(typeFullName, searchedName);
            var methodInfos = from method in methods select method.Info;

            return methodInfos;
        }

        public override string ToString()
        {
            var pathDescriptor = _currentPath == null ? "$root" : _currentPath;
            return "[Iterator]" + _assembly.Name + "|" + pathDescriptor;
        }
    }
}
