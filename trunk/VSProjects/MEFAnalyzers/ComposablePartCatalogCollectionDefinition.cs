﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;

using Analyzing;
using Analyzing.Editing;
using TypeSystem;
using TypeSystem.Runtime;

using Drawing;

namespace MEFAnalyzers
{
    public class ComposablePartCatalogCollectionDefinition : DataTypeDefinition
    {
        public const string TypeFullname = "System.ComponentModel.Composition.Hosting";

        protected Field<Instance> Parent;

        protected Field<List<Instance>> Catalogs;

        public ComposablePartCatalogCollectionDefinition()
        {
            FullName = TypeFullname;
        }

        public void _method_ctor(Instance parent)
        {
            Catalogs.Set(new List<Instance>());
            Parent.Set(parent);
        }

        public void _method_Add(Instance partCatalog)
        {
            Catalogs.Get().Add(partCatalog);
            ReportChildAdd(Parent.Get(), 1, "Part catalog");
        }
    }
}