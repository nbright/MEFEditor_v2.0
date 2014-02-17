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
    public class StringImport : DataTypeDefinition
    {
        public readonly Field<string> Import;
        public readonly Field<string> PreImport;

        public StringImport()
        {
            FullName = "StringImport";

            var builder = new ComponentInfoBuilder(GetTypeInfo());
            builder.AddPropertyImport(TypeDescriptor.Create<string>(), "Import");
            builder.SetImportingCtor();

            ComponentInfo = builder.BuildInfo();
        }

        public void _method_ctor()
        {
            Import.Set("Uninitialized import");
        }

        public void _set_Import(string import)
        {
            Import.Set(import);
        }

        public string _get_Import()
        {
            return Import.Get();
        }

        protected override void draw(InstanceDrawer services)
        {
            services.PublishField("Import", Import);
            services.PublishField("PreImport", PreImport);
            services.ForceShow();
        }
    }
}
