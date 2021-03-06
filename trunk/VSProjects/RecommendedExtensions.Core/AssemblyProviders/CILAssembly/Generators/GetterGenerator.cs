﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Execution;

namespace RecommendedExtensions.Core.AssemblyProviders.CILAssembly.Generators
{
    class GetterGenerator : GeneratorBase
    {
        private readonly string _fieldName;

        public GetterGenerator(string fieldName)
        {
            _fieldName = fieldName;
        }

        protected override void generate(EmitterBase emitter)
        {
            emitter.DirectInvoke(get);
        }

        private void get(AnalyzingContext context)
        {
            var This = context.CurrentArguments[0];

            Instance fieldValue;
            if (This is DirectInstance)
            {
                fieldValue = null;
            }
            else
            {
                fieldValue = context.GetField(This, _fieldName) as Instance;
            }

            context.Return(fieldValue);
        }
    }
}
