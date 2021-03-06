﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;

using MEFEditor.Drawing;
using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Editing;
using MEFEditor.TypeSystem;
using MEFEditor.TypeSystem.Runtime;

using RecommendedExtensions.Core.TypeDefinitions.CompositionEngine;

namespace RecommendedExtensions.Core.TypeDefinitions
{
    /// <summary>
    /// Analyzing definition of <see cref="CompositionContainer" />.
    /// </summary>
    public class CompositionContainerDefinition : DataTypeDefinition
    {
        /// <summary>
        /// The contained composable catalog.
        /// </summary>
        protected Field<Instance> ComposableCatalog;

        /// <summary>
        /// The direct children.
        /// </summary>
        protected Field<List<Instance>> DirectChildren;

        /// <summary>
        /// The composition result.
        /// </summary>
        protected Field<CompositionResult> CompositionResult;

        /// <summary>
        /// The compose edit.
        /// </summary>
        protected Field<Edit> ComposeEdit;

        /// <summary>
        /// The compose by using batch edit.
        /// </summary>
        protected Field<Edit> ComposeBatchEdit;

        /// <summary>
        /// Determine that container was composed.
        /// </summary>
        protected Field<bool> WasComposed;

        /// <summary>
        /// Error occured during composition.
        /// </summary>
        protected Field<string> Error;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionContainerDefinition" /> class.
        /// </summary>
        public CompositionContainerDefinition()
        {
            Simulate<CompositionContainer>();
            AddCreationEdit("Add CompositionContainer");
        }

        /// <summary>
        /// Runtime member definition.
        /// </summary>
        /// <param name="composablePartCatalog">The composable part catalog.</param>
        /// <param name="exportProviders">The export providers.</param>
        [ParameterTypes(typeof(ComposablePartCatalog), typeof(ExportProvider[]))]
        public void _method_ctor(Instance composablePartCatalog, params Instance[] exportProviders)
        {
            _method_ctor();

            ReportChildAdd(1, "Composable part catalog", true);
            ComposableCatalog.Set(composablePartCatalog);
        }

        /// <summary>
        /// Runtime member definition.
        /// </summary>
        public void _method_ctor()
        {
            WasComposed.Value = false;
            DirectChildren.Value = new List<Instance>();
            initEdits(true);
        }

        /// <summary>
        /// Runtime member definition.
        /// </summary>
        /// <param name="constructedParts">The constructed parts.</param>
        public void _method_ComposeParts(params Instance[] constructedParts)
        {
            //there is already compose part call 
            Edits.Remove(ComposeEdit.Get());
            //so we will accept components to this call
            var e = Edits;
            AppendArg(constructedParts.Length + 1, UserInteraction.AcceptEditName, (v) => acceptAppendComponent(e, v));

            DirectChildren.Value.AddRange(constructedParts);
            for (int i = 0; i < constructedParts.Length; ++i)
            {
                ReportParamChildAdd(i, constructedParts[i], "Composed part", true);
            }

            composeWithCatalog(constructedParts);
        }

        /// <summary>
        /// Runtime member definition.
        /// </summary>
        /// <param name="part">The part.</param>
        public void _method_SatisfyImportsOnce(Instance part)
        {
            ReportChildAdd(1, "Satisfied component");
            DirectChildren.Value.Add(part);

            composeWithCatalog(new[] { part });
        }

        /// <summary>
        /// Composition batch restriction is needed.
        /// </summary>
        /// <param name="batch">The batch.</param>
        [ParameterTypes(typeof(CompositionBatch))]
        public void _method_Compose(Instance batch)
        {
            Edits.Remove(ComposeEdit.Get());

            ReportChildAdd(1, "Composition batch");
            DirectChildren.Value.Add(batch);

            AsyncCall<Instance[]>(batch, "get_PartsToRemove", (toRemove) =>
            {
                AsyncCall<Instance[]>(batch, "get_PartsToAdd", (toAdd) =>
                {
                    composeWithCatalog(toAdd, toRemove);
                });
            });
        }

