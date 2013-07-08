﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;


using Analyzing.Execution.Instructions;
using UnitTesting.Analyzing_TestUtils;


namespace UnitTesting
{
    [TestClass]
    public class Machine_Testing
    {
        [TestMethod]
        public void Assign()
        {
            ExecutionUtils.Run((e) =>
            {
                e.AssignLiteral("var1", 1);
                e.Assign("var2", "var1");
                e.Assign("var1", "var1");
            })
            .AssertVariable("var1").HasValue(1)
            .AssertVariable("var2").HasValue(1);
        }

        [TestMethod]
        public void DirectCall()
        {
            ExecutionUtils.Run((e) =>
            {
                e.AssignLiteral("var1", "HELLO");
                e.Call("ToLower".Method(), "var1");
                e.AssignReturnValue("var2");
            })
            .AssertVariable("var2").HasValue("hello");
        }

        [TestMethod]
        public void DirectCall_withArguments()
        {
            ExecutionUtils.Run((e) =>
            {
                e.AssignLiteral("var1", 25);
                e.AssignLiteral("format", "Number: {0}");
                e.Call("ToString".Method(), "var1", "format");
                e.AssignReturnValue("var2");
            })
            .AssertVariable("var2").HasValue(25.ToString("Number: {0}"));
        }

        [TestMethod]
        public void InjectedMethodCall_static()
        {
            ExecutionUtils.Run((e) =>
            {
                e.AssignLiteral("var1", 40);
                e.AssignLiteral("var2", 2);
                e.StaticCall("System.Int32", "+".Method(), "var1", "var2");
                e.AssignReturnValue("var3");
            })
            .AssertVariable("var3").HasValue(40 + 2);
        }

        [TestMethod]
        public void ConditionalLoop_iteration()
        {
            ExecutionUtils.Run((e) =>
            {
                var start = e.CreateLabel("start");
                var end = e.CreateLabel("end");

                e.AssignLiteral("stop", 100);
                e.AssignLiteral("step", 1);
                e.AssignLiteral("increment", 0);

                e.SetLabel(start);
                e.StaticCall("System.Int32", "+".Method(), "increment", "step");
                e.AssignReturnValue("increment");
                e.Call("Equals".Method(), "increment", "stop");
                e.AssignReturnValue("condition");
                e.ConditionalJump("condition", end);
                e.Jump(start);
                e.SetLabel(end);
            })
            .AssertVariable("increment").HasValue(100);
        }
    }
}
