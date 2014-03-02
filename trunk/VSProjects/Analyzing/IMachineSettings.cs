﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing.Execution;

namespace Analyzing
{
    public delegate void DirectMethod(AnalyzingContext context);


    public interface IMachineSettings
    {
        bool IsDirect(InstanceInfo typeInfo);

        InstanceInfo GetNativeInfo(Type literalType);

        bool IsTrue(Instance condition);

        MethodID GetSharedInitializer(InstanceInfo sharedInstanceInfo);

    }
}
