// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace DRFront
{
    // ■■ ボード画像上に配置される枠に関連するメソッド ■■
    public partial class MainWindow : Window
    {
        // ボード画像上に配置される枠のセットアップ
        private void SetupComponentRects()
        {
            DRFrontBoardDefinition board;
            string boardDir = BaseDir + FileName.BoardsDir + ST.TargetBoardDir + "\\";

            // 初期化処理
            cvsBoard.Children.Clear();
            if (ComponentRectangles == null)
            {
                ComponentRectangles = new Dictionary<string, Rectangle>();
                InputPortList = new List<string>();
                OutputPortList = new List<string>();
            }                
            else
            {
                ComponentRectangles.Clear();
                InputPortList.Clear();
                OutputPortList.Clear();
            }
            InputPortList.Add("");
            OutputPortList.Add("");

            // ボード定義ファイルを読み込む
            try
            {
                XmlSerializer serial = new XmlSerializer(typeof(DRFrontBoardDefinition));
                FileStream fs = new FileStream(boardDir + "board.xml", FileMode.Open);
                board = (DRFrontBoardDefinition)serial.Deserialize(fs);
                fs.Close();
            }
            catch (Exception ex)
            {
                MsgBox.Warn("ボードファイルのロードに失敗しました．\n\n" +
                    "エラー内容: " + ex.Message);
                return;
            }

            ComponentLocations = new Dictionary<string, Rect>();
            ComponentDefaults = new Dictionary<string, string>();

            TargetFPGA = board.TargetFPGA;
            TargetBoardName = board.TargetBoard;
            foreach (DRFrontBoardDefinition.DRFrontBoardComponent comp in board.Components)
            {
                string cname = comp.Name;
                if (ST.PreferredLanguage != "VHDL")
                    cname = cname.Replace('(', '[').Replace(')', ']');
                ComponentLocations.Add(cname, new Rect(comp.Left, comp.Top, comp.Width, comp.Height));
                if (comp.DefaultValue != null)
                {
                    ComponentDefaults.Add(cname, comp.DefaultValue);
                    OutputPortList.Add(cname);
                }
                else
                {
                    InputPortList.Add(cname);
                }
            }
            // 存在しなくなったポートへの割り当てを解除
            foreach (UserPortItem port in VM.UserPorts)
                if (! port.TopPortList.Contains(port.TopPort))
                    port.TopPort = "";

            // 背景画像を表示
            BitmapImage newBMP = new BitmapImage();
            newBMP.BeginInit();
            newBMP.CacheOption = BitmapCacheOption.OnLoad;
            newBMP.UriSource = new Uri(boardDir + board.BoardImage.Path);
            newBMP.EndInit();
            Image newImage = new Image();
            newImage.Source = newBMP;
            newImage.Width = board.BoardImage.Width;
            newImage.Height = board.BoardImage.Height;
            cvsBoard.Children.Add(newImage);

            // 重ね合わせる枠を表示
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

    // ボード定義ファイルの XML に対応したクラス
    public class DRFrontBoardDefinition
    {
        public string TargetFPGA;
        public string TargetBoard;
        public class DRFrontBoardImage
        {
            [XmlAttribute("path")]   public string Path;
            [XmlAttribute("width")]  public int Width;
            [XmlAttribute("height")] public int Height;
        }
        public DRFrontBoardImage BoardImage;
        public class DRFrontBoardComponent
        {
            [XmlAttribute("name")]    public string Name;
            [XmlAttribute("left")]    public int Left;
            [XmlAttribute("top")]     public int Top;
            [XmlAttribute("width")]   public int Width;
            [XmlAttribute("height")]  public int Height;
            [XmlAttribute("default")] public string DefaultValue;
        }
        [XmlElement("Component")] public List<DRFrontBoardComponent> Components;
    }
}
