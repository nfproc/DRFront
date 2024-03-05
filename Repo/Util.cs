// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace DRFront
{
    // ■■ 複数クラスから利用されるメソッドを保持するユーティリティクラス ■■
    public static class Util
    {
        public const string VivadoRootPathDefault = @"C:\Xilinx\Vivado\";
        public const string TargetBoardDirDefault = "Nexys A7-100T";
        public const string PreferredLanguageDefault = "VHDL";

        // root ディレクトリに対し，ファイル fileName を持つサブディレクトリの一覧を返す
        public static List<string> GetSubDirs(string root, string fileName)
        {
            DirectoryInfo rootInfo = new DirectoryInfo(root);
            List<string> subDirs = new List<string>();
            if (rootInfo.Exists)
            {
                foreach (DirectoryInfo subDir in rootInfo.GetDirectories())
                    if (subDir.GetFiles(fileName).Length != 0)
                        subDirs.Add(subDir.Name);
            }
            subDirs.Sort();
            return subDirs;
        }

        // Vivado のバージョンチェックを行う
        public static List<string> GetVivadoVersions(string root)
        {
            return GetSubDirs(root, "settings64.bat");
        }

        public static string GetLatestVivadoVersion(string root)
        {
            List<string> versions = GetVivadoVersions(root);
            if (versions != null)
                return versions[versions.Count - 1];
            else
                return null;
        }
    }

    // ■■ 設定ファイルに対応するクラス ■■
    public class DRFrontSettings
    {
        public string DRFrontVersion;
        public string VivadoRootPath;
        public string VivadoVersion;
        public string TargetBoardDir;
        public string PreferredLanguage;
        private bool disableSaveSettings;

        public const string DRFrontCurrentDataVersion = "0.4";

        public DRFrontSettings()
        {
            DRFrontVersion = DRFrontCurrentDataVersion;
            disableSaveSettings = false;
        }

        private void Reset()
        {
            VivadoRootPath = Util.VivadoRootPathDefault;
            VivadoVersion = Util.GetLatestVivadoVersion(VivadoRootPath);
            TargetBoardDir = Util.TargetBoardDirDefault;
            PreferredLanguage = Util.PreferredLanguageDefault;
        }

        public bool Load(string fileName)
        {
            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(DRFrontSettings));
                FileStream fs = new FileStream(fileName, FileMode.Open);
                DRFrontSettings newSettings = (DRFrontSettings) ser.Deserialize(fs);

                VivadoRootPath = newSettings.VivadoRootPath;
                VivadoVersion = newSettings.VivadoVersion;
                TargetBoardDir = newSettings.TargetBoardDir;
                PreferredLanguage = newSettings.PreferredLanguage;
                fs.Close();
            }
            catch (FileNotFoundException)
            {
                Reset();
                Save(fileName);
                return false;
            }
            catch (InvalidOperationException)
            {
                MsgBox.Warn("設定ファイルのロードに失敗しました．形式が正しくありません．");
                Reset();
                return false;
            }
            catch (Exception ex)
            {
                MsgBox.Warn("設定ファイルのロード中にエラーが発生しました．\n" +
                    "設定ファイルを保存しません．\n\nエラー内容: " + ex.Message);
                Reset();
                disableSaveSettings = true;
                return false;
            }
            return true;
        }

        public void Save(string fileName)
        {
            if (disableSaveSettings)
                return;
            try
            {
                XmlSerializer serial = new XmlSerializer(typeof(DRFrontSettings));
                FileStream fs = new FileStream(fileName, FileMode.Create);
                serial.Serialize(fs, this);
                fs.Close();
            }
            catch (Exception ex)
            {
                MsgBox.Warn("設定ファイルのセーブ中にエラーが発生しました．\n" +
                    "これ以降，設定ファイルを保存しません．\n\nエラー内容: " + ex.Message);
                disableSaveSettings = true;
            }
        }
    }
}
