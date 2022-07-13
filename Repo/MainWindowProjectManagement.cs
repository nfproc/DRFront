// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace DRFront
{
    // ■■ Vivado プロジェクトの管理に関するメソッド ■■
    public partial class MainWindow : Window
    {
        private class VivadoProject
        {
            public string Name;
            public bool DCPExists, BITExists;

            public VivadoProject(string name, bool dcpExists, bool bitExists)
            {
                Name = name;
                DCPExists = dcpExists;
                BITExists = bitExists;
            }
        }

        // プロジェクト一覧およびボタンの有効/無効を更新
        private void UpdateProjectList()
        {
            int lastProjectCount = VM.VivadoProjects.Count;
            string lastSelection = VM.CurrentProject;
            VM.VivadoProjects.Clear();
            VM.CurrentProject = "";

            if (! VM.IsSourcesValid)
                return;

            List<VivadoProject> projects = EnumerateVivadoProject();
            projects.Add(new VivadoProject(NewProjectLabel, false, false));
            projects.Add(new VivadoProject(NewSimulationLabel, false, false));
            VM.VivadoProjects.Clear();
            foreach (VivadoProject project in projects)
                VM.VivadoProjects.Add(project.Name);

            int lastIndex = VM.VivadoProjects.IndexOf(lastSelection);
            if (LastSourceDir == VM.SourceDirPath &&
                lastProjectCount == VM.VivadoProjects.Count && lastIndex != -1)
            {
                // 前回と変更なし
                VM.CurrentProject = lastSelection;
                VM.IsDCPAvailable = projects[lastIndex].DCPExists;
                VM.IsBITAvailable = projects[lastIndex].BITExists;
            }
            else
            {
                // それ以外は既存プロジェクトの末尾（ない場合は新規プロジェクト）
                int idx = (projects.Count >= 3) ? projects.Count - 3 : 0;
                VM.CurrentProject = projects[idx].Name;
                VM.IsDCPAvailable = projects[idx].DCPExists;
                VM.IsBITAvailable = projects[idx].BITExists;
            }
            LastSourceDir = VM.SourceDirPath;
        }

        // プロジェクトを新規作成するために，適当な連番のプロジェクト名を返す
        private string GetNewProjectName(string prefix = "project")
        {
            int maxProjectNumber = 0;
            foreach (VivadoProject proj in EnumerateVivadoProject())
            {
                Match match = Regex.Match(proj.Name, prefix + @"_(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int projectNumber = int.Parse(match.Groups[1].Value);
                    if (maxProjectNumber < projectNumber)
                        maxProjectNumber = projectNumber;
                }
            }
            return prefix + "_" + (maxProjectNumber + 1);
        }

        // Vivado のプロジェクトの一覧をリストアップする
        private List<VivadoProject> EnumerateVivadoProject()
        {
            List<VivadoProject> result = new List<VivadoProject>();
            DirectoryInfo sourceDirInfo = new DirectoryInfo(VM.SourceDirPath);
            if (! VM.IsSourcesValid)
                return result;
            if (! sourceDirInfo.Exists)
                return result;

            foreach (DirectoryInfo subDir in sourceDirInfo.GetDirectories())
            {
                if (File.Exists(subDir.FullName + @"\" + subDir.Name + ".xpr"))
                {
                    bool dcp = (EnumerateVivadoFiles(subDir.FullName, "DCPFileExceptBase").Count != 0);
                    bool bit = (EnumerateVivadoFiles(subDir.FullName, "BITFile").Count != 0);
                    result.Add(new VivadoProject(subDir.Name, dcp, bit));
                }
            }
            result.Sort(new FileNameWithIntPostfixSorter<VivadoProject>());
            return result;
        }

        // Vivado 関連ファイルの一覧をリストアップする
        private List<string> EnumerateVivadoFiles(string dir, string mode)
        {
            List<string> result = new List<string>();
            string ext = ".*";
            if (mode == "BITFile")
                ext = ".bit";
            else if (mode == "DCPFile" || mode == "DCPFileExceptBase")
                ext = ".dcp";

            DirectoryInfo dirInfo = new DirectoryInfo(dir);
            if (dirInfo.Exists)
                foreach (FileInfo file in dirInfo.GetFiles("*" + ext))
                    if (file.Name.EndsWith(ext) &&
                        (mode != "DCPFileExceptBase" || file.Name != BaseCheckPointFileName))
                        result.Add(file.Name);
            return result;
        }

        // ベースデザインの dcp ファイルのある場所を返す
        private string GetBaseCheckpointName()
        {
            string baseDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + @"\";
            List<string> baseNames = EnumerateVivadoFiles(baseDir, "DCPFile");

            if (baseNames.Count == 0)
                return "";
            if (baseNames.Count > 1)
                if (! MsgBox.WarnAndConfirm("ベース設計のチェックポイントファイルが複数見つかりました．\n" +
                    baseNames[baseNames.Count - 1] + "を使用します．\n続行しますか？"))
                    return "";

            return baseDir + baseNames[baseNames.Count - 1];
        }

        // プロジェクトの dcp ファイルのある場所を返す（複数ある場合は1つに絞る）
        private string GetCheckpointName()
        {
            const string defaultName = "__checkpoint.dcp";
            string projectDir = VM.SourceDirPath + @"\" + VM.CurrentProject;

            if (! VM.IsSourcesValid)
                return "";

            List<string> checkpointNames = EnumerateVivadoFiles(projectDir, "DCPFileExceptBase");

            if (checkpointNames.Count == 0)
                return "";
            if (checkpointNames.Count == 1)
                return checkpointNames[0];
            checkpointNames.Sort(new FileNameWithIntPostfixSorter<string>());
            string lastName = checkpointNames[checkpointNames.Count - 1];

            if (! MsgBox.WarnAndConfirm("チェックポイントファイルが複数見つかりました．\n" +
                lastName + " だけを残し，ファイル名を " + defaultName + "に変更します．\n続行しますか？"))
                return "";

            for (int i = 0; i < checkpointNames.Count - 1; i += 1)
                File.Delete(projectDir + @"\" + checkpointNames[i]);
            File.Move(projectDir + @"\" + lastName, projectDir + @"\" + defaultName);
            return defaultName;
        }

        // プロジェクトや dcp が複数ある場合の名前比較用メソッドを実装するクラス
        public class FileNameWithIntPostfixSorter<T> : IComparer<T>
        {
            public int Compare(T a, T b)
            {
                string aval = ((a is string) ? a : a.GetType().GetField("Name").GetValue(a)) as string;
                string bval = ((b is string) ? b : b.GetType().GetField("Name").GetValue(b)) as string;
                Match am = Regex.Match(aval, @"(\d+)(\....)?$");
                Match bm = Regex.Match(bval, @"(\d+)(\....)?$");
                if (! (am.Success && bm.Success))
                    return aval.CompareTo(bval);
                string astr = aval.Substring(0, aval.Length - am.Groups[0].Length);
                string bstr = bval.Substring(0, bval.Length - bm.Groups[0].Length);
                if (astr != bstr)
                    return astr.CompareTo(bstr);
                int aint = int.Parse(am.Groups[1].Value);
                int bint = int.Parse(bm.Groups[1].Value);
                return aint - bint; 
            }
        }
    }
}
