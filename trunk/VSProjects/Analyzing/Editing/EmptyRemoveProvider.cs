﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzing.Editing
{
    class EmptyRemoveProvider:RemoveTransformProvider
    {
        public override Transformation Remove()
        {
            return new EmptyTransformation();
        }
    }
}