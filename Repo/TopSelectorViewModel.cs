// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2023 Naoki FUJIEDA. New BSD License is applied.
//
// Code in this file are derived from "GGFront: A GHDL/GTKWave GUI Frontend"
// Copyright (C) 2018-2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Windows.Data;
using System.Windows.Media;

namespace DRFront
{
    // EntityHierarchyItem のコンバータクラス
    public class HierarchyLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.Format("{0},0,0,0", (int)value * 30);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HierarchyPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return "";
            if ((string)value == "")
                return "";

            return "(in " + (string)value + ")";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HierarchyTopColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((bool)value) ? Brushes.Blue : Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HierarchyTopFontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((bool)value) ? "Bold" : "Normal";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
