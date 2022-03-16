// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DRFront
{
    // ■■ ボード画像上に配置される枠に関連するメソッド ■■
    public partial class MainWindow : Window
    {
        // ボード画像上に配置される枠のセットアップ
        private void SetupComponentRects()
        {
            ComponentLocations = new Dictionary<string, Rect>();
            ComponentDefaults = new Dictionary<string, string>();
            for (int i = 0; i < 16; i += 1)
            {
                ComponentLocations.Add("SW(" + i + ")", new Rect(538 - 33.5 * i, 256, 30, 60));
                ComponentLocations.Add("LD(" + i + ")", new Rect(525 - 32   * i, 216, 30, 25));
                ComponentDefaults.Add ("LD(" + i + ")", "'0'");
            }
            for (int i = 0; i < 4; i += 1)
            {
                ComponentLocations.Add("AN(" +  i      + ")", new Rect(390 - 41 * i, 166, 38, 50));
                ComponentLocations.Add("AN(" + (i + 4) + ")", new Rect(220 - 41 * i, 166, 38, 50));
            }
            for (int i = 0; i < 8; i += 1)
                ComponentDefaults.Add ("AN(" +  i + ")", "'1'");

            ComponentLocations.Add("CLK",  new Rect(219,  18, 32, 32));
            ComponentLocations.Add("RST",  new Rect(349,  18, 32, 32));
            ComponentLocations.Add("CA",   new Rect(120,  17, 60, 20));
            ComponentLocations.Add("CB",   new Rect(150,  38, 30, 32));
            ComponentLocations.Add("CC",   new Rect(140,  92, 30, 32));
            ComponentLocations.Add("CD",   new Rect(100, 125, 58, 20));
            ComponentLocations.Add("CE",   new Rect(100,  92, 30, 32));
            ComponentLocations.Add("CF",   new Rect(110,  38, 30, 32));
            ComponentLocations.Add("CG",   new Rect(110,  71, 60, 20));
            ComponentLocations.Add("DP",   new Rect(160, 125, 20, 20));
            ComponentLocations.Add("BTNU", new Rect(448,  40, 40, 40));
            ComponentLocations.Add("BTNL", new Rect(403,  84, 40, 40));
            ComponentLocations.Add("BTNC", new Rect(448,  84, 40, 40));
            ComponentLocations.Add("BTNR", new Rect(493,  84, 40, 40));
            ComponentLocations.Add("BTND", new Rect(448, 128, 40, 40));

            foreach (string port in new List<string> { "CA", "CB", "CC", "CD", "CE", "CF", "CG", "DP" })
                ComponentDefaults.Add(port, "'1'");

            ComponentRectangles = new Dictionary<string, Rectangle>();
            SolidColorBrush fillRect = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            foreach (var loc in ComponentLocations)
            {
                Rectangle newRect = new Rectangle();
                newRect.Width = loc.Value.Width;
                newRect.Height = loc.Value.Height;
                Canvas.SetTop(newRect, loc.Value.Top);
                Canvas.SetLeft(newRect, loc.Value.Left);
                newRect.Stroke = Brushes.Gray;
                newRect.StrokeThickness = 1;
                newRect.Fill = fillRect;
                ComponentRectangles.Add(loc.Key, newRect);
                cvsBoard.Children.Add(newRect);
            }
        }

        // ボード画像上に配置される枠の色を更新する
        private void UpdateComponentRectangles()
        {
            Dictionary<string, List<string>> assign = GetAssignments();
            foreach (var rect in ComponentRectangles)
            {
                if (assign[rect.Key].Count >= 2)
                {
                    rect.Value.Stroke = Brushes.Red;
                    rect.Value.StrokeThickness = 3;
                    rect.Value.ToolTip = rect.Key + ": !! Multiple Ports !!";
                }
                else if (assign[rect.Key].Count == 1)
                {
                    rect.Value.Stroke = Brushes.Cyan;
                    rect.Value.StrokeThickness = 2;
                    rect.Value.ToolTip = rect.Key + ": " + assign[rect.Key][0];
                }
                else
                {
                    rect.Value.Stroke = Brushes.Gray;
                    rect.Value.StrokeThickness = 1;
                    rect.Value.ToolTip = rect.Key + ": (None)";
                }
                if (rect.Key == SelectedRectangleName)
                {
                    rect.Value.Stroke = Brushes.Yellow;
                    rect.Value.StrokeThickness = 3;

                }
            }
        }
        
        // ボード画像上の枠の中にマウスカーソルがあれば，対応するトップ回路のポート名を返す
        private string GetTargetRect(Point point, List<string> portList)
        {
            foreach (var loc in ComponentLocations)
                if (portList.Contains(loc.Key) && loc.Value.Contains(point))
                    return loc.Key;
            return "";
        }
    }
}
