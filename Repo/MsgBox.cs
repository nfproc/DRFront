// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.Windows;

namespace DRFront
{
    // ■■ メッセージボックス表示のためのユーティリティクラス ■■
    public static class MsgBox
    {
        public static void Info(string Message)
        {
            MessageBox.Show(Message, "情報", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Warn(string Message)
        {
            MessageBox.Show(Message, "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static bool WarnAndConfirm(string Message)
        {
            MessageBoxResult result = MessageBox.Show(Message, "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return (result == MessageBoxResult.Yes);
        }
    }
}
