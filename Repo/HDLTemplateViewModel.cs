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
    // ■■ HDLTemplateWindow のバインディングの解決用 ■■
    public class HDLTemplateViewModel : INotifyPropertyChanged
    {
        private string _entityName = "USER_TOP";
        private bool _validEntityName = true;
        private string PreferredLanguage;

        public HDLTemplateViewModel(string language)
        {
            PreferredLanguage = language;
        }

        public string EntityName
        {
            get => _entityName;
            set
            {
                if (_entityName == value)
                    return;
                _entityName = value;
                _validEntityName = (PreferredLanguage == "VHDL") ? 
                    VHDLNameChecker.Check(_entityName) : VerilogNameChecker.Check(_entityName);

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

    public class TemplatePortItem : INotifyPropertyChanged
    {
        private string _name = "";
        private bool _validName = false;
        private string PreferredLanguage;

        public TemplatePortItem(string language)
        {
            PreferredLanguage = language;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                _validName = (PreferredLanguage == "VHDL") ?
                    VHDLNameChecker.Check(_name) : VerilogNameChecker.Check(_name);

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

        public VerilogPort ToVerilogPort()
        {
            int intWidth = int.Parse(_width);
            int upper = (intWidth == 1) ? -1 : intWidth - 1;
            int lower = (intWidth == 1) ? -1 : 0;
            return new VerilogPort(Name, Direction + "put", upper, lower);
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
