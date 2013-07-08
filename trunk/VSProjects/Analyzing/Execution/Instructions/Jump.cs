﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzing.Execution.Instructions
{
    class Jump<MethodID, InstanceInfo> : IInstruction<MethodID, InstanceInfo>
    {        
        private readonly Label _target;

        internal Jump(Label target)
        {
            _target = target;
        }

        public void Execute(AnalyzingContext<MethodID, InstanceInfo> context)
        {
            context.Jump(_target);
        }
    }
}