        /// <summary>
        /// Process composition by using given catalog.
        /// </summary>
        /// <param name="constructedPartsToAdd">The constructed parts to add.</param>
        /// <param name="constructedPartsToRemove">The constructed parts to remove.</param>
        private void composeWithCatalog(Instance[] constructedPartsToAdd, Instance[] constructedPartsToRemove = null)
        {
            if (WasComposed.Value)
            {
                Error.Value = "CompositionContainer was composed multiple times.";
                return;
            }

            WasComposed.Value = true;

            var catalog = ComposableCatalog.Get();
            if (catalog == null)
            {
                //there is no catalog which parts can be retrieved
                processComposition(null, constructedPartsToAdd, constructedPartsToRemove);
            }
            else
            {
                //collect instances for composition
                AsyncCall<Instance[]>(catalog, "get_Parts", (catalogParts) =>
                {
                    processComposition(catalogParts, constructedPartsToAdd, constructedPartsToRemove);
                });
            }
        }

        /// <summary>
        /// Processes the composition.
        /// </summary>
        /// <param name="notConstructed">The not constructed instances.</param>
        /// <param name="constructed">The constructed instances.</param>
        /// <param name="toRemove">Instances to be removed from composition.</param>
        private void processComposition(IEnumerable<Instance> notConstructed, IEnumerable<Instance> constructed, IEnumerable<Instance> toRemove)
        {
            if (notConstructed == null)
                notConstructed = new Instance[0];

            if (constructed == null)
                constructed = new Instance[0];

            if (toRemove == null)
                toRemove = new Instance[0];

            var composition = new CompositionContext(CallingAssemblyServices, Context);

            notConstructed = notConstructed.Except(toRemove);
            constructed = constructed.Except(toRemove);

            //add components that needs importing constructor call
            composition.AddNotConstructedComponents(notConstructed);

            //add instances that doesn't need constructor call
            composition.AddConstructedComponents(constructed);

            //create composition
            var compositionResult = CompositionProvider.Compose(composition);
            CompositionResult.Set(compositionResult);

            if (!compositionResult.Failed)
            {
                //if composition building is OK then process composition
                Context.DynamicCall("$dynamic_composition", composition.Generator, composition.InputInstances);
            }
        }

        #region Container edits

        /// <summary>
        /// Initializes the edits.
        /// </summary>
        /// <param name="acceptCatalog">if set to <c>true</c> accepting catalog is included.</param>
        private void initEdits(bool acceptCatalog)
        {
            var e = Edits;
            if (acceptCatalog)
            {
                AppendArg(1, UserInteraction.AcceptEditName, (v) => acceptPartCatalog(e, v));
            }

            ComposeEdit.Set(
                AddCallEdit(UserInteraction.AcceptEditName, (v) => acceptInstance(v))
            );
        }

        /// <summary>
        /// Accepts the instance in given view.
        /// </summary>
        /// <param name="view">The view where instance will be accepted.</param>
        /// <returns>Accepting call.</returns>
        private CallEditInfo acceptInstance(ExecutionView view)
        {
            var toAccept = UserInteraction.DraggedInstance;

            if (Services.IsAssignable(CompositionBatchDefinition.Info, toAccept.Info))
            {
                return new CallEditInfo(This, "Compose", toAccept);
            }

            var componentInfo = Services.GetComponentInfo(toAccept.Info);

            if (componentInfo == null)
            {
                view.Abort("Can accept only components");
                return null;
            }

            return new CallEditInfo(TypeDescriptor.Create(typeof(AttributedModelServices)), "ComposeParts", true, This, toAccept);
        }

        /// <summary>
        /// Accepts the component by appending to ComposeParts call.
        /// </summary>
        /// <param name="e">The edits provider used for accepting.</param>
        /// <param name="view">The view where instance will be accepted.</param>
        /// <returns>Variable with accepted instance and correct scope.</returns>
        private object acceptAppendComponent(EditsProvider e, ExecutionView view)
        {
            var toAccept = UserInteraction.DraggedInstance;
            var componentInfo = Services.GetComponentInfo(toAccept.Info);

