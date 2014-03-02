﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;
using Analyzing.Execution;

using TypeSystem.Runtime;

namespace TypeSystem
{
    public delegate void OnInstanceCreated(Instance instance);

    public class MachineSettings : IMachineSettings
    {
        public readonly RuntimeAssembly Runtime = new RuntimeAssembly();

        public InstanceInfo GetNativeInfo(Type literalType)
        {
            if (literalType.IsAssignableFrom(typeof(Array<InstanceWrap>)))
            {
                return TypeDescriptor.ArrayInfo;
            }

            return TypeDescriptor.Create(literalType);
        }

        public InstanceInfo GetSharedInstanceInfo(string typeFullname)
        {
            return TypeDescriptor.Create(typeFullname);
        }

        public bool IsTrue(Instance condition)
        {
            var dirVal = condition.DirectValue;
            if (dirVal is bool)
            {
                return (bool)dirVal;
            }
            else if (dirVal is int)
            {
                return (int)dirVal != 0;
            }

            return false;
        }

        public MethodID GetSharedInitializer(InstanceInfo sharedInstanceInfo)
        {
            if (IsDirect(sharedInstanceInfo))
                //direct types doesn't have static initializers 
                //TODO this could be potentionall inconsitency drawback
                return null;

            return Naming.Method(sharedInstanceInfo, Naming.ClassCtorName, false, new ParameterTypeInfo[] { });
        }

        public bool IsDirect(InstanceInfo typeInfo)
        {
            return Runtime.IsDirectType(typeInfo);
        }
    }
}
