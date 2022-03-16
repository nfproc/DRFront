// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
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
            VM.IsSourcesValid = false;
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
                return;
            }
            if (!Directory.Exists(VM.SourceDirPath))
            {
                VM.SourceProblem = "無効または存在しないディレクトリです．";
                return;
            }

            // VHDL ファイルを含むかどうか確認
            SourceFileNames.Clear();
            foreach (string file in Directory.GetFiles(VM.SourceDirPath, "*.vhd"))
                if (file.ToLower().EndsWith(".vhd"))
                    SourceFileNames.Add(file);
            foreach (string file in Directory.GetFiles(VM.SourceDirPath, "*.vhdl"))
                if (! file.ToLower().EndsWith(TopVHDLFileName) && file.ToLower().EndsWith(".vhdl"))
                    SourceFileNames.Add(file);
            if (SourceFileNames.Count == 0)
            {
                VM.SourceProblem = "ディレクトリ内に VHDL ファイルが見つかりません．";
                return;
            }

            // VHDL ファイルを順番に解析 (See VHDLSources.cs)
            TopEntityFinder top = new TopEntityFinder(SourceFileNames);
            if (!top.IsValid)
            {
                VM.SourceProblem = top.Problem;
                return;
            }

            // 解析結果をウィンドウに反映
            VM.IsSourcesValid = true;
            VM.UserEntity = top.TopEntity;
            VHDLUserPorts = top.TopPorts;
            foreach (VHDLPort port in VHDLUserPorts)
                if (port.IsVector)
                    for (int i = port.Lower; i <= port.Upper; i += 1)
                        VM.UserPorts.Add(new UserPortItem(port.OriginalName + "(" + i + ")", port.Direction));
                else
                    VM.UserPorts.Add(new UserPortItem(port.OriginalName, port.Direction));
            ReadTopVHDL();
            UpdateComponentRectangles();

            // プロジェクトリストの更新
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
                if (port.TopPort != "")
                    result[port.TopPort].Add(port.Name);

            return result;
        }

        // トップ回路の VHDL 記述を生成する
        private void GenerateTopVHDL()
        {
            // エラー・重複がある場合は生成しない
            if (!VM.IsSourcesValid)
                return;
            Dictionary<string, List<string>> assignments = GetAssignments();
            foreach (var assignment in assignments)
                if (assignment.Value.Count >= 2)
                    return;

            GenerateVHDL(Properties.Resources.DR_TOP, TopVHDLFileName);
        }

        // テストベンチ雛形の VHDL 記述を生成する
        private void GenerateTestBenchVHDL(string project)
        {
            if (!VM.IsSourcesValid)
                return;
            GenerateVHDL(Properties.Resources.DR_TESTBENCH, TestBenchVHDLFileName, project);
        }

        // トップ回路またはテストベンチ雛形の VHDL 記述を生成する
        private void GenerateVHDL(string template, string fileName, string project = null)
        {
            string fullFileName = (VM.SourceDirPath + @"\" +
                                   ((project == null) ? "" : project + @"\") + fileName);
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
                    else
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
        
        // トップ回路の VHDL 記述を読んで，割当てを復元する
        private void ReadTopVHDL()
        {
            // エラーがある場合やファイルが存在しない場合はスキップ
            if (! VM.IsSourcesValid)
                return;
            if (! File.Exists(VM.SourceDirPath + @"\" + TopVHDLFileName))
                return;
            
            try
            {
                StreamReader sr = new StreamReader(VM.SourceDirPath + @"\" + TopVHDLFileName, Encoding.GetEncoding("ISO-8859-1"));
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = Regex.Match(line, @"([A-Za-z0-9_\(\)]+) => ([A-Z0-9\(\)]+)");
                    if (match.Success)
                    {
                        string usr = match.Groups[1].Value;
                        string top = match.Groups[2].Value;
                        foreach (UserPortItem port in VM.UserPorts)
                            if (port.Name == usr && port.TopPortList.Contains(top))
                                port.TopPort = top;
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
    }
}