            if (componentInfo == null)
            {
                view.Abort("Can accept only components");
                return null;
            }
            return e.GetVariableFor(toAccept, view);
        }

        /// <summary>
        /// Accepts the part catalog in constructor.
        /// </summary>
        /// <param name="e">The edits provider used for accepting.</param>
        /// <param name="view">The view where instance will be accepted.</param>
        /// <returns>Variable with accepted catalog and correct scope.</returns>
        private object acceptPartCatalog(EditsProvider e, ExecutionView view)
        {
            var instance = UserInteraction.DraggedInstance;
            var isCatalog = Services.IsAssignable(TypeDescriptor.Create<ComposablePartCatalog>(), instance.Info);

            if (!isCatalog)
            {
                //allow accepting only components
                view.Abort("CompositionContainer can accept only single part catalog");
                return null;
            }

            return e.GetVariableFor(instance, view);
        }

        #endregion

        #region Container drawing

        /// <summary>
        /// Export data from represented <see cref="Instance" /> by using given drawer.
        /// <remarks>Note that only instances which are forced to display are displayed in root of <see cref="MEFEditor.Drawing.DiagramCanvas" /></remarks>.
        /// </summary>
        /// <param name="drawer">The drawer.</param>
        protected override void draw(InstanceDrawer drawer)
        {
            var slot = drawer.AddSlot();

            drawCatalog(drawer, slot);
            setCompositionInfo(drawer);

            drawer.ForceShow();
        }

        /// <summary>
        /// Sets the composition information.
        /// </summary>
        /// <param name="drawer">The drawer.</param>
        private void setCompositionInfo(InstanceDrawer drawer)
        {
            var compositionResult = CompositionResult.Get();

            CompositionContext context = null;

            var error = Error.Value == null ? "" : Error.Value;
            if (compositionResult != null)
            {
                if (compositionResult.Error != null)
                    error = compositionResult.Error + Environment.NewLine + error;
                context = compositionResult.Context;
            }

            if (error != "")
                drawer.SetProperty("Error", error);

            if (context != null)
            {
                foreach (var component in context.InputInstances)
                {
                    var drawing = drawer.GetInstanceDrawing(component);
                    drawing.SetProperty("Composed", "True");
                }

                foreach (var point in compositionResult.Points)
                {
                    var joinPoint = getConnector(point, drawer);
                }

                foreach (var join in compositionResult.Joins)
                {
                    if (compositionResult.Failed && !join.IsErrorJoin)
                        //on error we want to display only error joins
                        continue;

                    var fromPoint = getConnector(join.Export, drawer);
                    var toPoint = getConnector(join.Import, drawer);

                    var joinDefinition = drawer.DrawJoin(fromPoint, toPoint);
                    //TODO set properties of joinDefinition
                }
            }
        }

        /// <summary>
        /// Draws the catalog.
        /// </summary>
        /// <param name="drawer">The drawer.</param>
        /// <param name="slot">The slot.</param>
        private void drawCatalog(InstanceDrawer drawer, SlotDefinition slot)
        {
            var composableCatalog = ComposableCatalog.Get();
            if (composableCatalog != null)
            {
                var catalogDrawing = drawer.GetInstanceDrawing(composableCatalog);
                slot.Add(catalogDrawing.Reference);
            }

            foreach (var composedPart in DirectChildren.Value)
            {
                var partDrawing = drawer.GetInstanceDrawing(composedPart);
                slot.Add(partDrawing.Reference);
            }
        }

        /// <summary>
        /// Gets the connector.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="drawer">The drawer.</param>
        /// <returns>ConnectorDefinition.</returns>
        private ConnectorDefinition getConnector(JoinPoint point, InstanceDrawer drawer)
        {
            var instance = drawer.GetInstanceDrawing(point.Instance.Component);

            var connector = instance.GetJoinPoint(point.Point);

            connector.SetProperty("Error", point.Error);
            connector.SetProperty("Warning", point.Warning);

            return connector;
        }

        #endregion
    }
}
