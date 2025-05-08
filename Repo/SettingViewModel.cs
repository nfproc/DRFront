// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2025 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace DRFront
{
    // ■■ SettingWindow のバインディングの解決用
    class SettingViewModel : INotifyPropertyChanged
    {
        private string _vivadoRootPath = "";
        public string VivadoRootPath
        {
            get => _vivadoRootPath;
            set
            {
                if (_vivadoRootPath == value)
                    return;
                _vivadoRootPath = value;
                OnPropertyChanged("VivadoRootPath");
            }
        }

        private string _selectedVersion = "";
        public string SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                if (_selectedVersion == value)
                    return;
                _selectedVersion = value;
                OnPropertyChanged("SelectedVersion");
            }
        }

        private string _selectedBoard = "";
        public string SelectedBoard
        {
            get => _selectedBoard;
            set
            {
                if (_selectedBoard == value)
                    return;
                _selectedBoard = value;
                OnPropertyChanged("SelectedBoard");
            }
        }

        private string _preferredLanguage = "";
        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set
            {
                if (_preferredLanguage == value)
                    return;
                _preferredLanguage = value;
                OnPropertyChanged("PreferredLanguage");
            }
        }

        private bool _useDCP = true;
        public bool UseDCP
        {
            get => _useDCP;
            set
            {
                if (_useDCP == value)
                    return;
                _useDCP = value;
                OnPropertyChanged("UseDCP");
            }
        }

        public ObservableCollection<string> VivadoVersions { get; set; } =
            new ObservableCollection<string>();
        public ObservableCollection<string> TargetBoards { get; set; } =
            new ObservableCollection<string>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class PreferredLanguageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((string) parameter == (string) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool) value ? parameter : Binding.DoNothing;
        }
    }
}
