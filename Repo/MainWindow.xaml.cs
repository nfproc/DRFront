// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
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

namespace DRFront
{
    // ■■ イベントハンドラとユーティリティメソッド ■■
    public partial class MainWindow : Window
    {
        private MainViewModel VM;
        private Dictionary<string, Rect> ComponentLocations;
        private Dictionary<string, Rectangle> ComponentRectangles;
        private Dictionary<string, string> ComponentDefaults;
        private List<VHDLPort> VHDLUserPorts;
        private string SelectedRectangleName = "";

        private string VivadoVersion;
        private DateTime VivadoLastLaunched;
        private const string VivadoRootPath = @"C:\Xilinx\Vivado\";
        private List<string> SourceFileNames;
        private const string BaseCheckPointFileName = "base.dcp";
        private const string TopVHDLFileName = "dr_top.vhdl";
        private const string TestBenchVHDLFileName = "dr_testbench.vhdl";
        private const string NewProjectLabel = "(New Project)";
        private const string NewSimulationLabel = "(New Simulation)";
        private string LastSourceDir = "";
        private DispatcherTimer updateTimer;

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

        // VHDL テンプレート作成画面を表示するボタンが押されたとき
        private void VHDLTemplate_Click(object sender, RoutedEventArgs e)
        {
            VHDLTemplateWindow win = new VHDLTemplateWindow();
            win.Show();
        }

        // ピンの自動割当てボタンが押されたとき
        private void AutoAssign_Click(object sender, RoutedEventArgs e)
        {
            if (! MsgBox.WarnAndConfirm("未割当てのピンを LD や SW に割当てます．続けますか？"))
                return;
            
            Dictionary<string, List<string>> assign = GetAssignments();
            List<string> clockNames = new List<string> { "CK", "CLK", "CLOCK" };
            List<string> resetNames = new List<string> { "RST", "RESET" };
            foreach (var port in VM.UserPorts)
            {
                if (port.TopPort != "")
                    continue;
                if (clockNames.Contains(port.Name.ToUpper()) && assign["CLK"].Count == 0)
                {
                    port.TopPort = "CLK";
                    assign["CLK"].Add(port.Name);
                    continue;
                }
                if (resetNames.Contains(port.Name.ToUpper()) && assign["RST"].Count == 0)
                {
                    port.TopPort = "RST";
                    assign["RST"].Add(port.Name);
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
            GenerateTopVHDL();        
        }

        // 割当てを初期化するボタンが押されたとき
        private void ResetAssign_Click(object sender, RoutedEventArgs e)
        {
            if (! MsgBox.WarnAndConfirm("全ての割当てが削除されます．続けますか？"))
                return;
            foreach (var port in VM.UserPorts)
                port.TopPort = "";
            UpdateComponentRectangles();
            GenerateTopVHDL();
        }

        // Vivado のプロジェクトを新規作成，または開くボタンが押されたとき
        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            const string tclFile = "OpenProject.tcl";
            if (! CheckForLaunchVivado())
                return;

            Dictionary<string, string> args = new Dictionary<string, string>();
            string template, project;

            string escapedSource = "";
            int i = 0;
            foreach (string fileName in SourceFileNames)
            {
                string sep = (i == SourceFileNames.Count - 1) ? "" : " ";
                escapedSource += "../" + (new FileInfo(fileName).Name.Replace(" ", @"\ ")) + sep;
                i += 1;
            }

            if (VM.CurrentProject == NewProjectLabel)
            {
                project = GetNewProjectName();
                template = Properties.Resources.CREATE_PROJECT;
                Directory.CreateDirectory(VM.SourceDirPath + @"\" + project);
                escapedSource += " ../" + TopVHDLFileName;
            }
            else if (VM.CurrentProject == NewSimulationLabel)
            {
                project = GetNewProjectName("simulation");
                template = Properties.Resources.CREATE_SIMULATION;
                Directory.CreateDirectory(VM.SourceDirPath + @"\" + project);
                GenerateTestBenchVHDL(project);
            }
            else
            {
                project = VM.CurrentProject;
                template = Properties.Resources.OPEN_PROJECT;
            }

            args.Add("project_name", project);
            args.Add("source_files", "{" + escapedSource + "}");
            args.Add("testbench_file", TestBenchVHDLFileName);
            if (! PrepareTcl(project, tclFile, template, args))
                return;
            LaunchVivado(project, tclFile);
        }

        // Vivado で配置配線を行うボタンが押されたとき
        private void RunPAR_Click(object sender, RoutedEventArgs e)
        {
            const string tclFile = "GenerateBitstream.tcl";
            if (! CheckForLaunchVivado())
                return;

            Dictionary<string, string> args = new Dictionary<string, string>();
            string dcpFile = GetCheckpointName();
            string dcpBase = GetBaseCheckpointName();
            if (dcpFile == "" || dcpBase == "")
            {
                MsgBox.Warn("チェックポイントファイルが見つかりませんでした．");
                return;
            }

            string projectDir = VM.SourceDirPath + @"\" + VM.CurrentProject;
            string checkPointPath = projectDir + @"\" + BaseCheckPointFileName;
            try
            {
                File.Copy(dcpBase, checkPointPath, true);
            }
            catch (UnauthorizedAccessException)
            {
                if (CompareFileHash(dcpBase, checkPointPath))
                {
                    MsgBox.Warn("ベース設計のチェックポイントファイルのコピーに失敗しましたが，同一ファイルが既に存在するので，このまま続けます．");
                }
                else
                {
                    MsgBox.Warn("ベース設計のチェックポイントファイルのコピーに失敗しました．");
                    return;
                }
            }
            catch (Exception)
            {
                MsgBox.Warn("ベース設計のチェックポイントファイルのコピーに失敗しました．");
                return;
            }

            args.Add("checkpoint_base", BaseCheckPointFileName);
            args.Add("checkpoint_proj", dcpFile);
            args.Add("project_name", VM.CurrentProject);
            if (! PrepareTcl(VM.CurrentProject, tclFile, Properties.Resources.GENERATE_BITSTREAM, args))
                return;
            LaunchVivado(VM.CurrentProject, tclFile, true);

        }

        // Vivado でハードウェアマネージャーを開くボタンが押されたとき
        private void OpenHW_Click(object sender, RoutedEventArgs e)
        {
            const string tclFile = "OpenHW.tcl";
            if (! CheckForLaunchVivado())
                return;
            if (! PrepareTcl(VM.CurrentProject, tclFile, Properties.Resources.OPEN_HARDWARE))
                return;
            LaunchVivado(VM.CurrentProject, tclFile);
        }

        // ピン配置をコンボボックスから切り替えたとき
        private void AssignTo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            if (cmb.IsDropDownOpen || cmb.IsKeyboardFocused) // UIから選択を切り替えた時のみ処理
            {
                UpdateComponentRectangles();
                GenerateTopVHDL();
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
            GenerateTopVHDL();
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