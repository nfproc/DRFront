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
            VHDLUserPorts = null;
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
            if (!Directory.Exists(VM.SourceDirPath))
            {
                VM.SourceProblem = "無効または存在しないディレクトリです．";
                UpdateProjectList();
                return;
            }

            // VHDL ファイルを含むかどうか確認
            SourceFileNames.Clear();
            foreach (string file in Directory.GetFiles(VM.SourceDirPath, "*.vhd"))
                if (file.ToLower().EndsWith(".vhd"))
                    SourceFileNames.Add(file);
            foreach (string file in Directory.GetFiles(VM.SourceDirPath, "*.vhdl"))
                if (! file.ToLower().EndsWith(FileName.TopVHDL) && file.ToLower().EndsWith(".vhdl"))
                    SourceFileNames.Add(file);
            if (SourceFileNames.Count == 0)
            {
                VM.SourceProblem = "ディレクトリ内に VHDL ファイルが見つかりません．";
                UpdateProjectList();
                return;
            }

            // VHDL ファイルを順番に解析 (See VHDLSources.cs)
            TopFinder = new TopEntityFinder(SourceFileNames);
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

        // トップ回路の VHDL 記述を生成する
        private void GenerateTopVHDL(string project)
        {
            // エラー・重複がある場合は生成しない
            if (!VM.IsProjectValid || project == NewProjectLabel)
                return;
            Dictionary<string, List<string>> assignments = GetAssignments();
            foreach (var assignment in assignments)
                if (assignment.Value.Count >= 2)
                    return;

            GenerateVHDL(Properties.Resources.DR_TOP, FileName.TopVHDL, project);
        }

        // テストベンチ雛形の VHDL 記述を生成する
        private void GenerateTestBenchVHDL(string project)
        {
            if (!VM.IsProjectValid || project == NewProjectLabel)
                return;
            GenerateVHDL(Properties.Resources.DR_TESTBENCH, FileName.TestBenchVHDL, project);
        }

        // トップ回路またはテストベンチ雛形の VHDL 記述を生成する
        private void GenerateVHDL(string template, string fileName, string project)
        {
            string fullFileName = VM.SourceDirPath + @"\" + project + @"\" + fileName;
            string userCode = GetUserCodeToPreserve(fullFileName);
            bool preserved = false;

            Dictionary<string, List<string>> assignments = GetAssignments();
            string[] strs = template.Replace("\r\n","\n").Split(new[]{ '\n'});
            try
            {
                StreamWriter sw = File.CreateText(fullFileName);
                foreach (string str in strs)
                {
                    if (str.StartsWith("-- USER_COMPONENT"))
                    {
                        // Component 宣言
                        sw.WriteLine("    component " + VM.UserEntity + " is");
                        sw.WriteLine("        port (");
                        int i = 0;
                        foreach (VHDLPort port in VHDLUserPorts)
                        {
                            string sep = (i == VHDLUserPorts.Count - 1) ? ");" : ";";
                            sw.WriteLine("            " + port.ToString() + sep);
                            i += 1;
                        }
                        sw.WriteLine("    end component;");
                    }
                    else if (str.StartsWith("-- USER_SIGNAL"))
                    {
                        // Port に対応する内部信号
                        foreach (VHDLPort port in VHDLUserPorts)
                            sw.WriteLine("    signal " + port.ToString(true) + ";");
                    }
                    else if (str.StartsWith("-- USER_INSTANCE"))
                    {
                        // インスタンス化
                        sw.WriteLine("    usr : " + VM.UserEntity + " port map (");
                        int i = 0;
                        foreach (UserPortItem port in VM.UserPorts)
                        {
                            string target = (port.TopPort != "") ? port.TopPort :
                                            (port.Direction == "Input") ? "'0'" : "open";
                            string sep = (i == VM.UserPorts.Count - 1) ? " );" : ",";
                            sw.WriteLine("        " + port.Name + " => " + target + sep);
                            i += 1;
                        }
                        foreach (var def in ComponentDefaults)
                            if (assignments[def.Key].Count == 0)
                                sw.WriteLine("    " + def.Key + " <= " + def.Value + ";");
                    }
                    else if (str.StartsWith("-- USER_UUT"))
                    {
                        // インスタンス化（テストベンチ用）
                        sw.WriteLine("    uut : " + VM.UserEntity + " port map (");
                        int i = 0;
                        foreach (UserPortItem port in VM.UserPorts)
                        {
                            string sep = (i == VM.UserPorts.Count - 1) ? " );" : ",";
                            sw.WriteLine("        " + port.Name + " => " + port.Name + sep);
                            i += 1;
                        }
                    }
                    else if (str.StartsWith("-- vvv"))
                    {
                        sw.WriteLine(str);
                        if (userCode != "")
                        {
                            sw.Write(userCode);
                            preserved = true;
                        }
                    }
                    else if (str.StartsWith("-- ^^^"))
                    {
                        preserved = false;
                        sw.WriteLine(str);
                    }
                    else if (! preserved)
                    {
                        sw.WriteLine(str);
                    }
                }
                sw.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("VHDL ファイルの作成中にエラーが発生しました．\n" + ex.Message);
                return;
            }
        }

        // 雛形の VHDL 記述からユーザの記述した箇所を抜き出す
        private string GetUserCodeToPreserve(string fullFileName)
        {
            string result = "";
            if (! File.Exists(fullFileName))
                return result;

            try
            {
                StreamReader sr = new StreamReader(fullFileName, Encoding.GetEncoding("ISO-8859-1"));
                string line;
                bool preserve = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.IndexOf("-- ^^^") != -1)
                        preserve = false;
                    if (preserve)
                        result += line + "\n";
                    if (line.IndexOf("-- vvv") != -1)
                        preserve = true;
                }
                sr.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("VHDL ファイルの読込中にエラーが発生しました．\n" + ex.Message);
                return "";
            }
            result = Regex.Replace(result, @"[^\u0000-\u007F]", "?"); // 非ASCII文字は ? にする
            return result;
        }
        
        // トップ回路の VHDL 記述を読んで，割当てを復元する
        private void ReadTopVHDL(string project)
        {
            // エラーがある場合やファイルが存在しない場合はスキップ
            if (! VM.IsProjectValid)
                return;
            if (File.Exists(VM.SourceDirPath + @"\" + FileName.TopVHDL))
                MoveTopVHDL();
            if (! File.Exists(VM.SourceDirPath + @"\" + project + @"\" + FileName.TopVHDL))
                return;

            foreach (VHDLPort port in TopFinder.UserPorts)
                port.ToAssign = "";

            try
            {
                StreamReader sr = new StreamReader(VM.SourceDirPath + @"\" + project + @"\" + FileName.TopVHDL, Encoding.GetEncoding("ISO-8859-1"));
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = Regex.Match(line, @"component ([A-Za-z0-9_]+) is");
                    if (match.Success)
                    {
                        TopFinder.SetTopEntity(match.Groups[1].Value);
                    }
                    match = Regex.Match(line, @"([A-Za-z0-9_\(\)]+) => ([A-Z0-9\(\)]+)");
                    if (match.Success)
                    {
                        string usr = match.Groups[1].Value;
                        string top = match.Groups[2].Value;
                        foreach (VHDLPort port in TopFinder.UserPorts)
                            if (port.Name == usr.ToLower() && ComponentRectangles.ContainsKey(top))
                                port.ToAssign = top;
                    }
                }
                sr.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("トップ回路の VHDL ファイルの読込中にエラーが発生しました．\n" + ex.Message);
                return;
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
            TopEntityFinder newTop = new TopEntityFinder(SourceFileNames);
            if (! newTop.IsValid)
            {
                MsgBox.Warn("更新されたソースファイルに問題があります．\n" + newTop.Problem);
                return false;
            }
            if (newTop.TopEntity != TopFinder.TopEntity)
                if (! newTop.SetTopEntity(TopFinder.TopEntity))
                {
                    MsgBox.Warn("更新されたソースファイルに問題があります．\nエンティティ " + TopFinder.TopEntity + " が見つかりません．");
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
                ReadTopVHDL(VM.CurrentProject);
                UpdateUserPorts();
                GenerateTopVHDL(VM.CurrentProject);
            }
            return true;
        }
    }
}
