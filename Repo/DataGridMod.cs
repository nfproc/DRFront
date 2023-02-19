// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2023 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DRFront
{
    // DataGrid has been modified for desired drag-and-drop behavior
    // ref: https://stackoverflow.com/questions/38204291/how-do-i-stop-drag-select-on-wpf-data-grid

    public class DataGridMod : DataGrid
    {
        public bool AllowDrag { get; set; } = true;

        private static readonly FieldInfo s_isDraggingSelectionField =
            typeof(DataGrid).GetField("_isDraggingSelection", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo s_endDraggingMethod =
            typeof(DataGrid).GetMethod("EndDragging", BindingFlags.Instance | BindingFlags.NonPublic);

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            
            if (CurrentCell.Column == null)
                return;
            int columnIndex = CurrentCell.Column.DisplayIndex;
            if (AllowDrag && columnIndex <= 1 && CurrentItem is UserPortItem item)
                DragDrop.DoDragDrop(this, item, DragDropEffects.Link);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if ((bool)(s_isDraggingSelectionField?.GetValue(this) ?? false))
                s_endDraggingMethod.Invoke(this, new object[0]);
        }
    }
}