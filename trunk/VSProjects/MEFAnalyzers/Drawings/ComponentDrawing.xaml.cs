﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Drawing;

namespace MEFAnalyzers.Drawings
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class ComponentDrawing : ContentDrawing
    {
        public ComponentDrawing(DrawingDefinition definition)
            :base(definition)
        {
            InitializeComponent();

            TypeName.Text = definition.DrawedType;

            foreach (var property in definition.Properties)
            {
                var propertyBlock = new TextBlock();
                propertyBlock.Text = string.Format("{0}: {1}", property.Name, property.Value);

                Properties.Children.Add(propertyBlock);
            }
        }
    }
}
