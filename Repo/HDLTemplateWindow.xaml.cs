// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.Windows;
using Ookii.Dialogs.Wpf;

namespace DRFront
{
    public partial class HDLTemplateWindow : Window
    {
        private HDLTemplateViewModel VM;
        private string PreferredLanguage;
        
        // ■■ HDL のテンプレートを作成するためのウィンドウ ■■
        public HDLTemplateWindow(string language)
        {
            InitializeComponent();
            PreferredLanguage = language;
            VM = new HDLTemplateViewModel(language);
            DataContext = VM;
            VM.TemplatePorts.Add(new TemplatePortItem(language));

            grdTemplate.AllowDrag = false;
        }

        // 保存ボタンが押されたとき
        private void SaveHDL_Click(object sender, RoutedEventArgs e)
        {
            // 1つでも名前や幅に問題があるときはスキップ
            string problem = "";
            foreach (TemplatePortItem port in VM.TemplatePorts)
                if (! (port.ValidName && port.ValidWidth))
                    problem = "信号名または幅の指定に問題があります．";
            if (! VM.ValidEntityName)
                problem = "エンティティ/モジュール名の指定に問題があります．";
                
            if (problem != "")
            {
                MsgBox.Warn(problem);
                return;
            }

            // 保存先を指定してもらう
            VistaSaveFileDialog dialog = new VistaSaveFileDialog();
            if (PreferredLanguage == "VHDL")
            {
                dialog.Filter = "VHDL files (*.vhdl, *.vhd)|*.vhdl;*.vhd|All files (*.*)|*.*";
                dialog.DefaultExt = "vhdl";
            }
            else
            {
                dialog.Filter = "Verilog/SystemVerilog files (*.v, *.sv)|*.v;*.sv|All files (*.*)|*.*";
                dialog.DefaultExt = "sv";
            }
            if (! (bool) dialog.ShowDialog())
                return;

            // HDL ファイルを自動生成
            HDLEntity ent;
            HDLSource src;
            string template;
            if (PreferredLanguage == "VHDL")
            {
                ent = new VHDLEntity(dialog.FileName, VM.EntityName);
                src = new VHDLSource(ent, null);
                template = Properties.Resources.DR_TEMPLATE;
            }
            else
            {
                ent = new VerilogEntity(dialog.FileName, VM.EntityName);
                src = new VerilogSource(ent, null);
                template = Properties.Resources.DR_TEMPLATE_V;
            }
            src.Generate(template, dialog.FileName, VM.TemplatePorts);
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
            VM.TemplatePorts.Insert(pos, new TemplatePortItem(PreferredLanguage));
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
