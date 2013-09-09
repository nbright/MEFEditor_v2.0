﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnitTesting;
using UnitTesting.Analyzing_TestUtils;
using UnitTesting.TypeSystem_TestUtils;

using TypeSystem;

using Analyzing;
using Analyzing.Execution;
using Analyzing.Editing;

namespace TypeExperiments
{
    static class ResearchSources
    {
        public static readonly ParameterInfo SingleStringParamInfo = Parsing_Testing.SingleStringParamInfo;
        public static readonly ParameterInfo SingleIntParamInfo = Parsing_Testing.SingleIntParamInfo;

        static internal TestingAssembly InstanceRemoving()
        {
            return AssemblyUtils.Run(@"
                var toDelete=""toDelete"";                
                CallWithOptional(CallWithRequired(toDelete));           
                var x=1;
                var y=2;
                if(x<y){
                    toDelete=""same"";
                }     else{
                    toDelete=""different"";
                }
            ")

         .AddMethod("Test.CallWithOptional", (c) =>
         {
             var arg = c.CurrentArguments[1];
             c.Edits.SetOptional(1);
             c.Return(arg);
         }, "System.String", new ParameterInfo("p", InstanceInfo.Create<string>()))

         .AddMethod("Test.CallWithRequired", (c) =>
         {
             var arg = c.CurrentArguments[1];
             c.Return(arg);
         }, "System.String", new ParameterInfo("p", InstanceInfo.Create<string>()))

         .AddRemoveAction("toDelete")

          ;

        }

        static internal TestingAssembly StaticCall()
        {
            return AssemblyUtils.Run(@"
                var test=StaticClass.StaticMethod(""CallArg"");
            ")

            .AddMethod("StaticClass.StaticMethod", (c) =>
            {
                var self = c.CurrentArguments[0];
                var arg = c.CurrentArguments[1].DirectValue as string;
                var field = c.GetField(self, "StaticField");

                var result = c.CreateDirectInstance(field + "_" + arg);
                c.Return(result);
            }
            , true, SingleStringParamInfo)

            .AddMethod("StaticClass.#initializer", (c) =>
            {
                var self = c.CurrentArguments[0];
                c.SetField(self, "StaticField", "InitValue");
            }
            , true)

             ;
        }

        static internal TestingAssembly EditProvider()
        {
            return AssemblyUtils.Run(@"
                var obj=new TestObj(""input"");
                
                var result = obj.GetInput();          
            ")

            .AddMethod("TestObj.TestObj", (c) =>
            {
                var thisObj = c.CurrentArguments[0];
                var arg = c.CurrentArguments[1];
                c.SetField(thisObj, "inputData", arg);

            }, "", new ParameterInfo("p", InstanceInfo.Create<string>()))

            .AddMethod("TestObj.GetInput", (c) =>
            {
                var thisObj = c.CurrentArguments[0];
                var data = c.GetField(thisObj, "inputData") as Instance;
                c.Return(data);
            }, false, new ParameterInfo("p", InstanceInfo.Create<string>()))


            ;
        }


        static object acceptInstance(EditsProvider edits, TransformationServices services)
        {
            var variable = edits.GetVariableFor(AssemblyUtils.EXTERNAL_INPUT, services);
            if (variable == null)
            {
                return services.Abort("Cannot get variable for instance");
            }

            return variable;
        }

        static internal TestingAssembly Fibonacci(int n)
        {
            return AssemblyUtils.Run(@"
var result=fib(" + n + @");

").AddMethod("fib", @"    
    if(n<3){
        return 1;
    }else{
        return fib(n-1)+fib(n-2);
    }
", returnType: "System.Int32", parameters: new ParameterInfo("n", InstanceInfo.Create<int>()));
        }
    }
}
