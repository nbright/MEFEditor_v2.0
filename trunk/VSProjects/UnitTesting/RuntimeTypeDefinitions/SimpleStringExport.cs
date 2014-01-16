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
    public class SimpleStringExport : DataTypeDefinition
    {
        protected Field<string> Export;

        public SimpleStringExport()
        {
            FullName = "SimpleStringExport";

            var builder = new ComponentInfoBuilder(GetTypeInfo());
            builder.AddExport(TypeDescriptor.Create<string>(), "Export");
            ComponentInfo = builder.BuildInfo();
        }

        public void _method_ctor()
        {
            Export.Set("SimpleExportValue");
        }

        public string _get_Export()
        {
            return Export.Get();
        }

        protected override void draw(InstanceDrawer services)
        {
            services.PublishField("Export", Export);
            services.ForceShow();
        }
    }
}