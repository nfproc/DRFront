// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2025 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;

namespace DRFront
{
    // ■■ 設定画面のウィンドウ ■■
    public partial class SettingWindow : Window
    {
        private SettingViewModel VM;
        public DRFrontSettings NewSetting;

        public SettingWindow(string baseDir, DRFrontSettings oldSetting)
        {
            InitializeComponent();

            VM = new SettingViewModel();
            DataContext = VM;
            VM.VivadoRootPath = oldSetting.VivadoRootPath;
            RefreshVivadoVersions();
            VM.SelectedVersion = oldSetting.VivadoVersion;
            foreach (string board in Util.GetSubDirs(baseDir, "board.xml"))
                VM.TargetBoards.Add(board);
            VM.SelectedBoard = oldSetting.TargetBoardDir;
            VM.PreferredLanguage = oldSetting.PreferredLanguage;
            VM.UseDCP = oldSetting.UseDCP;
        }

        private void VivadoDir_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).GetBindingExpression(TextBox.TextProperty).UpdateSource();
            RefreshVivadoVersions();
        }

        private void RefreshVivadoVersions()
        {
            string lastVersion = VM.SelectedVersion;
            VM.VivadoVersions.Clear();
            foreach (string ver in Util.GetVivadoVersions(VM.VivadoRootPath))
                VM.VivadoVersions.Add(ver);
            VM.SelectedVersion = lastVersion;
        }

        private void FindVivadoDir_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Vivado のインストール先を選択";
            dialog.UseDescriptionForTitle = true;
            if (Directory.Exists(VM.VivadoRootPath))
                dialog.SelectedPath = VM.VivadoRootPath;
            if ((bool)dialog.ShowDialog())
            {
                if (VM.VivadoRootPath == dialog.SelectedPath)
                {
                    RefreshVivadoVersions();
                }
                else
                {
                    VM.VivadoRootPath = dialog.SelectedPath;
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshVivadoVersions();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            NewSetting = new DRFrontSettings();
            NewSetting.VivadoRootPath = Path.GetFullPath(VM.VivadoRootPath);
            if (! NewSetting.VivadoRootPath.EndsWith("\\"))
                NewSetting.VivadoRootPath += "\\";
            NewSetting.VivadoVersion = VM.SelectedVersion;
            NewSetting.TargetBoardDir = VM.SelectedBoard;
            NewSetting.PreferredLanguage = VM.PreferredLanguage;
            NewSetting.UseDCP = VM.UseDCP;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
