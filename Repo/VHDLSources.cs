// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2023 Naoki FUJIEDA. New BSD License is applied.
//
// Code in this file are derived from "GGFront: A GHDL/GTKWave GUI Frontend"
// Copyright (C) 2018-2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DRFront
{
    // Entity に関する情報のクラス
    public class VHDLEntity
    {
        public string Name, OriginalName;
        public string ShortPath;

        public VHDLEntity(string path, string originalName)
        {
            Name = originalName.ToLower();
            OriginalName = originalName;
            ShortPath = path;
        }
    }

    // Component に関する情報のクラス
    public class VHDLComponent
    {
        public string Name, From;

        public VHDLComponent(string name, string from)
        {
            Name = name;
            From = from;
        }
    };

    // Port に関する情報を保持するクラス
    public class VHDLPort
    {
        public string Name, OriginalName, Direction, ToAssign;
        public bool IsVector;
        public int Upper, Lower;

        public VHDLPort(string originalName, string direction, int upper = -1, int lower = -1)
        {
            Name = originalName.ToLower();
            OriginalName = originalName;
            Direction = direction;
            ToAssign = "";
            IsVector = (upper != -1);
            Upper = upper;
            Lower = lower;
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool forSignal)
        {
            string dir = (forSignal) ? "" : Direction.Substring(0, Direction.Length - 3) + " ";
            string type = "std_logic" + ((IsVector) ? "_vector(" + Upper + " downto " + Lower + ")" : "");
            return OriginalName + " : " + dir + type;
        }
    }
    // ソースコードの解析結果を保持するクラス
    public class VHDLSource
    {
        public bool IsValid;
        public string Problem;
        public List<VHDLEntity> Entities;
        public List<VHDLComponent> Components;
        public Dictionary<string, List<VHDLPort>> Ports;

        // コンストラクタ: ソースファイルの解析を行う
        public VHDLSource(string sourceName)
        {
            string shortPath = Path.GetFileName(sourceName);
            Entities = new List<VHDLEntity>();
            Components = new List<VHDLComponent>();
            Ports = new Dictionary<string, List<VHDLPort>>();
            try
            {
                StreamReader sr = new StreamReader(sourceName, Encoding.GetEncoding("ISO-8859-1"));
                Match match;
                string line;
                string currentEntity = "";
                string currentArchitecture = "";
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.IndexOf("--") != -1)
                        line = line.Substring(0, line.IndexOf("--"));

                    // ■ entity NAME_OF_ENTITY is
                    match = Regex.Match(line, @"entity\s+([a-z0-9_]+)\s+is", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        VHDLEntity newEntity = new VHDLEntity(shortPath, match.Groups[1].Value);
                        currentEntity = newEntity.Name;
                        currentArchitecture = "";
                        Entities.Add(newEntity);
                        if (! Ports.ContainsKey(currentEntity))
                            Ports.Add(currentEntity, new List<VHDLPort>());
                    }

                    if (currentEntity != "" && currentArchitecture == "")
                    {
                        // ◆ end [entity] NAME_OF_ENTITY
                        match = Regex.Match(line, @"end(\s+entity)?\s+" + currentEntity, RegexOptions.IgnoreCase);
                        if (match.Success)
                            currentEntity = "";

                        // ◆ PORT_NAME[, PORT_NAME2, ...]: [in|out] std_logic[_vector]
                        //                          1           2       3                    4                   5                6                7
                        match = Regex.Match(line, @"([a-z0-9_]+)(\s*,\s*([a-z0-9_]+))*\s*:\s*(in|out)\s+std_logic(_vector\s*\(\s*(\d+)\s+downto\s+(\d+)\s*\))?",
                                            RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string originalName = match.Groups[1].Value;
                            string direction = (match.Groups[4].Value.ToLower() == "in") ? "Input" : "Output";
                            int lower = -1, upper = -1;
                            if (match.Groups[5].Value != "")
                            {
                                lower = int.Parse(match.Groups[7].Value);
                                upper = int.Parse(match.Groups[6].Value);
                            }
                            Ports[currentEntity].Add(new VHDLPort(originalName, direction, upper, lower));
                            foreach (Capture cap in match.Groups[3].Captures)
                            {
                                originalName = cap.Value;
                                Ports[currentEntity].Add(new VHDLPort(originalName, direction, upper, lower));
                            }
                        }
                    }

                    // ■ architecture NAME_OF_ARCHITECTURE of NAME_OF_ENTITY is
                    match = Regex.Match(line, @"architecture\s+([a-z0-9_]+)\s+of\s+([a-z0-9_]+)\s+is", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        currentEntity = match.Groups[2].Value.ToLower();
                        currentArchitecture = match.Groups[1].Value.ToLower();

                    }
                    if (currentArchitecture != "")
                    {
                        // ◆ end [architecture] NAME_OF_ARCHITECTURE
                        match = Regex.Match(line, @"end(\s+architecture)?\s" + currentArchitecture, RegexOptions.IgnoreCase);
                        if (match.Success)
                            currentEntity = currentArchitecture = "";

                        // ◆ component NAME_OF_COMPONENT is
                        match = Regex.Match(line, @"component\s+([a-z0-9_]+)\s+is", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            VHDLComponent newComponent = new VHDLComponent(match.Groups[1].Value.ToLower(), currentEntity);
                            if (!Components.Contains(newComponent))
                                Components.Add(newComponent);
                        }
                    }
                }
                sr.Close();
                IsValid = true;
            }
            catch (IOException)
            {
                Problem = $"ソースファイル {sourceName} の読み込みに失敗";
                IsValid = false;
            }
            catch (Exception ex)
            {
                Problem = "VHDLソース読み込み中に予期せぬエラーが発生:\n" + ex.ToString();
                IsValid = false;
            }
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
        public List<VHDLPort> TopPorts, UserPorts;

        public List<EntityHierarchyItem> ListItems;

        private List<VHDLSource> Sources;
        private List<VHDLEntity> Entities;
        private List<VHDLComponent> Components;
        private Dictionary<string, List<VHDLPort>> Ports;

        public TopEntityFinder(List<string> files)
        {
            // 各 VHDL ファイルを解析
            Sources = new List<VHDLSource>();
            int invalidSource = 0;
            foreach (string fileName in files)
            {
                VHDLSource newSource = new VHDLSource(fileName);
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
            Entities = new List<VHDLEntity>();
            Components = new List<VHDLComponent>();
            Ports = new Dictionary<string, List<VHDLPort>>();
            foreach (VHDLSource src in Sources)
            {
                foreach (VHDLEntity entity in src.Entities)
                {
                    if (Entities.Exists(x => x.Name == entity.Name))
                    {
                        Problem = "エンティティ " + entity.OriginalName + " が重複しています．";
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
            foreach (VHDLSource src in Sources)
                foreach (VHDLComponent component in src.Components)
                    if (Entities.Exists(x => x.Name == component.From))
                        Components.Add(component);

            if (Entities.Count == 0)
            {
                Problem = "回路のエンティティが見つかりません．";
                return;
            }

            // 他から参照されていない Entity のうち，含まれる回路が最も多いものを抽出
            List<List<EntityHierarchyItem>> trees = new List<List<EntityHierarchyItem>>();
            foreach (VHDLEntity entity in Entities)
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
                Problem = "エンティティが循環参照されています．";
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
            if (! Ports.ContainsKey(top.ToLower()))
                return false;
            TopEntity = top;
            TopPorts = Ports[TopEntity.ToLower()];
            TopPorts.Sort((a, b) => a.Name.CompareTo(b.Name));

            Dictionary<string, string> oldAssignments = new Dictionary<string, string>();
            if (preserveAssignment && UserPorts != null)
                foreach (VHDLPort port in UserPorts)
                    oldAssignments[port.Name] = port.ToAssign;

            UserPorts = new List<VHDLPort>();
            foreach (VHDLPort port in TopPorts)
                if (port.IsVector)
                    for (int i = port.Lower; i <= port.Upper; i += 1)
                        AddUserPorts(new VHDLPort(port.OriginalName + "(" + i + ")", port.Direction), oldAssignments);
                else
                    AddUserPorts(new VHDLPort(port.OriginalName, port.Direction), oldAssignments);
            return true;
        }

        // 入出力ポートの一覧にポートを追加
        private void AddUserPorts(VHDLPort newPort, Dictionary<string, string> oldAssignments)
        {
            if (oldAssignments.ContainsKey(newPort.Name))
                newPort.ToAssign = oldAssignments[newPort.Name];
            UserPorts.Add(newPort);
        }

        // target 以下に含まれる回路の数を返す関数
        private List<EntityHierarchyItem> SearchEntityTree (string target, List<string> parents)
        {
            List<EntityHierarchyItem> result = new List<EntityHierarchyItem>();
            if (parents.Contains(target))  // 循環参照の場合エラー
                return null;
            VHDLEntity targetEntity = Entities.Find(x => x.Name == target);
            if (targetEntity == null) // Entity 宣言がない場合はノーカン
                return result;

            result.Add(new EntityHierarchyItem
            {
                Level = 0,
                Name = targetEntity.OriginalName,
                ShortPath = targetEntity.ShortPath,
                IsTop = false
            });
            List<string> newParents = new List<string>(parents);
            newParents.Add(target);
            foreach (VHDLComponent component in Components)
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