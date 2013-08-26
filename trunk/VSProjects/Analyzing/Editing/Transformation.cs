﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzing.Editing
{
    public abstract class Transformation
    {
        protected abstract void apply(TransformationServices services);

        public void Apply(TransformationServices services)
        {
            apply(services);
        }
    }
}