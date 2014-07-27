﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MEFEditor.Analyzing.Editing;

namespace MEFEditor.Analyzing.Execution.Instructions
{
    abstract class AssignBase : InstructionBase
    {
        internal RemoveTransformProvider RemoveProvider { get; set; }
    }
}
