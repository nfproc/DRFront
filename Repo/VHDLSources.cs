// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//
// Some code in this file are derived from "GGFront: A GHDL/GTKWave GUI Frontend"
// Copyright (C) 2018-2022 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DRFront
{
    // Entity に関する情報のクラス
    public class VHDLEntity : HDLEntity
    {
        public VHDLEntity(string path, string originalName) : base(path, originalName)
        {
            Name = Name.ToLower();
        }
    }

    // Component に関する情報のクラス
    public class VHDLComponent : HDLComponent
    {
        public VHDLComponent(string name, string from) : base(name, from) {}
    };

    // Port に関する情報を保持するクラス
    public class VHDLPort : HDLPort
    {
        public VHDLPort(string originalName, string direction, int upper = -1, int lower = -1)
            : base(originalName, direction, upper, lower)
        {
            Name = Name.ToLower();
        }

        public VHDLPort(TemplatePortItem port) : base(port) { }

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

        public override List<HDLPort> ToVector()
        {
            List<HDLPort> result = new List<HDLPort>();
            if (IsVector)
                for (int i = Lower; i <= Upper; i += 1)
                    result.Add(new VHDLPort(OriginalName + "(" + i + ")", Direction));
            else
                result.Add(new VHDLPort(OriginalName, Direction));
            return result;
        }
    }

    // ソースコードの解析結果を保持するクラス
    public class VHDLSource : HDLSource
    {
        public VHDLSource() : base() { }
        public VHDLSource(HDLEntity entity, List<HDLPort> port) : base(entity, port) { }

        // Read メソッド: 一般のソースファイルの解析を行う
        public override void Read(string sourceName)
        {
            string shortPath = Path.GetFileName(sourceName);
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
                            Ports.Add(currentEntity, new List<HDLPort>());
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

        // ReadTop メソッド: 自動生成されたトップ回路の解析を行う
        public override void ReadTop(string sourceName)
        {
            try
            {
                StreamReader sr = new StreamReader(sourceName, Encoding.GetEncoding("ISO-8859-1"));
                List<HDLPort> topPorts = new List<HDLPort>(); 
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    Match match = Regex.Match(line, @"component ([A-Za-z0-9_]+) is");
                    if (match.Success)
                    {
                        Components.Add(new VHDLComponent(match.Groups[1].Value, "top"));
                    }
                    match = Regex.Match(line, @"([A-Za-z0-9_\(\)]+) => ([A-Z0-9\(\)]+)");
                    if (match.Success)
                    {
                        VHDLPort port = new VHDLPort(match.Groups[1].Value, "");
                        port.ToAssign = match.Groups[2].Value;
                        topPorts.Add(port);
                    }
                }
                Ports.Add("top", topPorts);
                sr.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("トップ回路の VHDL ファイルの読込中にエラーが発生しました．\n" + ex.Message);
                return;
            }
        }

        // Generate メソッド(1): トップ回路・テストベンチの生成
        public override void Generate(string template, string fullFileName, IList<UserPortItem> UserPorts, Dictionary<string, string> UnusedPorts = null)
        {
            string userCode = GetUserCodeToPreserve(fullFileName);
            string userEntity = Entities[0].OriginalName;
            List<HDLPort> HDLPorts = Ports[Entities[0].Name];
            bool preserved = false;

            string[] strs = template.Replace("\r\n", "\n").Split(new[] { '\n' });
            try
            {
                StreamWriter sw = File.CreateText(fullFileName);
                foreach (string str in strs)
                {
                    if (str.StartsWith("-- USER_COMPONENT"))
                    {
                        // Component 宣言
                        sw.WriteLine("    component " + userEntity + " is");
                        sw.WriteLine("        port (");
                        int i = 0;
                        foreach (HDLPort port in HDLPorts)
                        {
                            string sep = (i == HDLPorts.Count - 1) ? ");" : ";";
                            sw.WriteLine("            " + port.ToString() + sep);
                            i += 1;
                        }
                        sw.WriteLine("    end component;");
                    }
                    else if (str.StartsWith("-- USER_SIGNAL"))
                    {
                        // Port に対応する内部信号
                        foreach (HDLPort port in HDLPorts)
                            if (port is VHDLPort vport)
                                sw.WriteLine("    signal " + vport.ToString(true) + ";");
                    }
                    else if (str.StartsWith("-- USER_INSTANCE"))
                    {
                        // インスタンス化
                        sw.WriteLine("    usr : " + userEntity + " port map (");
                        int i = 0;
                        foreach (UserPortItem port in UserPorts)
                        {
                            string target = (port.TopPort != "") ? port.TopPort :
                                            (port.Direction == "Input") ? "'0'" : "open";
                            string sep = (i == UserPorts.Count - 1) ? " );" : ",";
                            sw.WriteLine("        " + port.Name + " => " + target + sep);
                            i += 1;
                        }
                        foreach (var def in UnusedPorts)
                            sw.WriteLine("    " + def.Key + " <= " + def.Value + ";");
                    }
                    else if (str.StartsWith("-- USER_UUT"))
                    {
                        // インスタンス化（テストベンチ用）
                        sw.WriteLine("    uut : " + userEntity + " port map (");
                        int i = 0;
                        foreach (UserPortItem port in UserPorts)
                        {
                            string sep = (i == UserPorts.Count - 1) ? " );" : ",";
                            sw.WriteLine("        " + port.Name + " => " + port.Name + sep);
                            i += 1;
                        }
                    }
                    else if (str.StartsWith("-- vvv"))
                    {
                        sw.WriteLine(str);
                        if (userCode != "")
                        {
                            sw.Write(userCode);
                            preserved = true;
                        }
                    }
                    else if (str.StartsWith("-- ^^^"))
                    {
                        preserved = false;
                        sw.WriteLine(str);
                    }
                    else if (!preserved)
                    {
                        sw.WriteLine(str);
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

        // Generate メソッド(2): ユーザ回路の雛形を生成
        public override void Generate(string template, string fullFileName, IList<TemplatePortItem> TemplatePorts)
        {
            string[] strs = template.Replace("\r\n", "\n").Split(new[] { '\n' });
            try
            {
                StreamWriter sw = File.CreateText(fullFileName);
                foreach (string str in strs)
                {
                    if (str.StartsWith("-- USER_PORT"))
                    {
                        // Port 宣言
                        sw.WriteLine("    port (");
                        int i = 0;
                        foreach (TemplatePortItem port in TemplatePorts)
                        {
                            string sep = (i == TemplatePorts.Count - 1) ? ");" : ";";
                            sw.WriteLine("        " + (new VHDLPort(port)).ToString() + sep);
                            i += 1;
                        }
                    }
                    else
                    {
                        sw.WriteLine(str.Replace("DR_ENTITY_NAME", Entities[0].OriginalName));
                    }
                }
                sw.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("トップ回路の VHDL ファイルの作成中にエラーが発生しました．\n" + ex.Message);
                return;
            }
        }
    }

    // VHDL の信号名チェックのための静的クラス
    public static class VHDLNameChecker
    {
        private static List<string> reservedNames = new List<string>
        {
            "abs", "access", "after", "alias", "all", "and", "architecture", "array", "assert",
            "attribute", "begin", "block", "body", "buffer", "bus", "case", "component",
            "configuration", "constant", "disconnect", "downto", "else", "elsif", "end",
            "entity", "exit", "file", "for", "function", "generate", "generic", "group",
            "guarded", "if", "impure", "in", "inertial", "inout", "is", "label", "library",
            "linkage", "literal", "loop", "map", "mod", "nand", "new", "next", "nor", "not",
            "null", "of", "on", "open", "or", "others", "out", "package", "port", "postponed",
            "procedure", "process", "pure", "range", "record", "register", "reject", "rem",
            "report", "return", "rol", "ror", "select", "severity", "signal", "shared", "sla",
            "sll", "sra", "srl", "subtype", "then", "to", "transport", "type", "unaffected",
            "units", "until", "use", "variable", "wait", "when", "while", "with", "xnor", "xor"
        };

        public static bool Check(string name)
        {
            bool valid = (name != "");
            Match match = Regex.Match(name, @"^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase);
            valid &= match.Success;
            valid &= !name.Contains("__");
            valid &= !name.EndsWith("_");
            valid &= !reservedNames.Contains(name.ToLower());
            return valid;
        }
    }
}