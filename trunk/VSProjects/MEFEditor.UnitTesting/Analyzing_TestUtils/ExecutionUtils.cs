﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Execution;

using MEFEditor.TypeSystem;

using MEFEditor.UnitTesting.TypeSystem_TestUtils;
using MEFEditor.UnitTesting.Analyzing_TestUtils.Environment;

namespace MEFEditor.UnitTesting.Analyzing_TestUtils
{

    /// <summary>
    /// Utility methods for <see cref="Machine"/> testing.
    /// </summary>
    public static class ExecutionUtils
    {
        /// <summary>
        /// Runs method generated by the specified director.
        /// </summary>
        /// <param name="director">The director that emits method's instructions.</param>
        /// <returns>Result of test analysis that can be tested by assertions.</returns>
        internal static TestResult Run(EmitDirector director)
        {
            var assembly = SettingsProvider.CreateTestingAssembly();

            var machine = SettingsProvider.CreateMachine(assembly.Settings);

            assembly.Runtime.BuildAssembly();
            var loader = new EmitDirectorLoader(director, assembly.Loader);
            return new TestResult(machine.Run(loader, loader.EntryPoint));
        }

        /// <summary>
        /// Asserts the variable so it can be tested by HasValue method.
        /// </summary>
        /// <param name="result">The result of analysis.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>TestCase.</returns>
        internal static TestCase AssertVariable(this TestResult result, string variable)
        {
            return new TestCase(result, variable);
        }

        /// <summary>
        /// Childs the contexts.
        /// </summary>
        /// <param name="callContext">The call context.</param>
        /// <returns>IEnumerable&lt;CallContext&gt;.</returns>
        public static IEnumerable<CallContext> ChildContexts(this CallContext callContext)
        {
            var block = callContext.EntryBlock;
            while (block != null)
            {
                foreach (var childContext in block.Calls)
                {
                    yield return childContext;
                }
                block = block.NextBlock;
            }
        }
    }

    /// <summary>
    /// Result of test case analysis that can be inspected by assertions.
    /// </summary>
    internal class TestCase
    {
        /// <summary>
        /// The result of test analysis.
        /// </summary>
        private readonly TestResult _result;
        /// <summary>
        /// The asserted variable.
        /// </summary>
        private readonly VariableName _variable;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestCase"/> class.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="variable">The asserted variable.</param>
        internal TestCase(TestResult result, string variable)
        {
            _result = result;
            _variable = new VariableName(variable);
        }

        /// <summary>
        /// Asserts the variable so it can be tested by HasValue method.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <returns>TestCase.</returns>
        internal TestCase AssertVariable(string variable)
        {
            return new TestCase(_result, variable);
        }

        /// <summary>
        /// Determines whether the asserted variable has expected value.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <returns>TestCase.</returns>
        internal TestCase HasValue(object expectedValue)
        {
            var entryContext = _result.Execution.EntryContext;
            var instance = _variable.Name == null ? _result.Execution.ReturnValue : entryContext.GetValue(_variable);

            var actualValue = instance.DirectValue;

            Assert.AreEqual(expectedValue, actualValue);
            return this;
        }
    }
}