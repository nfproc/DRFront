// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//
// Some code in this file are derived from "GGFront: A GHDL/GTKWave GUI Frontend"
// Copyright (C) 2018-2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DRFront
{
    // Entity に関する情報の基底クラス
    public abstract class HDLEntity
    {
        public string Name, OriginalName;
        public string ShortPath;

        public HDLEntity(string path, string originalName)
        {
            Name = originalName;
            OriginalName = originalName;
            ShortPath = path;
        }
    }

    // Component に関する情報の基底クラス
    public abstract class HDLComponent
    {
        public string Name, From;

        public HDLComponent(string name, string from)
        {
            Name = name;
            From = from;
        }

        public override bool Equals(object obj)
        {
            return Name == ((HDLComponent) obj).Name && From == ((HDLComponent) obj).From;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ From.GetHashCode();
        }
    }
    // Port に関する情報の基底クラス
    public abstract class HDLPort
    {
        public string Name, OriginalName, Direction, ToAssign;
        public bool IsVector;
        public int Upper, Lower;

        public HDLPort(string originalName, string direction, int upper = -1, int lower = -1)
        {
            Name = originalName;
            OriginalName = originalName;
            Direction = direction;
            ToAssign = "";
            IsVector = (upper != -1);
            Upper = upper;
            Lower = lower;
        }

        public HDLPort(TemplatePortItem port)
        {
            int intWidth = int.Parse(port.Width);
            Name = port.Name;
            OriginalName = port.Name;
            Direction = port.Direction + "put";
            ToAssign = "";
            IsVector = (intWidth != 1);
            Upper = (intWidth == 1) ? -1 : intWidth - 1;
            Lower = (intWidth == 1) ? -1 : 0;
        }

        public abstract List<HDLPort> ToVector();
    }

    // ソースコードの解析結果の基底クラス
    public abstract class HDLSource
    {
        public bool IsValid;
        public string Problem;
        public List<HDLEntity> Entities;
        public List<HDLComponent> Components;
        public Dictionary<string, List<HDLPort>> Ports;

        public HDLSource()
        {
            Entities = new List<HDLEntity>();
            Components = new List<HDLComponent>();
            Ports = new Dictionary<string, List<HDLPort>>();
        }

        public HDLSource(HDLEntity entity, List<HDLPort> port) : this()
        {
            Entities.Add(entity);
            if (port != null)
                Ports.Add(entity.Name, port);
        }

        public abstract void Read(string sourceName);
        public abstract void ReadTop(string sourceName);
        public abstract void Generate(string template, string fullFileName, IList<UserPortItem> UserPorts, Dictionary<string, string> UnusedPorts = null);
        public abstract void Generate(string template, string fullFileName, IList<TemplatePortItem> TemplatePorts);


        // 雛形の HDL 記述からユーザの記述した箇所を抜き出す
        protected static string GetUserCodeToPreserve(string fullFileName)
        {
            string result = "";
            if (!File.Exists(fullFileName))
                return result;

            try
            {
                StreamReader sr = new StreamReader(fullFileName, Encoding.GetEncoding("ISO-8859-1"));
                string line;
                bool preserve = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.IndexOf("-- ^^^") != -1 || line.IndexOf("// ^^^") != -1)
                        preserve = false;
                    if (preserve)
                        result += line + "\n";
                    if (line.IndexOf("-- vvv") != -1 || line.IndexOf("// vvv") != -1)
                        preserve = true;
                }
                sr.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("HDL ファイルの読込中にエラーが発生しました．\n" + ex.Message);
                return "";
            }
            result = Regex.Replace(result, @"[^\u0000-\u007F]", "?"); // 非ASCII文字は ? にする
            return result;
        }
    }

    // Entity の参照関係の解析結果を保持するクラス
    public class EntityHierarchyItem
    {
        public int Level { get; set; }
        public string Name { get; set; }
        public string ShortPath { get; set; }
        public bool IsTop { get; set; }
    }

    // Entity の階層関係を作成するクラス
    public class TopEntityFinder
    {
        public bool IsValid = false;
        public string TopEntity, Problem;
        public string SuggestedTopEntity;
        public List<HDLPort> TopPorts, UserPorts;

        public List<EntityHierarchyItem> ListItems;

        private List<HDLSource> Sources;
        private List<HDLEntity> Entities;
        private List<HDLComponent> Components;
        private Dictionary<string, List<HDLPort>> Ports;

        public TopEntityFinder(List<string> files, string language, string extPath = null)
        {
            string EntityTerm = (language == "VHDL") ? "エンティティ" : "モジュール";

            // 各 HDL ファイルを解析
            Sources = new List<HDLSource>();
            int invalidSource = 0;
            foreach (string fileName in files)
            {
                HDLSource newSource;
                if (language == "VHDL")
                    newSource = new VHDLSource();
                else
                    newSource = new VerilogSource(extPath);
                newSource.Read(fileName);
                Sources.Add(newSource);
                if (!newSource.IsValid)
                {
                    invalidSource += 1;
                    Problem = newSource.Problem;
                }
            }
            if (invalidSource >= 2)
                Problem = invalidSource + "個のソースファイルに問題があります．";
            if (invalidSource != 0)
                return;

            // Entity, Component 宣言を数え上げる
            Entities = new List<HDLEntity>();
            Components = new List<HDLComponent>();
            Ports = new Dictionary<string, List<HDLPort>>();
            foreach (HDLSource src in Sources)
            {
                foreach (HDLEntity entity in src.Entities)
                {
                    if (Entities.Exists(x => x.Name == entity.Name))
                    {
                        Problem = EntityTerm + " " + entity.OriginalName + " が重複しています．";
                        return;
                    }
                    // 入出力がない場合はテストベンチ扱いとして除外
                    if (src.Ports[entity.Name].Count != 0)
                    {
                        Entities.Add(entity);
                        Ports.Add(entity.Name, src.Ports[entity.Name]);
                    }
                }
            }
            foreach (HDLSource src in Sources)
                foreach (HDLComponent component in src.Components)
                    if (Entities.Exists(x => x.Name == component.From))
                        Components.Add(component);

            if (Entities.Count == 0)
            {
                Problem = "回路の " + EntityTerm + " が見つかりません．";
                return;
            }

            // 他から参照されていない Entity のうち，含まれる回路が最も多いものを抽出
            List<List<EntityHierarchyItem>> trees = new List<List<EntityHierarchyItem>>();
            foreach (HDLEntity entity in Entities)
            {
                if (!Components.Exists(x => x.Name == entity.Name))
                {
                    List<EntityHierarchyItem> tree = SearchEntityTree(entity.Name, new List<string>());
                    if (tree == null) // エラーを検出した時点で止める
                    {
                        trees.Clear();
                        break;
                    }
                    trees.Add(tree);
                }
            }
            if (trees.Count == 0)
            {
                Problem = EntityTerm + " " + "が循環参照されています．";
                return;
            }
            trees.Sort((a, b) => b.Count - a.Count);
            SuggestedTopEntity = trees[0][0].Name;
            SetTopEntity(SuggestedTopEntity);
            IsValid = true;

            // 参照関係を1次元リスト化
            ListItems = new List<EntityHierarchyItem>();
            foreach (List<EntityHierarchyItem> tree in trees)
                ListItems.AddRange(tree);
        }

        // ユーザ回路のトップを変更し，入出力ポートの一覧を更新する
        public bool SetTopEntity(string top, bool preserveAssignment = false)
        {
            if (!Ports.ContainsKey(top))
                return false;
            TopEntity = top;
            TopPorts = Ports[TopEntity];
            TopPorts.Sort((a, b) => a.Name.CompareTo(b.Name));

            Dictionary<string, string> oldAssignments = new Dictionary<string, string>();
            if (preserveAssignment && UserPorts != null)
                foreach (HDLPort port in UserPorts)
                    oldAssignments[port.Name] = port.ToAssign;

            UserPorts = new List<HDLPort>();
            foreach (HDLPort port in TopPorts)
                foreach (HDLPort vport in port.ToVector())
                    AddUserPorts(vport, oldAssignments);
            return true;
        }

        // 入出力ポートの一覧にポートを追加
        private void AddUserPorts(HDLPort newPort, Dictionary<string, string> oldAssignments)
        {
            if (oldAssignments.ContainsKey(newPort.Name))
                newPort.ToAssign = oldAssignments[newPort.Name];
            UserPorts.Add(newPort);
        }

        // target 以下に含まれる回路の数を返す関数
        private List<EntityHierarchyItem> SearchEntityTree(string target, List<string> parents)
        {
            List<EntityHierarchyItem> result = new List<EntityHierarchyItem>();
            if (parents.Contains(target))  // 循環参照の場合エラー
                return null;
            HDLEntity targetEntity = Entities.Find(x => x.Name == target);
            if (targetEntity == null) // Entity 宣言がない場合はノーカン
                return result;

            result.Add(new EntityHierarchyItem
            {
                Level = 0,
                Name = targetEntity.Name,
                ShortPath = targetEntity.ShortPath,
                IsTop = false
            });
            List<string> newParents = new List<string>(parents);
            newParents.Add(target);
            foreach (HDLComponent component in Components)
                if (component.From == target)
                {
                    List<EntityHierarchyItem> children = SearchEntityTree(component.Name, newParents);
                    if (children == null)
                        return null;
                    foreach (EntityHierarchyItem child in children)
                        child.Level += 1;
                    result.AddRange(children);
                }
            return result;
        }
    }
}
