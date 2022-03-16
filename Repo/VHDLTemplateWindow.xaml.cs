// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;

namespace DRFront
{
    public partial class VHDLTemplateWindow : Window
    {
        VHDLTemplateViewModel VM;
        
        // ■■ VHDL のテンプレートを作成するためのウィンドウ ■■
        public VHDLTemplateWindow()
        {
            InitializeComponent();
            VM = new VHDLTemplateViewModel();
            DataContext = VM;
            VM.TemplatePorts.Add(new TemplatePortItem());

            grdTemplate.AllowDrag = false;
        }

        // 保存ボタンが押されたとき
        private void SaveVHDL_Click(object sender, RoutedEventArgs e)
        {
            // 1つでも名前や幅に問題があるときはスキップ
            string problem = "";
            foreach (TemplatePortItem port in VM.TemplatePorts)
                if (! (port.ValidName && port.ValidWidth))
                    problem = "信号名または幅の指定に問題があります．";
            if (! VM.ValidEntityName)
                problem = "エンティティ名の指定に問題があります．";
                
            if (problem != "")
            {
                MsgBox.Warn(problem);
                return;
            }

            // 保存先を指定してもらう
            VistaSaveFileDialog dialog = new VistaSaveFileDialog();
            dialog.Filter = "VHDL files (*.vhdl, *.vhd)|*.vhdl;*.vhd|All files (*.*)|*.*";
            dialog.DefaultExt = "vhdl";
            if (! (bool) dialog.ShowDialog())
                return;
            
            // VHDL ファイルを自動生成
            string[] strs = Properties.Resources.DR_TEMPLATE.Replace("\r\n","\n").Split(new[]{ '\n'});
            try
            {
                StreamWriter sw = File.CreateText(dialog.FileName);
                foreach (string str in strs)
                {
                    if (str.StartsWith("-- USER_PORT"))
                    {
                        // Port 宣言
                        sw.WriteLine("    port (");
                        int i = 0;
                        foreach (TemplatePortItem port in VM.TemplatePorts)
                        {
                            string sep = (i == VM.TemplatePorts.Count - 1) ? ");" : ";";
                            sw.WriteLine("        " + port.ToVHDLPort().ToString() + sep);
                            i += 1;
                        }
                    }
                    else
                    {
                        sw.WriteLine(str.Replace("DR_ENTITY_NAME", VM.EntityName));
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

        // 閉じるボタンがクリックされたとき
        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 行の追加ボタンがクリックされたとき
        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            int pos = grdTemplate.SelectedIndex;
            pos = (pos != -1) ? pos + 1 : VM.TemplatePorts.Count;
            VM.TemplatePorts.Insert(pos, new TemplatePortItem());
        }

        // 行の削除ボタンがクリックされたとき
        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (grdTemplate.SelectedIndex == -1)
                return;
            VM.TemplatePorts.RemoveAt(grdTemplate.SelectedIndex);
        }
    }
}
