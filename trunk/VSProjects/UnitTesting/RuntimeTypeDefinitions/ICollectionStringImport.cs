﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;
using TypeSystem;
using TypeSystem.Runtime;

namespace UnitTesting.RuntimeTypeDefinitions
{
    public class ICollectionStringImport : DataTypeDefinition
    {
        public readonly Field<List<string>> Import;

        public ICollectionStringImport()
        {
            Import = new Field<List<string>>(this);
            FullName = "ICollectionStringImport";

            var builder = new ComponentInfoBuilder(GetTypeInfo());
            builder.AddManyImport(InstanceInfo.Create<List<string>>(), InstanceInfo.Create<string>(), "Import");
            ComponentInfo = builder.BuildInfo();
        }

        public void _method_ctor()
        {
            var data = new List<string>();
            //   data.Add("Uninitialized import");
            Import.Set(new List<string>(data));
        }

        public void _set_Import(List<string> import)
        {
            Import.Set(import);
        }

        public List<string> _get_Import()
        {
            return Import.Get();
        }
    }
}