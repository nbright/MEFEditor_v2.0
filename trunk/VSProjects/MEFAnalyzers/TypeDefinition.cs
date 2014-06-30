﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;

using Analyzing;
using Analyzing.Editing;
using TypeSystem;
using TypeSystem.Runtime;

using Drawing;

namespace MEFAnalyzers
{
    public class TypeDefinition : DataTypeDefinition
    {
        public TypeDefinition()
        {
            Simulate<Type>();
        }
                
        public void _static_method_cctor()
        {
            //nothing to do
        }
    }
}