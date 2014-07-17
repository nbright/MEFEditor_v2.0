﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using System.Windows;
using System.Windows.Media;

using System.ComponentModel;


namespace Drawing
{
    public class DiagramCanvas : DiagramCanvasBase
    {
        private readonly ScaleTransform _scale = new ScaleTransform();

        private Vector _shift;


        public Vector Shift
        {
            get
            {
                return _shift;
            }

            set
            {
                if (_shift == value)
                    return;

                _shift = value;
                InvalidateArrange();
            }
        }

        public double Zoom
        {
            get
            {
                //both scale factors has same value
                return _scale.ScaleX;
            }
            set
            {
                if (_scale.ScaleX == value)
                    return;

                _scale.ScaleX = value;
                _scale.ScaleY = value;

                InvalidateArrange();
            }
        }

       protected override Size ArrangeOverride(Size arrangeSize)
        {
    /*        if (DiagramContext != null)
            {
                DiagramContext.Provider.Engine.ArrangeChildren(OwnerItem, this);
            }*/

            foreach (FrameworkElement child in Children)
            {
                child.RenderTransform = _scale;

                var position = GetPosition(child);
                position = _scale.Transform(position);

                position.X += Shift.X;
                position.Y += Shift.Y;

                child.Arrange(new Rect(position, child.DesiredSize));
            }
            return arrangeSize;
        }

        public void Clear()
        {
            Children.Clear();
            ContextMenu = null;
        }

        public void Reset()
        {
            Shift = new Vector(0, 0);
            Zoom = 1;
        }
    }
}
