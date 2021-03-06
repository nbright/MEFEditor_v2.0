﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

namespace MEFEditor.Drawing.Behaviours
{
    class PreviewDropStrategy : DropStrategyBase
    {
        protected override void move()
        {
            DragAdorner.Hint = "Change item position";
            E.Effects = DragDropEffects.Move;
        }

        protected override void onDrop()
        {
            Hint = "";
        }

        protected override void onDropEnd()
        {
            //nothing to do
        }

        protected override bool exclude()
        {
            if (DragItem.CanExcludeFromParent)
            {
                CurrentView = DragItem.ParentExcludeEdit.Action(CurrentView);

                if (CurrentView.HasError)
                {
                    addHintLine("Cannot exclude from: '{0}'", DragItem.ParentItem.ID);
                }
                else
                {
                    addHintLine("Exclude from: '{0}'", DragItem.ParentItem.ID);
                    return true;
                }
            }
            else
            {
                E.Effects = DragDropEffects.None;
                addHintLine("Can't exclude from: '{0}'", DragItem.ParentItem.ID);
            }

            return false;
        }

        protected override bool rootExclude()
        {
            //nothing to do
            return true;
        }

        protected override void rootAccept()
        {
            //notihng to do
        }

        protected override void accept()
        {
            var acceptTarget = DropTarget.OwnerItem.ID;
            var errors = new List<string>();
            foreach (var accept in DropTarget.OwnerItem.AcceptEdits)
            {
                //TODO use exclude view
                var accepting = accept.Action(CurrentView);
                if (!accepting.HasError)
                {
                    CurrentView = accepting;
                    addHintLine("Accept by '{0}'", acceptTarget);
                    return;
                }

                errors.Add(accepting.Error);
            }

            var error = errors.Count == 0 ? "There is no accept edit" : string.Join("' and\n\t'", errors);
            E.Effects = DragDropEffects.None;
            addHintLine("Cannot accept by '{0}', because of \n\t'{1}'", acceptTarget, error);
        }

        private void addHintLine(string format, params object[] formatArgs)
        {
            var line = string.Format(format, formatArgs);
            if (Hint != "")
                Hint += "\n";

            Hint += line;
        }
    }
}
