﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;

namespace TypeSystem.Core
{
    class AssembliesManager
    {
        readonly AssemblyCollection _assemblies;

        readonly TypeServices _services;

        internal AssembliesManager(AssemblyCollection assemblies)
        {
            _services = new TypeServices(this);

            _assemblies = assemblies;
            _assemblies.OnAdd += onAssemblyAdd;
            _assemblies.OnRemove += onAssemblyRemove;

            foreach (var assembly in _assemblies)
            {
                onAssemblyAdd(assembly);
            }
        }

        internal bool TryResolveMethod(MethodID method, InstanceInfo[] staticArgumentInfo,out VersionedName name)
        {
            foreach (var assembly in _assemblies)
            {
                var methodName = assembly.ResolveMethod(method,staticArgumentInfo);
                if (methodName != null)
                {
                    name=createVersionedName(methodName);
                    bindName(name, assembly);
                    return true;
                }
            }

            name=default(VersionedName);
            return false;
        }
        
        internal IInstructionGenerator<MethodID, InstanceInfo> GetGenerator(VersionedName methodName)
        {
            //TODO: Ask binded provider for generator or get cached one

            foreach (var assembly in _assemblies)
            {
                var generator = assembly.GetGenerator(methodName);
                if (generator != null)
                {
                    return generator;
                }
            }

            throw new NotSupportedException("Invalid method name");
        }

        private void bindName(VersionedName name, AssemblyProvider provider)
        {
            //throw new NotImplementedException("When name is found, we remember provider of the name");
        }

        private VersionedName createVersionedName(string methodName)
        {
            //todo  
            return new VersionedName(methodName, 42);
        }

        private void onAssemblyAdd(AssemblyProvider assembly)
        {
            assembly.SetServices(_services);
        }

        private void onAssemblyRemove(AssemblyProvider assembly)
        {
            assembly.UnloadServices();
        }

        internal MethodSearcher CreateSearcher()
        {
            return new MethodSearcher(_assemblies);
        }
    }
}