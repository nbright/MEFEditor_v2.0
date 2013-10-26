﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

namespace Drawing
{
    public class DiagramItemDefinition
    {
        private DrawingProperties _properties = new DrawingProperties();

        private HashSet<SlotDefinition> _slots = new HashSet<SlotDefinition>();

        /// <summary>
        /// Type of drawed object. May differ from type that provides draing
        /// </summary>
        public readonly string DrawedType;

        /// <summary>
        /// ID of current drawing definition. Has to be uniqueue in drawing context scope
        /// </summary>
        public readonly string ID;

        /// <summary>
        /// All properties that has been set for drawing definition
        /// </summary>
        public IEnumerable<DrawingProperty> Properties { get { return _properties.Values; } }

        public IEnumerable<SlotDefinition> Slots { get { return _slots; } }

        public Point GlobalPosition { get; set; }

        /// <summary>
        /// Initialize drawing definition
        /// </summary>
        /// <param name="id">ID of current drawing definition. Has to be uniqueue in drawing context scope</param>
        /// <param name="drawedType">Type of drawed object. May differ from type that provides drawing</param>
        public DiagramItemDefinition(string id, string drawedType)
        {
            ID = id;
            DrawedType = drawedType;
        }

        /// <summary>
        /// Set property of given name to value
        /// </summary>
        /// <param name="name">Name of property</param>
        /// <param name="value">Value of property</param>
        public void SetProperty(string name, string value)
        {
            _properties[name] = new DrawingProperty(name, value); 
        }

        /// <summary>
        /// Add drawing slot to current definition
        /// </summary>
        /// <param name="slot">Added slot</param>
        public void AddSlot(SlotDefinition slot)
        {
            _slots.Add(slot);
        }

        public DrawingProperty GetProperty(string name)
        {
            DrawingProperty result;
            _properties.TryGetValue(name, out result);

            return result;
        }

        
    }
}