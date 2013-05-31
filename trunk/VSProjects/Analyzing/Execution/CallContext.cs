﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzing.Execution
{
    class CallContext
    {
        private Dictionary<VariableName, Instance> _variables = new Dictionary<VariableName, Instance>();
        private IInstruction[] _callInstructions;
        private uint _instructionPointer;

        /// <summary>
        /// Determine that call doesn't have next instructions to proceed
        /// </summary>
        internal bool IsCallEnd { get { return _instructionPointer >= _callInstructions.Length; } }
        /// <summary>
        /// Return value of call
        /// </summary>
        internal Instance ReturnValue { get; private set; }

        public CallContext(IInstructionGenerator generator, Instance[] argumentValues)
        {
            var emitter = new CallEmitter();

            generator.Generate(emitter);

            _callInstructions = emitter.GetEmittedInstructions();
            _instructionPointer = 0;
        }

        internal void SetValue(VariableName targetVaraiable, Instance value)
        {
            _variables[targetVaraiable] = value;
        }

        internal Instance GetValue(VariableName variable)
        {
            return _variables[variable];
        }

        internal IInstruction NextInstrution()
        {
            if (IsCallEnd)
            {
                return null;
            }

            return _callInstructions[_instructionPointer++];
        }
    }
}
