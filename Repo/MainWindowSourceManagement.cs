// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace DRFront
{
    // ■■ ソースファイルの解析・トップ回路の作成に関するメソッド ■■
    public partial class MainWindow : Window
    {
        // ソースディレクトリに問題がないかチェックする
        private void CheckSourceDirectory()
        {
            VM.IsSourceValid = false;
            VM.IsProjectValid = false;
            VM.SourceProblem = "";
            VM.UserEntity = "";
            HDLUserPorts = null;
            VM.UserPorts.Clear();
            updateTimer.Stop();

            // 問題が起きそうなディレクトリ名の場合警告する
            Match match = Regex.Match(VM.SourceDirPath, @"[^\u0000-\u001F\u0021-\u007F]");
            if (match.Success)
                VM.SourceProblem = "ディレクトリ名に非ASCII文字や空白が含まれます．\nVivado が正常に動作しないかもしれません．";

            // ディレクトリかどうか確認
            if (VM.SourceDirPath == "")
            {
                VM.SourceProblem = "ディレクトリ名が空です．";
                UpdateProjectList();
                return;
            }
            if (! Directory.Exists(VM.SourceDirPath))
            {
                VM.SourceProblem = "無効または存在しないディレクトリです．";
                UpdateProjectList();
                return;
            }

            // HDL ファイルを含むかどうか確認
            string topHDL      = (ST.PreferredLanguage == "VHDL") ? FileName.TopVHDL : FileName.TopVerilog;
            IList<string> exts = (ST.PreferredLanguage == "VHDL") ? FileName.ExtsVHDL : FileName.ExtsVerilog;

            SourceFileNames.Clear();
            foreach (string ext in exts)
                foreach (string file in Directory.GetFiles(VM.SourceDirPath, "*" + ext))
                    if (! file.ToLower().EndsWith(topHDL) && file.ToLower().EndsWith(ext))
                        SourceFileNames.Add(file);

            if (SourceFileNames.Count == 0)
            {
                VM.SourceProblem = "ディレクトリ内に " + ST.PreferredLanguage + " ファイルが見つかりません．";
                UpdateProjectList();
                return;
            }

            // HDL ファイルを順番に解析 (See HDLSources.cs)
            string fullSVInstPath = BaseDir + "\\" + FileName.SVInstPath;
            TopFinder = new TopEntityFinder(SourceFileNames, ST.PreferredLanguage, fullSVInstPath);
            if (! TopFinder.IsValid)
            {
                VM.SourceProblem = TopFinder.Problem;
                UpdateProjectList();
                return;
            }

            // 解析結果をウィンドウに反映
            VM.IsSourceValid = true;
            UpdateProjectList();
            updateTimer.Start();
        }

        // トップ回路のポート → ユーザ回路のポート の割当てを返す
        private Dictionary<string, List<string>> GetAssignments()
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
            foreach (var rect in ComponentRectangles)
                result.Add(rect.Key, new List<string>());

            foreach (var port in VM.UserPorts)
                if (port.TopPort != "" && result.ContainsKey(port.TopPort))
                    result[port.TopPort].Add(port.Name);

            return result;
        }

        // トップ回路の HDL 記述を生成する
        private void GenerateTopHDL(string project)
        {
            // エラー・重複がある場合は生成しない
            if (!VM.IsProjectValid || project == NewProjectLabel)
                return;
            Dictionary<string, List<string>> assignments = GetAssignments();
            foreach (var assignment in assignments)
                if (assignment.Value.Count >= 2)
                    return;

            string fullFileName;
            HDLEntity ent;
            HDLSource src;
            string template;
            if (ST.PreferredLanguage == "VHDL")
            {
                fullFileName = VM.SourceDirPath + @"\" + project + @"\" + FileName.TopVHDL;
                ent = new VHDLEntity(FileName.TopVHDL, VM.UserEntity);
                src = new VHDLSource(ent, HDLUserPorts);
                template = Properties.Resources.DR_TOP;
            }
            else
            {
                fullFileName = VM.SourceDirPath + @"\" + project + @"\" + FileName.TopVerilog;
                ent = new VerilogEntity(FileName.TopVerilog, VM.UserEntity);
                src = new VerilogSource(ent, HDLUserPorts);
                template = Properties.Resources.DR_TOP_V;
            }

            Dictionary<string, string> unusedPorts = new Dictionary<string, string>();
            foreach (var def in ComponentDefaults)
                if (assignments[def.Key].Count == 0)
                    unusedPorts.Add(def.Key, def.Value);

            src.Generate(template, fullFileName, VM.UserPorts, unusedPorts);
        }

        // テストベンチ雛形の HDL 記述を生成する
        private void GenerateTestBenchHDL(string project)
        {
            if (!VM.IsProjectValid || project == NewProjectLabel)
                return;

            string fullFileName;
            HDLEntity ent;
            HDLSource src;
            string template;
            if (ST.PreferredLanguage == "VHDL")
            {
                fullFileName = VM.SourceDirPath + @"\" + project + @"\" + FileName.TestBenchVHDL;
                ent = new VHDLEntity(FileName.TestBenchVHDL, VM.UserEntity);
                src = new VHDLSource(ent, HDLUserPorts);
                template = Properties.Resources.DR_TESTBENCH;
            }
            else
            {
                fullFileName = VM.SourceDirPath + @"\" + project + @"\" + FileName.TestBenchVerilog;
                ent = new VerilogEntity(FileName.TestBenchVerilog, VM.UserEntity);
                src = new VerilogSource(ent, HDLUserPorts);
                template = Properties.Resources.DR_TESTBENCH_V;
            }
            src.Generate(template, fullFileName, VM.UserPorts);
        }
        
        // トップ回路の HDL 記述を読んで，割当てを復元する
        private void ReadTopHDL(string project)
        {
            HDLSource src;
            string topFileName;
            if (ST.PreferredLanguage == "VHDL")
            {
                src = new VHDLSource();
                topFileName = FileName.TopVHDL;
            }
            else
            {
                src = new VerilogSource();
                topFileName = FileName.TopVerilog;
            }
            string oldFileName = VM.SourceDirPath + @"\" + topFileName;
            string fullFileName = VM.SourceDirPath + @"\" + project + @"\" + topFileName;

            // エラーがある場合やファイルが存在しない場合はスキップ
            if (! VM.IsProjectValid)
                return;
            if (File.Exists(oldFileName))
                MoveTopVHDL();
            if (! File.Exists(fullFileName))
                return;

            src.ReadTop(fullFileName);
            TopFinder.SetTopEntity(src.Components[0].Name);
            foreach (HDLPort uport in TopFinder.UserPorts)
            {
                uport.ToAssign = "";
                foreach (HDLPort hport in src.Ports["top"])
                    if (uport.Name == hport.Name && ComponentRectangles.ContainsKey(hport.ToAssign))
                        uport.ToAssign = hport.ToAssign;
            }

            UpdateComponentRectangles();
        }

        // トップ回路の VHDL 記述をソースディレクトリからプロジェクトディレクトリに移動（v0.3.0 仕様変更による）
        private void MoveTopVHDL()
        {
            string oldPath = VM.SourceDirPath + @"\" + FileName.TopVHDL;
            if (! File.Exists(oldPath))
                return;
            MsgBox.Info("トップ回路がソースディレクトリに見つかりました．\n各プロジェクトのディレクトリへ移動します．");

            try
            {
                foreach (string project in VM.VivadoProjects)
                {
                    if (project == NewProjectLabel)
                        break;
                    string newPath = VM.SourceDirPath + @"\" + project + @"\" + FileName.TopVHDL;
                    File.Copy(oldPath, newPath, true);
                }
                File.Delete(oldPath);
            }
            catch (IOException ex)
            {
                MsgBox.Warn("トップ回路の移動中にエラーが発生しました．\n" + ex.Message);
            }
        }

        // ユーザ回路のポート一覧が変更されていないかを確認する
        private bool CheckUserPortModified()
        {
            bool modified = false;
            string fullSVInstPath = BaseDir + "\\" + FileName.SVInstPath;
            TopEntityFinder newTop = new TopEntityFinder(SourceFileNames, ST.PreferredLanguage, fullSVInstPath);
            if (! newTop.IsValid)
            {
                MsgBox.Warn("更新されたソースファイルに問題があります．\n" + newTop.Problem);
                return false;
            }
            if (newTop.TopEntity != TopFinder.TopEntity)
                if (! newTop.SetTopEntity(TopFinder.TopEntity))
                {
                    MsgBox.Warn("更新されたソースファイルに問題があります．\n" + TopFinder.TopEntity + " が見つかりません．");
                    return false;
                }
                    
            if (newTop.TopPorts.Count != TopFinder.TopPorts.Count)
                modified = true;
            else
                for (int i = 0; i < newTop.TopPorts.Count; i++)
                    if (newTop.TopPorts[i].ToString() != TopFinder.TopPorts[i].ToString())
                        modified = true;

            if (modified)
            {
                if (! MsgBox.WarnAndConfirm("ユーザ回路が更新されているようです．トップ回路を再生成して続行しますか？"))
                    return false;
                TopFinder = newTop;
                ReadTopHDL(VM.CurrentProject);
                UpdateUserPorts();
                GenerateTopHDL(VM.CurrentProject);
            }
            return true;
        }
    }
}
