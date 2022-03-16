// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace DRFront
{
    // ■■ Vivado の起動等に関するメソッド ■■
    public partial class MainWindow : Window
    {
        // Vivado のバージョンチェックを行う
        private string CheckVivadoVersion()
        {
            DirectoryInfo vivadoRoot = new DirectoryInfo(VivadoRootPath);
            List<string> versions = new List<string>();
            if (! vivadoRoot.Exists)
                return null;

            foreach (DirectoryInfo subDir in vivadoRoot.GetDirectories())
                if (subDir.GetFiles("settings64.bat").Length != 0)
                    versions.Add(subDir.Name);

            if (versions.Count == 0)
                return null;

            versions.Sort();
            return versions[versions.Count - 1];
        }

        // Vivado の起動準備が整っているか確認
        private bool CheckForLaunchVivado()
        {
            if (VivadoVersion == null)
            {
                MsgBox.Warn("Vivado が見つからないため，起動できません．");
                return false;
            }
            if (VivadoLastLaunched != null)
            {
                TimeSpan ts = DateTime.Now - VivadoLastLaunched;
                if (ts.TotalSeconds < 10)
                {
                    MsgBox.Warn("Vivado を起動中です．しばらくお待ちください．");
                    return false;
                }
            }
            return true;
        }

        // Vivado に与える Tcl ファイルを作成する
        private bool PrepareTcl(string project, string tclFile, string template, Dictionary<string, string> args = null)
        {
            try
            {
                StreamWriter sw = File.CreateText(VM.SourceDirPath + @"\" + project + @"\" + tclFile);
                string[] templateLines = template.Replace("\r\n","\n").Split(new[]{ '\n'});
                if (args != null)
                    foreach (KeyValuePair<string, string> arg in args)
                        sw.WriteLine("set " + arg.Key + " " + arg.Value);
                foreach (string line in templateLines)
                    sw.WriteLine(line);
                sw.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("スクリプトファイルの作成中にエラーが発生しました．\n" + ex.Message);
                return false;
            }
            return true;
        }

        // Vivado を起動する
        private bool LaunchVivado(string project, string tclFile, bool batchMode = false)
        {
            VivadoLastLaunched = DateTime.Now;
            Process p = new Process();
            try
            {
                string vivadoMode = (batchMode) ? "batch" : "tcl";
                string vivadoDir = VivadoRootPath + VivadoVersion;
                p.StartInfo.FileName = vivadoDir + @"\bin\vivado.bat";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = false; // あえてコマンドプロンプトを表示させる
                p.StartInfo.WorkingDirectory = VM.SourceDirPath + @"\" + project;
                p.StartInfo.Arguments = "-mode " + vivadoMode + " -source " + tclFile + " -nojournal";
                p.StartInfo.Environment["PATH"] += ";" + vivadoDir + @"\bin;" + vivadoDir + @"\lib\win64.o";
                p.StartInfo.Environment["XILINX_VIVADO"] = vivadoDir;
                p.Start();
            }
            catch (Win32Exception ex)
            {
                MsgBox.Warn("Vivado の起動中にエラーが発生しました．\n" + ex.Message);
                return false;
            }
            return true;
        }
    }
}
