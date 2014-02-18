﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing;

namespace TypeSystem
{
    /// <summary>
    /// Builder helping with creating component info.
    /// </summary>
    public class ComponentInfoBuilder
    {
        /// <summary>
        /// Defined self exports
        /// </summary>
        private readonly List<Export> _selfExports = new List<Export>();

        /// <summary>
        /// Defined exports
        /// </summary>
        private readonly List<Export> _exports = new List<Export>();

        /// <summary>
        /// Defined imports
        /// </summary>
        private readonly List<Import> _imports = new List<Import>();

        /// <summary>
        /// Defined explicit composition points, which has been marked with composition point attribute
        /// </summary>
        private readonly List<CompositionPoint> _explicitCompositionPoints = new List<CompositionPoint>();

        /// <summary>
        /// Importing constructor if available
        /// </summary>
        private MethodID _importingCtor;

        /// <summary>
        /// Type of component that is builded
        /// </summary>
        public readonly TypeDescriptor ComponentType;

        /// <summary>
        /// Determine that no component info has been added yet
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return
                   _selfExports.Count == 0 &&
                   _exports.Count == 0 &&
                   _imports.Count == 0 &&
                   _explicitCompositionPoints.Count == 0
                   ;
            }
        }

        /// <summary>
        /// Create ComponentInfoBuilder
        /// </summary>
        /// <param name="componentType">Type of component that is builded</param>
        public ComponentInfoBuilder(TypeDescriptor componentType)
        {
            ComponentType = componentType;
        }

        #region Exports building

        /// <summary>
        /// Add export of given type
        /// </summary>
        /// <param name="exportType">Type of exported value</param>
        /// <param name="propertyName">Name of exporting property</param>
        /// <param name="contract">Contract of export</param>
        public void AddPropertyExport(TypeDescriptor exportType, string propertyName, string contract = null)
        {
            var getterID = Naming.Method(ComponentType, Naming.GetterPrefix + propertyName, false, new ParameterTypeInfo[0]);
            AddExport(exportType, getterID, contract);
        }

        /// <summary>
        /// Add export of given type
        /// </summary>
        /// <param name="exportType">Type of exported value</param>
        /// <param name="getterID">Id of exporting method</param>
        /// <param name="contract">Contract of export</param>
        public void AddExport(TypeDescriptor exportType, MethodID getterID, string contract = null)
        {
            if (contract == null)
            {
                contract = exportType.TypeName;
            }

            var export = new Export(exportType, getterID, contract);
            AddExport(export);
        }

        /// <summary>
        /// Add given export to component info
        /// </summary>
        /// <param name="export">Added export</param>
        public void AddExport(Export export)
        {
            _exports.Add(export);
        }

        /// <summary>
        /// Add self export of component
        /// </summary>
        /// <param name="contract">Contract of export</param>
        public void AddSelfExport(string contract)
        {
            var export = new Export(ComponentType, null, contract);

            _selfExports.Add(export);
        }

        #endregion

        #region Imports building

        /// <summary>
        /// Add import of given type
        /// </summary>
        /// <param name="importType">Imported type</param>
        /// <param name="setterName">Name of property</param>
        public void AddPropertyImport(TypeDescriptor importType, string setterName)
        {
            var setterID = getSetterID(importType, setterName);
            var importTypeInfo = ImportTypeInfo.Parse(importType);
            var contract = importTypeInfo.ItemType.TypeName;

            AddImport(importTypeInfo, setterID, contract);
        }

        /// <summary>
        /// Add import of given type
        /// </summary>
        /// <param name="importTypeInfo">Info about imported type</param>
        /// <param name="setterID">Importing setter</param>
        /// <param name="contract">Contract of import</param>
        /// <param name="allowMany">Determine many values can be imported</param>
        public void AddImport(ImportTypeInfo importTypeInfo, MethodID setterID, string contract = null, bool allowMany = false, bool allowDefault = false)
        {
            if (contract == null)
            {
                contract = importTypeInfo.ItemType.TypeName;
            }

            var import = new Import(importTypeInfo, setterID, contract, allowMany, allowDefault);
            AddImport(import);
        }

        /// <summary>
        /// Add import of component
        /// </summary>
        /// <param name="import">Added import</param>
        public void AddImport(Import import)
        {
            _imports.Add(import);
        }

        /// <summary>
        /// Add many import of given type
        /// </summary>
        /// <param name="importTypeInfo">Info about imported type</param>
        /// <param name="setterID">Importing setter</param>
        /// <param name="contract">Contract of import</param>
        public void AddManyImport(TypeDescriptor importType, TypeDescriptor itemType, string setterName)
        {
            var setterID = getSetterID(importType, setterName);
            var importTypeInfo = ImportTypeInfo.ParseFromMany(importType, itemType);
            AddImport(importTypeInfo, setterID, null, true);
        }

        /// <summary>
        /// Set importing constructor of component
        /// </summary>
        /// <param name="importingParameters">Parameters of import</param>
        public void SetImportingCtor(params TypeDescriptor[] importingParameters)
        {
            var parameters = new ParameterTypeInfo[importingParameters.Length];

            for (int i = 0; i < parameters.Length; ++i)
            {
                var importType = importingParameters[i];
                parameters[i] = ParameterTypeInfo.Create("p", importType);

                var importTypeInfo = ImportTypeInfo.Parse(importType);
                var contract = importTypeInfo.ItemType.TypeName;
                var preImport = new Import(importTypeInfo, null, contract);
                _imports.Add(preImport);
            }

            _importingCtor = Naming.Method(ComponentType, Naming.CtorName, false, parameters);
        }
        #endregion

        /// <summary>
        /// Add explicit composition point of component.
        /// </summary>
        /// <param name="method">Composition point method</param>
        public void AddExplicitCompositionPoint(MethodID method)
        {
            _explicitCompositionPoints.Add(new CompositionPoint(ComponentType, method, true));
        }

        /// <summary>
        /// Build collected info into ComponentInfo
        /// </summary>
        /// <returns>Created ComponentInfo</returns>
        public ComponentInfo BuildInfo()
        {
            if (_importingCtor == null)
            {
                //default importin constructor
                _importingCtor = Naming.Method(ComponentType, Naming.CtorName, false);
            }

            var compositionPoints = new List<CompositionPoint>(_explicitCompositionPoints);

            return new ComponentInfo(ComponentType, _importingCtor, _imports.ToArray(), _exports.ToArray(), _selfExports.ToArray(), compositionPoints.ToArray());
        }

        #region Private helpers

        /// <summary>
        /// Get id for setter of given property
        /// </summary>
        /// <param name="propertyType">Type of property</param>
        /// <param name="property">Property which setter is needed</param>
        /// <returns>MethodID of desired property</returns>
        private MethodID getSetterID(TypeDescriptor propertyType, string property)
        {
            var parameters = new ParameterTypeInfo[] { ParameterTypeInfo.Create("value", propertyType) };
            var setterID = Naming.Method(ComponentType, Naming.SetterPrefix + property, false, parameters);
            return setterID;
        }

        #endregion
    }
}
