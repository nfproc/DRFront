// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2023 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Media;

namespace DRFront
{
    // ■■ VHDLTemplateWindow のバインディングの解決用 ■■
    public class VHDLTemplateViewModel : INotifyPropertyChanged
    {
        private string _entityName = "USER_TOP";
        private bool _validEntityName = true;
        public string EntityName
        {
            get => _entityName;
            set
            {
                if (_entityName == value)
                    return;
                _entityName = value;
                _validEntityName = VHDLNameChecker.Check(_entityName);

                OnPropertyChanged("EntityName");
                OnPropertyChanged("ValidEntityName");
            }
        }
        public bool ValidEntityName { get => _validEntityName; }

        public ObservableCollection<TemplatePortItem> TemplatePorts { get; set; } =
            new ObservableCollection<TemplatePortItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    // VHDL の信号名チェックのための静的クラス
    public static class VHDLNameChecker
    {
        private static List<string> reservedNames = new List<string>
        {
            "abs", "access", "after", "alias", "all", "and", "architecture", "array", "assert",
            "attribute", "begin", "block", "body", "buffer", "bus", "case", "component",
            "configuration", "constant", "disconnect", "downto", "else", "elsif", "end",
            "entity", "exit", "file", "for", "function", "generate", "generic", "group",
            "guarded", "if", "impure", "in", "inertial", "inout", "is", "label", "library",
            "linkage", "literal", "loop", "map", "mod", "nand", "new", "next", "nor", "not",
            "null", "of", "on", "open", "or", "others", "out", "package", "port", "postponed",
            "procedure", "process", "pure", "range", "record", "register", "reject", "rem",
            "report", "return", "rol", "ror", "select", "severity", "signal", "shared", "sla",
            "sll", "sra", "srl", "subtype", "then", "to", "transport", "type", "unaffected",
            "units", "until", "use", "variable", "wait", "when", "while", "with", "xnor", "xor"
        };

        public static bool Check(string name)
        {
            bool valid = (name != "");
            Match match = Regex.Match(name, @"^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase);
            valid &= match.Success;
            valid &= ! name.Contains("__");
            valid &= ! name.EndsWith("_");
            valid &= ! reservedNames.Contains(name.ToLower());
            return valid;
        }
    }

    public class TemplatePortItem : INotifyPropertyChanged
    {
        private string _name = "";
        private bool _validName = false;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                _validName = VHDLNameChecker.Check(_name);

                OnPropertyChanged("Name");
                OnPropertyChanged("ValidName");
            }
        }
        public bool ValidName { get => _validName; }

        private string _direction = "in";
        public string Direction
        {
            get => _direction;
            set
            {
                if (_direction == value)
                    return;
                _direction = value;
                OnPropertyChanged("Direction");
            }
        }

        private string _width = "1";
        private bool _validWidth = true;
        public string Width
        {
            get => _width;
            set
            {
                if (value == _width)
                    return;
                _width = value;

                int intWidth;
                _validWidth = int.TryParse(_width, out intWidth);
                _validWidth &= (intWidth >= 1);

                OnPropertyChanged("Width");
                OnPropertyChanged("ValidWidth");
            }
        }
        public bool ValidWidth { get => _validWidth; }
        
        public VHDLPort ToVHDLPort()
        {
            int intWidth = int.Parse(_width);
            int upper = (intWidth == 1) ? -1 : intWidth - 1;
            int lower = (intWidth == 1) ? -1 : 0;
            return new VHDLPort(Name, Direction + "put", upper, lower);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class TemplatePortBackGroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? Brushes.White : Brushes.LightPink;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
