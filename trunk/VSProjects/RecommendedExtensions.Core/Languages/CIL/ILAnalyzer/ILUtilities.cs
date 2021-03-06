﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace RecommendedExtensions.Core.Languages.CIL.ILAnalyzer
{
    /// <summary>
    /// Taken from answer at: http://stackoverflow.com/questions/14243284/how-can-i-retrieve-string-literals-using-reflection.
    /// </summary>
    public static class ILUtilities
    {
        /// <summary>
        /// Prints the specified method.
        /// </summary>
        /// <param name="method">The method.</param>
        public static void Print(MethodInfo method)
        {
            var reader = new ILReader(method);
            Console.WriteLine(method);
            foreach (var instr in reader.Instructions)
            {
                Console.WriteLine(instr);
            }
        }
    }
}
