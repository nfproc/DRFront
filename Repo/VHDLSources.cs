// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022 Naoki FUJIEDA. New BSD License is applied.
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

        public VHDLEntity(string originalName)
        {
            Name = originalName.ToLower();
            OriginalName = originalName;
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
        public string Name, OriginalName, Direction;
        public bool IsVector;
        public int Upper, Lower;

        public VHDLPort(string originalName, string direction, int upper = -1, int lower = -1)
        {
            Name = originalName.ToLower();
            OriginalName = originalName;
            Direction = direction;
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
                        VHDLEntity newEntity = new VHDLEntity(match.Groups[1].Value);
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

    // Entity の階層関係を作成するクラス
    public class TopEntityFinder
    {
        public bool IsValid = false;
        public string TopEntity, Problem;
        public List<VHDLPort> TopPorts;

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
            int maxChildren = -1;
            foreach (VHDLEntity entity in Entities)
            {
                if (!Components.Exists(x => x.Name == entity.Name))
                {
                    int numChildren = SearchEntityTree(entity.Name, new List<string>());
                    if (numChildren == -1) // エラーを検出した時点で止める
                    {
                        maxChildren = -1;
                        break;
                    }
                    else if (numChildren > maxChildren)
                    {
                        maxChildren = numChildren;
                        TopEntity = entity.OriginalName;
                    }
                }
            }
            if (maxChildren == -1)
            {
                Problem = "エンティティが循環参照されています．";
                return;
            }
            TopPorts = Ports[TopEntity.ToLower()];
            TopPorts.Sort((a, b) => a.Name.CompareTo(b.Name));
            IsValid = true;
        }

        // target 以下に含まれる回路の数を返す関数
        private int SearchEntityTree (string target, List<string> parents)
        {
            int numChildren = 1;
            if (parents.Contains(target))  // 循環参照の場合エラー
                return -1;
            if (! Entities.Exists(x => x.Name == target)) // Entity 宣言がない場合はノーカン
                return 0;
            List<string> newParents = new List<string>(parents);
            newParents.Add(target);
            foreach (VHDLComponent component in Components)
                if (component.From == target)
                {
                    int newChildren = SearchEntityTree(component.Name, newParents);
                    if (newChildren == -1)
                        return -1;
                    numChildren += newChildren;
                }
            return numChildren;
        }
    }
}