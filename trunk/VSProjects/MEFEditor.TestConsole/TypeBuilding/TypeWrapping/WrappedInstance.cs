﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TypeExperiments.Core;
namespace TypeExperiments.TypeBuilding.TypeWrapping
{
    class WrappedObject<T>:Instance
    {
        /// <summary>
        /// Instance is here just data holder
        /// </summary>
        internal T WrappedData { get; set; }

        public WrappedObject(InternalType type):base(type)
        {
        }

        
    }
}
