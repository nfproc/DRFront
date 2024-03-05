// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DRFront
{
    // ■■ MainWindow のバインディングの解決用 ■■
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _sourceDirPath = "";
        public string SourceDirPath
        {
            get => _sourceDirPath;
            set
            {
                if (_sourceDirPath == value)
                    return;
                _sourceDirPath = value;
                OnPropertyChanged("SourceDirPath");
            }
        }

        private string _currentProject = "";
        public string CurrentProject
        {
            get => _currentProject;
            set
            {
                if (_currentProject == value)
                    return;
                _currentProject = value;
                OnPropertyChanged("CurrentProject");
            }
        }

        private string _userEntity = "";
        public string UserEntity
        {
            get => _userEntity;
            set
            {
                if (_userEntity == value)
                    return;
                _userEntity = value;
                OnPropertyChanged("UserEntity");
            }
        }

        private string _sourceProblem = "";
        public string SourceProblem
        {
            get => _sourceProblem;
            set
            {
                if (_sourceProblem == value)
                    return;
                _sourceProblem = value;
                OnPropertyChanged("SourceProblem");
            }
        }

        private bool _isSourceValid = false;
        public bool IsSourceValid
        {
            get => _isSourceValid;
            set
            {
                if (_isSourceValid == value)
                    return;
                _isSourceValid = value;
                OnPropertyChanged("IsSourceValid");

            }
        }

        private bool _isProjectValid = false;
        public bool IsProjectValid
        {
            get => _isProjectValid;
            set
            {
                if (_isProjectValid == value)
                    return;
                _isProjectValid = value;
                OnPropertyChanged("IsProjectValid");

            }
        }

        private bool _isNewProjectSelected = false;
        public bool IsNewProjectSelected
        {
            get => _isNewProjectSelected;
            set
            {
                if (_isNewProjectSelected == value)
                    return;
                _isNewProjectSelected = value;
                OnPropertyChanged("IsNewProjectSelected");

            }
        }

        private bool _isTCLAvailable = false;
        public bool IsTCLAvailable
        {
            get => _isTCLAvailable;
            set
            {
                if (_isTCLAvailable == value)
                    return;
                _isTCLAvailable = value;
                OnPropertyChanged("IsTCLAvailable");

            }
        }

        private bool _isDCPAvailable = false;
        public bool IsDCPAvailable
        {
            get => _isDCPAvailable;
            set
            {
                if (_isDCPAvailable == value)
                    return;
                _isDCPAvailable = value;
                OnPropertyChanged("IsDCPAvailable");

            }
        }

        private bool _isBITAvailable = false;
        public bool IsBITAvailable
        {
            get => _isBITAvailable;
            set
            {
                if (_isBITAvailable == value)
                    return;
                _isBITAvailable = value;
                OnPropertyChanged("IsBITAvailable");

            }
        }

        public ObservableCollection<string> VivadoProjects { get; set; } =
            new ObservableCollection<string>();
        public ObservableCollection<UserPortItem> UserPorts { get; set; } =
            new ObservableCollection<UserPortItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class UserPortItem : INotifyPropertyChanged
    {
        private string _topPort = "";
        public string Name { get; }
        public string Direction { get; }
        public string TopPort
        {
            get => _topPort;
            set
            {
                if (_topPort == value)
                    return;
                _topPort = value;
                OnPropertyChanged();
            }
        }

        public List<string> TopPortList { get; }

        public UserPortItem(string name, string direction, List<string> portList, string topPort = "")
        {
            Name = name;
            Direction = direction;
            TopPortList = portList;
            if (TopPortList.Contains(topPort))
                TopPort = topPort;
            else
                TopPort = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TopPort"));
        }
    }

    public class SourceDirForeGroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? Brushes.Black : Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SourceDirBackGroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((string)value == "") ? Brushes.White : Brushes.LightYellow;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ToolTipVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((string)value == "") ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
