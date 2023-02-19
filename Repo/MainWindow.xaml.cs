// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2023 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Threading;
using System.Security.Cryptography;
using System;
using Ookii.Dialogs.Wpf;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DRFront
{
    // ■■ イベントハンドラとユーティリティメソッド ■■
    public partial class MainWindow : Window
    {
        private MainViewModel VM;
        private TopEntityFinder TopFinder;
        private Dictionary<string, Rect> ComponentLocations;
        private Dictionary<string, Rectangle> ComponentRectangles;
        private Dictionary<string, string> ComponentDefaults;
        private List<VHDLPort> VHDLUserPorts;
        private string SelectedRectangleName = "";
        private bool ProjectListUpdating = false;

        private string DRFrontVersion;
        private string VivadoVersion;
        private DateTime VivadoLastLaunched;
        private const string VivadoRootPath = @"C:\Xilinx\Vivado\";
        private List<string> SourceFileNames;
        private const string NewProjectLabel = "(New Project)";
        private string LastSourceDir = "";
        private DispatcherTimer updateTimer;

        private static class FileName
        {
            public const string BaseCheckPoint = "base.dcp";
            public const string UserCheckPoint = "__checkpoint.dcp";
            public const string TopVHDL = "dr_top.vhdl";
            public const string TestBenchVHDL = "dr_testbench.vhdl";
            public const string OpenProjectTCL = "OpenProject.tcl";
            public const string BitGenTCL = "GenerateBitstream.tcl";
            public const string OpenHWTCL = "OpenHW.tcl";
            public const string LogFolder = "logs";
        }

        public MainWindow()
        {
            InitializeComponent();
            SetupComponentRects();

            VM = new MainViewModel();
            DataContext = VM;

            SourceFileNames = new List<string>();
            updateTimer = new DispatcherTimer();
            updateTimer.Tick += UpdateTimer_Timer;
            updateTimer.Interval = new TimeSpan(0, 0, 3);

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            DRFrontVersion = Regex.Replace(version, @".[0-9]+$", "");

            VivadoVersion = CheckVivadoVersion();
            if (VivadoVersion == null)
                MsgBox.Warn("Vivado が見つかりませんでした．\nVivado を使用する機能は動作しません．");
        }

        // ソースディレクトリが変更されたとき（新しいディレクトリに対してチェックを行う）
        private void SourceDir_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).GetBindingExpression(TextBox.TextProperty).UpdateSource();
            CheckSourceDirectory();
        }

        // ソースディレクトリの選択ボタンが押されたとき
        private void FindSourceDir_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = "VHDL ソースファイルを含むフォルダを選択";
            dialog.UseDescriptionForTitle = true;
            if (Directory.Exists(VM.SourceDirPath))
                dialog.SelectedPath = VM.SourceDirPath;
            if ((bool) dialog.ShowDialog())
                VM.SourceDirPath = dialog.SelectedPath;
        }

        // ソースディレクトリを再チェックするボタンが押されたとき
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CheckSourceDirectory();

        }

        // プロジェクトのドロップダウンを変更したとき
        private void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (! ProjectListUpdating)
                ReadProjectSettings();
        }

        // プロジェクトを作成するボタンが押されたとき
        private void CreateProject_Click(object sender, RoutedEventArgs e)
        {
            if (VM.CurrentProject != NewProjectLabel || ! VM.IsSourceValid)
                return;

            string project = GetNewProjectName();
            Directory.CreateDirectory(VM.SourceDirPath + @"\" + project);
            Directory.CreateDirectory(VM.SourceDirPath + @"\" + project + @"\" + FileName.LogFolder);
            VM.CurrentProject = project;
            VM.IsNewProjectSelected = false;
            VM.IsProjectValid = true;
            TopFinder.SetTopEntity(TopFinder.SuggestedTopEntity);
            UpdateUserPorts();
            GenerateTopVHDL(project);
            UpdateProjectList();
        }

        // トップモジュールを変更するボタンが押されたとき
        private void SelectTop_Click(object sender, RoutedEventArgs e)
        {
            TopSelectorWindow win = new TopSelectorWindow();
            foreach (EntityHierarchyItem item in TopFinder.ListItems)
            {
                item.IsTop = (item.Name == VM.UserEntity);
                win.HierarchyCollection.Add(item);
            }
            win.Owner = GetWindow(this);
            win.ShowDialog();
            if (win.ReturnValue != null)
            {
                TopFinder.SetTopEntity(win.ReturnValue, true);
                UpdateUserPorts();
                GenerateTopVHDL(VM.CurrentProject);
            }
         }

        // VHDL テンプレート作成画面を表示するボタンが押されたとき
        private void VHDLTemplate_Click(object sender, RoutedEventArgs e)
        {
            VHDLTemplateWindow win = new VHDLTemplateWindow();
            win.Owner = GetWindow(this);
            win.Show();
        }

        // ピンの自動割当てボタンが押されたとき
        private void AutoAssign_Click(object sender, RoutedEventArgs e)
        {
            if (! MsgBox.WarnAndConfirm("未割当てのピンを LD や SW に割当てます．続けますか？"))
                return;
            
            Dictionary<string, List<string>> assign = GetAssignments();
            List<string> clockNames = new List<string> { "CK", "CLK", "CLOCK", "ICK", "ICLK", "ICLOCK" };
            List<string> resetNames = new List<string> { "RST", "RESET", "IRST", "IRESET" };
            bool clockAssigned = false, resetAssigned = false;
            foreach (var port in VM.UserPorts)
            {
                if (port.TopPort != "")
                    continue;
                if (clockNames.Contains(port.Name.ToUpper()) && assign["CLK"].Count == 0)
                {
                    port.TopPort = "CLK";
                    assign["CLK"].Add(port.Name);
                    clockAssigned = true;
                    continue;
                }
                if (resetNames.Contains(port.Name.ToUpper()) && assign["RST"].Count == 0)
                {
                    port.TopPort = "RST";
                    assign["RST"].Add(port.Name);
                    resetAssigned = true;
                    continue;
                }
                foreach (var con in assign)
                {
                    if ((con.Key.StartsWith("LD") || con.Key.StartsWith("SW")) &&
                        con.Value.Count == 0 &&
                        port.TopPortList.Contains(con.Key))
                    {
                        port.TopPort = con.Key;
                        con.Value.Add(port.Name);
                        break;
                    }
                }
            }

            UpdateComponentRectangles();
            GenerateTopVHDL(VM.CurrentProject);

            if (clockAssigned || resetAssigned)
            {
                string sig = (clockAssigned && resetAssigned) ? "CLK および RST" :
                             (clockAssigned) ? "CLK" : "RST";
                MsgBox.Info("ボード上の " + sig + " へ自動割当てを行いました．\n意図した割当てになっているか確認してください．");
            }
        }

        // 割当てを初期化するボタンが押されたとき
        private void ResetAssign_Click(object sender, RoutedEventArgs e)
        {
            if (! MsgBox.WarnAndConfirm("全ての割当てが削除されます．続けますか？"))
                return;
            foreach (var port in VM.UserPorts)
                port.TopPort = "";
            UpdateComponentRectangles();
            GenerateTopVHDL(VM.CurrentProject);
        }

        // プロジェクトごとに必要なファイルを作成/更新するボタンが押されたとき
        private void UpdateFiles_Click(object sender, RoutedEventArgs e)
        {
            if (! CheckProjectVersion(VM.CurrentProject))
                return;

            // ログを保存するフォルダ（なければ作成）
            if (!Directory.Exists(VM.SourceDirPath + @"\" + VM.CurrentProject + @"\logs"))
                Directory.CreateDirectory(VM.SourceDirPath + @"\" + VM.CurrentProject + @"\logs");

            // テストベンチ
            GenerateTestBenchVHDL(VM.CurrentProject);

            // プロジェクトを開く Tcl スクリプト
            Dictionary<string, string> argsOpen = new Dictionary<string, string>();
            string escapedSource = "";
            foreach (string fileName in SourceFileNames)
                escapedSource += "../" + (new FileInfo(fileName).Name.Replace(" ", @"\ ")) + " ";
            escapedSource += "./" + FileName.TopVHDL;

            argsOpen.Add("project_name", VM.CurrentProject);
            argsOpen.Add("source_files", "{" + escapedSource + "}");
            argsOpen.Add("testbench_file", FileName.TestBenchVHDL);
            PrepareTcl(VM.CurrentProject, FileName.OpenProjectTCL, Properties.Resources.OPEN_PROJECT, argsOpen);

            // ビットストリームを生成する Tcl スクリプト
            Dictionary<string, string> argsGen = new Dictionary<string, string>();

            argsGen.Add("project_name", VM.CurrentProject);
            argsGen.Add("checkpoint_base", FileName.BaseCheckPoint);
            argsGen.Add("checkpoint_proj", FileName.UserCheckPoint);
            PrepareTcl(VM.CurrentProject, FileName.BitGenTCL, Properties.Resources.GENERATE_BITSTREAM, argsGen);

            // ハードウェアを開く Tcl スクリプト
            PrepareTcl(VM.CurrentProject, FileName.OpenHWTCL, Properties.Resources.OPEN_HARDWARE);

            // ベース設計のコピー
            string dcpBase = GetBaseCheckpointName();
            if (dcpBase == "")
            {
                MsgBox.Warn("ベース設計のチェックポイントファイルが見つかりません．");
                return;
            }

            string projectDir = VM.SourceDirPath + @"\" + VM.CurrentProject;
            string checkPointPath = projectDir + @"\" + FileName.BaseCheckPoint;
            try
            {
                File.Copy(dcpBase, checkPointPath, true);
            }
            catch (Exception ex)
            {
                if ((ex is UnauthorizedAccessException) && (CompareFileHash(dcpBase, checkPointPath)))
                {
                    MsgBox.Warn("ベース設計のチェックポイントファイルのコピーに失敗しましたが，同一ファイルが既に存在するので，このまま続けます．");
                }
                else
                {
                    MsgBox.Warn("ベース設計のチェックポイントファイルのコピーに失敗しました．");
                    return;
                }
            }
            MsgBox.Info("Vivado 用のスクリプトや，テストベンチの雛形を作成・更新しました．");
        }

        // Vivado を実行するボタンが押されたとき
        private void RunVivado_Click(object sender, RoutedEventArgs e)
        {
            string tclFile = null;
            string senderName = ((Button)sender).Name;
            bool useBatchMode = (senderName == "btnBitGen");
            if (senderName == "btnOpenProject")
                tclFile = FileName.OpenProjectTCL;
            else if (senderName == "btnBitGen")
                tclFile = FileName.BitGenTCL;
            else if (senderName == "btnOpenHW")
                tclFile = FileName.OpenHWTCL;
            else
                return;

            if (! CheckForLaunchVivado())
                return;
            if (! CheckTclVersion(VM.CurrentProject,tclFile))
                return;

            LaunchVivado(VM.CurrentProject, tclFile, useBatchMode);
        }

        // ピン配置をコンボボックスから切り替えたとき
        private void AssignTo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            if (cmb.IsDropDownOpen || cmb.IsKeyboardFocused) // UIから選択を切り替えた時のみ処理
            {
                UpdateComponentRectangles();
                GenerateTopVHDL(VM.CurrentProject);
            }
        }

        // ボード画像上にドラッグ&ドロップを行うとき
        private void Board_DragEnter(object sender, DragEventArgs e)
        {
            Board_DragOver(sender, e);
        }

        private void Board_DragLeave(object sender, DragEventArgs e)
        {
            SelectedRectangleName = "";
            UpdateComponentRectangles();
        }

        private void Board_DragOver(object sender, DragEventArgs e)
        {
            if (sender is Canvas canvas && e.Data.GetData(typeof(UserPortItem)) is UserPortItem item)
                SelectedRectangleName = GetTargetRect(e.GetPosition(canvas), item.TopPortList);
            else
                SelectedRectangleName = "";
            e.Effects = (SelectedRectangleName == "") ? DragDropEffects.None : DragDropEffects.Link;
            UpdateComponentRectangles();
        }

        private void Board_Drop(object sender, DragEventArgs e)
        {
            Board_DragOver(sender, e);
            if (SelectedRectangleName == "")
                return;
            UserPortItem item = e.Data.GetData(typeof(UserPortItem)) as UserPortItem;
            item.TopPort = SelectedRectangleName;
            SelectedRectangleName = "";
            UpdateComponentRectangles();
            GenerateTopVHDL(VM.CurrentProject);
        }

        // プロジェクトの状況を監視するタイマが作動したとき
        public void UpdateTimer_Timer(object sender, EventArgs e)
        {
            UpdateProjectList();
        }
        
        // コンボボックスの無用なスクロール防止
        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (! (Keyboard.IsKeyDown(Key.Down) || Keyboard.IsKeyDown(Key.Up)))
                e.Handled = true;
        }

        // ファイルの一致確認
        private bool CompareFileHash(string path1, string path2)
        {
            try
            {
                HashAlgorithm hash = new SHA1CryptoServiceProvider();
                FileStream fs1 = new FileStream(path1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                FileStream fs2 = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                string bs1 = BitConverter.ToString(hash.ComputeHash(fs1));
                string bs2 = BitConverter.ToString(hash.ComputeHash(fs2));
                return bs1 == bs2;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}