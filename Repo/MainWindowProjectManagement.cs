// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2023 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            public bool IsValid, TCLExists, DCPExists, BITExists;

            public VivadoProject(string name, bool isValid, bool tclExists, bool dcpExists, bool bitExists)
            {
                Name = name;
                IsValid   = isValid;
                TCLExists = tclExists;
                DCPExists = dcpExists;
                BITExists = bitExists;
            }
        }

        // プロジェクト一覧を更新
        private void UpdateProjectList()
        {
            if (! VM.IsSourceValid)
            {
                VM.VivadoProjects.Clear();
                VM.CurrentProject = "";
                UpdateProjectStatus(null);
                return;
            }

            List<VivadoProject> projects = EnumerateVivadoProject();
            projects.Add(CheckVivadoProject(NewProjectLabel));

            // 変更がないかどうかチェック
            int idx = -1;
            if (LastSourceDir == VM.SourceDirPath)
            {
                int countSame = 0;
                for (int i = 0; i < projects.Count; i++)
                {
                    if (i >= VM.VivadoProjects.Count)
                        break;
                    if (projects[i].Name == VM.VivadoProjects[i])
                        countSame++;
                    if (projects[i].Name == VM.CurrentProject)
                        idx = i;
                }
                if (countSame != projects.Count)
                    idx = -1;
            }
            LastSourceDir = VM.SourceDirPath;

            // 変更があればリストを更新し，既存プロジェクトの末尾（か新規プロジェクト）を選択
            if (idx == -1)
            {
                ProjectListUpdating = true; // supress event trigger
                VM.VivadoProjects.Clear();
                foreach (VivadoProject project in projects)
                    VM.VivadoProjects.Add(project.Name);
                idx = (projects.Count >= 2) ? projects.Count - 2 : 0;
                VM.CurrentProject = projects[idx].Name;
                ProjectListUpdating = false;
                ReadProjectSettings();
            }
            else
            {
                UpdateProjectStatus(projects[idx]);
            }
        }

        // 現在のプロジェクトに対するボタン操作の有効/無効を更新
        private void UpdateProjectStatus(VivadoProject proj)
        {
            VM.IsNewProjectSelected = (proj != null) && (VM.CurrentProject == NewProjectLabel);
            VM.IsProjectValid = (proj != null) && proj.IsValid;
            VM.IsTCLAvailable = (proj != null) && proj.TCLExists;
            VM.IsDCPAvailable = (proj != null) && proj.DCPExists;
            VM.IsBITAvailable = (proj != null) && proj.BITExists;
            if (proj == null || VM.IsNewProjectSelected)
            {
                VM.UserEntity = "";
                VM.UserPorts.Clear();
                UpdateComponentRectangles();
            }
        }

        // トップ回路の VHDL をもとに，プロジェクトの設定を更新する
        private void ReadProjectSettings()
        {
            UpdateProjectStatus(CheckVivadoProject(VM.CurrentProject));
            if (VM.IsNewProjectSelected)
                return;

            ReadTopVHDL(VM.CurrentProject);
            if (TopFinder.TopEntity == null)
                TopFinder.TopEntity = TopFinder.SuggestedTopEntity;
            UpdateUserPorts();
        }

        // 選択されているユーザ回路をもとに，ポート一覧を更新する
        private void UpdateUserPorts()
        {
            if (VM.IsNewProjectSelected)
                return;

            VM.UserEntity = TopFinder.TopEntity;
            VHDLUserPorts = TopFinder.TopPorts;
            VM.UserPorts.Clear();
            foreach (VHDLPort port in TopFinder.UserPorts)
                VM.UserPorts.Add(new UserPortItem(port.OriginalName, port.Direction, port.ToAssign));
            UpdateComponentRectangles();
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
            if (! VM.IsSourceValid)
                return result;
            if (! sourceDirInfo.Exists)
                return result;

            foreach (DirectoryInfo subDir in sourceDirInfo.GetDirectories())
            {
                VivadoProject newProject = CheckVivadoProject(subDir.Name);
                if (newProject != null)
                    result.Add(newProject);
            }
            result.Sort(new FileNameWithIntPostfixSorter<VivadoProject>());
            return result;
        }

        // 指定されたフォルダが Vivado のプロジェクトかどうかチェックする
        private VivadoProject CheckVivadoProject(string project)
        {
            if (project == NewProjectLabel)
                return new VivadoProject(project, false, false, false, false);
            
            string fullName = VM.SourceDirPath + @"\" + project;
            if (File.Exists(fullName + @"\" + FileName.TopVHDL) ||
                File.Exists(fullName + @"\" + project + ".xpr"))
            {
                bool tcl = (EnumerateVivadoFiles(fullName, "TCLFile").Count != 0);
                bool dcp = (EnumerateVivadoFiles(fullName, "DCPFileExceptBase").Count != 0);
                bool bit = (EnumerateVivadoFiles(fullName, "BITFile").Count != 0);
                return new VivadoProject(project, true, tcl, dcp, bit);
            }
            return null;
        }

        // Vivado バージョンの不一致がある場合，Vivado が作成したファイルを削除する
        private bool CheckProjectVersion(string project)
        {
            List<string> filesByDRFront = new List<string>
            {
                FileName.BaseCheckPoint.ToLower(),
                FileName.TopVHDL.ToLower(),
                FileName.TestBenchVHDL.ToLower(),
                FileName.OpenProjectTCL.ToLower(),
                FileName.BitGenTCL.ToLower(),
                FileName.OpenHWTCL.ToLower(),
                FileName.LogFolder.ToLower()
            };
            string projectVersion = GetProjectVersion(project);
            if (projectVersion != null && VivadoVersion != null && projectVersion != VivadoVersion)
            {
                string warnMessage = "Vivado のバージョンが一致しません．\n"
                    + "この PC の Vivado バージョン: " + VivadoVersion + "\n"
                    + "プロジェクトの Vivado バージョン: " + projectVersion + "\n"
                    + "Vivado が作成したファイルを削除して続行しますか？";
                if (!MsgBox.WarnAndConfirm(warnMessage))
                    return false;

                try
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(VM.SourceDirPath + @"\" + project);
                    foreach (FileInfo file in dirInfo.GetFiles())
                        if (!filesByDRFront.Contains(file.Name.ToLower()))
                            file.Delete();
                    foreach (DirectoryInfo dir in dirInfo.GetDirectories())
                        if (!filesByDRFront.Contains(dir.Name.ToLower()))
                            dir.Delete(true);
                }
                catch (Exception ex)
                {
                    MsgBox.Warn("ファイルの削除中にエラーが発生しました．\n" + ex.Message);
                    return false;
                }
            }
            return true;
        }

        // Vivado が生成したファイルが古くなっていないか調べる
        private bool CheckVivadoFilesStale(List<string> targ, List<string> cmp, string targDescription, string cmpDescription)
        {
            if (GetNewestTimestamp(targ) > GetNewestTimestamp(cmp))
                return true;
            return MsgBox.WarnAndConfirm(targDescription + " が " + cmpDescription + " より古いです．続行しますか？");
        }

        // 最も新しいファイルの更新時間を返す（CheckVivadoFilesStale で使用）
        private DateTime GetNewestTimestamp(List<string> targ)
        {
            DateTime result = DateTime.MinValue;
            foreach (string path in targ)
                if (result < File.GetLastWriteTime(path))
                    result = File.GetLastWriteTime(path);

            return result;
        }

        // 指定されたフォルダにあるプロジェクトの Vivado バージョンを調べる
        private string GetProjectVersion(string project)
        {
            if (project == NewProjectLabel)
                return null;

            string projectFileName = VM.SourceDirPath + @"\" + project + @"\" + project + ".xpr";
            if (!File.Exists(projectFileName))
                return null;

            try
            {
                StreamReader sr = new StreamReader(projectFileName, Encoding.GetEncoding("ISO-8859-1"));
                string result = null;
                for (int i = 0; i < 5; i++) // Product Version is written in first few lines
                {
                    string line = sr.ReadLine();
                    if (line == null)
                        break;
                    Match match = Regex.Match(line, @"Product Version: Vivado v([0-9\.]+)");
                    if (match.Success)
                        result = match.Groups[1].Value;
                }
                sr.Close();
                return result;
            }
            catch (IOException ex)
            {
                MsgBox.Warn("Vivado プロジェクトファイルの読込中にエラーが発生しました．\n" + ex.Message);
                return null;
            }
         }

        // Vivado 関連ファイルの一覧をリストアップする
        private List<string> EnumerateVivadoFiles(string dir, string mode, bool full = false)
        {
            List<string> result = new List<string>();
            string ext = ".*";
            if (mode == "BITFile")
                ext = ".bit";
            else if (mode == "TCLFile")
                ext = ".tcl";
            else if (mode == "DCPFile" || mode == "DCPFileExceptBase")
                ext = ".dcp";

            DirectoryInfo dirInfo = new DirectoryInfo(dir);
            if (dirInfo.Exists)
                foreach (FileInfo file in dirInfo.GetFiles("*" + ext))
                    if (file.Name.EndsWith(ext) &&
                        (mode != "DCPFileExceptBase" || file.Name != FileName.BaseCheckPoint))
                        result.Add((full) ? file.FullName : file.Name);
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
