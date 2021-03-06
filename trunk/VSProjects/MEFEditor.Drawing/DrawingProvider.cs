﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;

namespace MEFEditor.Drawing
{
    /// <summary>
    /// Provider of drawing services. It is entry point of MEFEditor.Drawing library API.
    /// </summary>
    public class DrawingProvider
    {
        /// <summary>
        /// Factory used for creating diagram items
        /// </summary>
        private readonly AbstractDiagramFactory _diagramFactory;

        /// <summary>
        /// Lastly used context menu handler
        /// </summary>
        private ContextMenuEventHandler _lastContextMenuHandler;

        /// <summary>
        /// Currently displayed diagram
        /// </summary>
        private DiagramDefinition _currentDiagram;

        /// <summary>
        /// Engine used by current provider
        /// </summary>
        internal readonly DisplayEngine Engine;

        /// <summary>
        /// Output that is used by current provider
        /// </summary>
        internal DiagramCanvas Output { get { return Engine.Output; } }

        /// <summary>
        /// Initialize new instance of <see cref="DrawingProvider"/>
        /// </summary>
        /// <param name="output">Output that is used by current provider</param>
        /// <param name="diagramFactory">Factory that is used for creating diagrams</param>
        public DrawingProvider(DiagramCanvas output, AbstractDiagramFactory diagramFactory)
        {
            Engine = new DisplayEngine(output);
            _diagramFactory = diagramFactory;
        }

        #region Display handling

        /// <summary>
        /// Display diagram specified by given definition
        /// </summary>
        /// <param name="diagramDefinition">Diagram definition to be displayed</param>
        /// <returns>Context of displayed diagram</returns>
        public DiagramContext Display(DiagramDefinition diagramDefinition)
        {
            _currentDiagram = diagramDefinition;

            Engine.Clear();

            if (diagramDefinition == null)
                return null;

            var context = new DiagramContext(this, diagramDefinition);

            displayItems(diagramDefinition, context);

            var menu = createContextMenu(diagramDefinition, context);

            //context menu handling
            Engine.Output.ContextMenu = menu;
            if (_lastContextMenuHandler != null)
                Engine.Output.ContextMenuOpening -= _lastContextMenuHandler;
            _lastContextMenuHandler = (s, e) => menu_ContextMenuOpening(menu, context);
            Engine.Output.ContextMenuOpening += _lastContextMenuHandler;
            Engine.Output.SetContext(context);
            Engine.Display();

            return context;
        }

        /// <summary>
        /// Reset positions of diagram items
        /// </summary>
        public void ResetPositions()
        {
            //force saving old positions
            Engine.Clear();
            //clear saved positoins
            Engine.ClearOldPositions();
        }

        /// <summary>
        /// Redraw current diagram
        /// </summary>
        public void Redraw()
        {
            Display(_currentDiagram);
        }
        #endregion

        #region Drawing utilities

        /// <summary>
        /// Initialize drawing of given item according to its definition
        /// </summary>
        /// <param name="item">Item to be initialized</param>
        internal void InitializeItemDrawing(DiagramItem item)
        {
            foreach (var connectorDefinition in item.ConnectorDefinitions)
            {
                var connector = _diagramFactory.CreateConnector(connectorDefinition, item);
                item.Attach(connector);
            }

            ContentDrawing content;
            if (item.IsRecursive)
            {
                content = _diagramFactory.CreateRecursiveContent(item);
            }
            else
            {
                content = _diagramFactory.CreateContent(item);
            }
            item.SetContent(content);


            Engine.RegisterItem(item);
        }

        /// <summary>
        /// Create context menu according to edits and commands in given definition
        /// </summary>
        /// <param name="diagramDefinition">Definitin of diagram</param>
        /// <param name="context">Context available for diagram definition</param>
        /// <returns>Created context menu</returns>
        private ContextMenu createContextMenu(DiagramDefinition diagramDefinition, DiagramContext context)
        {
            var menu = new ContextMenu();

            //add edit entries
            foreach (var edit in diagramDefinition.Edits)
            {
                if (!edit.IsActive(diagramDefinition.InitialView))
                    continue;

                var item = createEditItem(edit, context);
                menu.Items.Add(item);
            }

            foreach (var menuProvider in diagramDefinition.MenuProviders)
            {
                var item = new MenuItem();
                item.Header = menuProvider.Key;
                item.Tag = menuProvider.Value;

                menu.Items.Add(item);
            }

            //add command entries
            foreach (var command in diagramDefinition.Commands)
            {
                var item = new MenuItem();
                item.Header = command.Name;

                item.Click += (e, s) => command.Command();
                menu.Items.Add(item);
            }

            return menu;
        }

        /// <summary>
        /// Create <see cref="MenuItem"/> representing given edit
        /// </summary>
        /// <param name="edit">Represented edit</param>
        /// <param name="context">Context of edit</param>
        /// <returns>Created <see cref="MenuItem"/></returns>
        private static MenuItem createEditItem(EditDefinition edit, DiagramContext context)
        {
            var item = new MenuItem();
            item.Header = edit.Name;

            item.Click += (e, s) => edit.Commit(context.Diagram.InitialView);
            return item;
        }

        /// <summary>
        /// Displaye <see cref="DiagramItem"/> objects according to given diagram definition
        /// </summary>
        /// <param name="diagramDefinition">Definition of diagram with items to display</param>
        /// <param name="context">Context available for diagram definition</param>
        private void displayItems(DiagramDefinition diagramDefinition, DiagramContext context)
        {
            foreach (var definition in context.RootItemDefinitions)
            {
                var item = new DiagramItem(definition, context);
                InitializeItemDrawing(item);
            }

            if (!diagramDefinition.ShowJoinLines)
                return;

            //show join lines
            foreach (var joinDefinition in diagramDefinition.JoinDefinitions)
            {
                foreach (var from in Engine.DefiningItems(joinDefinition.From))
                {
                    foreach (var to in Engine.DefiningItems(joinDefinition.To))
                    {
                        var join = _diagramFactory.CreateJoin(joinDefinition, context);
                        Engine.AddJoin(join, from, to);
                    }
                }
            }
        }

        /// <summary>
        /// GUI event handler for opening context menu. It computes
        /// currently displayed menu items.
        /// </summary>
        /// <param name="menu">The menu which is opening.</param>
        /// <param name="context">The context where menu is displayed.</param>
        private void menu_ContextMenuOpening(ContextMenu menu, DiagramContext context)
        {
            foreach (MenuItem item in menu.Items)
            {
                var provider = item.Tag as EditsMenuProvider;
                if (provider == null)
                    continue;

                item.Items.Clear();

                //create edits
                var edits = provider();
                if (edits == null)
                    continue;

                foreach (var edit in edits)
                {
                    var editItem = createEditItem(edit, context);
                    item.Items.Add(editItem);
                }
            }
        }

        #endregion
    }
}
