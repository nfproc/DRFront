// DRFront: A Dynamic Reconfiguration Frontend for Xilinx FPGAs
// Copyright (C) 2022-2024 Naoki FUJIEDA. New BSD License is applied.
//**********************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DRFront
{
    // Entity に関する情報のクラス
    public class VerilogEntity : HDLEntity
    {
        public VerilogEntity(string path, string originalName) : base(path, originalName) { }
    }

    // Component に関する情報のクラス
    public class VerilogComponent : HDLComponent
    {
        public VerilogComponent(string name, string from) : base(name, from) { }
    };

    // Port に関する情報を保持するクラス
    public class VerilogPort : HDLPort
    {
        public VerilogPort(string originalName, string direction, int upper = -1, int lower = -1)
            : base(originalName, direction, upper, lower) { }

        public VerilogPort(TemplatePortItem port) : base(port) { }

        public override string ToString()
        {
            return ToString(false, false);
        }

        public string ToString(bool forSignal, bool withPrefix)
        {
            string dir = (forSignal) ? "" : Direction.ToLower() + " ";
            string range = (IsVector) ? "[" + Upper + ":" + Lower + "] " : "";
            string prefix = (withPrefix) ? "_usr_" : "";
            return dir + "logic " + range + prefix + OriginalName;
        }

        public override List<HDLPort> ToVector()
        {
            List<HDLPort> result = new List<HDLPort>();
            if (IsVector)
                for (int i = Lower; i <= Upper; i += 1)
                    result.Add(new VerilogPort(OriginalName + "[" + i + "]", Direction));
            else
                result.Add(new VerilogPort(OriginalName, Direction));
            return result;
        }
    }

    // ソースコードの解析結果を保持するクラス
    public class VerilogSource : HDLSource
    {
        private string SVInstPath;

        public VerilogSource(string svpath = null) : base()
        {
            SVInstPath = svpath;
        }

        public VerilogSource(HDLEntity entity, List<HDLPort> port) : base(entity, port) { }

        // Read メソッド: 一般のソースファイルの解析を行う
        public override void Read(string sourceName)
        {
            string shortPath = Path.GetFileName(sourceName);
            string analStr = CallSVInst(sourceName);
            if (analStr == null)
                return;

            string[] strs = analStr.Replace("\r\n", "\n").Split(new[] { '\n' });
            Match match;
            string currentEntity = "", currentPortName = "", currentPortDir = "";
            foreach (string str in strs)
            {
                // モジュール定義
                match = Regex.Match(str, @"^      - mod_name: ""([A-Za-z_][A-Za-z0-9_$]*)""");
                if (match.Success)
                {
                    VerilogEntity newEntity = new VerilogEntity(shortPath, match.Groups[1].Value);
                    currentEntity = newEntity.Name;
                    Entities.Add(newEntity);
                    if (! Ports.ContainsKey(currentEntity))
                        Ports.Add(currentEntity, new List<HDLPort>());
                }
                // モジュールのインスタンス化
                match = Regex.Match(str, @"^          - mod_name: ""([A-Za-z_][A-Za-z0-9_$]*)""");
                if (match.Success)
                {
                    VerilogComponent newComponent = new VerilogComponent(match.Groups[1].Value, currentEntity);
                    if (! Components.Contains(newComponent))
                        Components.Add(newComponent);
                }
                // ポート定義
                match = Regex.Match(str, @"^          - port_name: ""([A-Za-z_][A-Za-z0-9_$]*)""");
                if (match.Success)
                    currentPortName = match.Groups[1].Value;
                match = Regex.Match(str, @"^            port_dir: ""(in|out)put""");
                if (match.Success)
                    currentPortDir = (match.Groups[1].Value == "in") ? "Input" : "Output";
                match = Regex.Match(str, @"^            port_width: ([0-9]+)");
                if (match.Success)
                {
                    int width = int.Parse(match.Groups[1].Value);
                    int upper = (width != 1) ? width - 1 : -1;
                    int lower = (width != 1) ? 0 : -1;
                    Ports[currentEntity].Add(new VerilogPort(currentPortName, currentPortDir, upper, lower));
                }
            }
            IsValid = true;
        }

        private string CallSVInst(string sourceName)
        {
            if (! File.Exists(SVInstPath))
            {
                Problem = "ファイル解析のためのアプリケーションが見つかりません．";
                return null;
            }

            // プロセスを起動
            string outMessage = "";
            Process p = new Process();
            try
            {
                p.StartInfo.FileName = SVInstPath;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.Arguments = "--allow_incomplete \"" + sourceName + "\"";
                p.Start();
            }
            catch (Win32Exception)
            {
                Problem = "ファイル解析のためのアプリケーションの起動に失敗しました．";
                return null;
            }
            p.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (e.Data != null)
                    outMessage += e.Data + "\n";
            });
            p.BeginOutputReadLine();

            // プロセスの終了待ち．しばらく待っても終了しない場合はタイムアウト
            if (!p.WaitForExit(1000))
            {
                p.Kill();
                p.Close();
                Problem = "ファイル解析がタイムアウトしました．";
                return null;
            }
            if (p.ExitCode != 0)
            {
                p.Close();
                Problem = "ファイル解析に失敗しました．";
                return null;
            }
            p.Close();
            return outMessage;
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
                    Match match = Regex.Match(line, @"([A-Za-z_][A-Za-z0-9_$]*) usr \(");
                    if (match.Success)
                    {
                        Components.Add(new VHDLComponent(match.Groups[1].Value, "top"));
                    }
                    match = Regex.Match(line, @"assign _usr_([A-Za-z0-9_$\[\]]+) = ([A-Za-z0-9_$\[\]]+)");
                    if (match.Success)
                    {
                        VerilogPort port = new VerilogPort(match.Groups[1].Value, "Input");
                        port.ToAssign = match.Groups[2].Value;
                        topPorts.Add(port);
                    }
                    match = Regex.Match(line, @"assign ([A-Za-z0-9_$\[\]]+) = _usr_([A-Za-z0-9_$\[\]]+)");
                    if (match.Success)
                    {
                        VerilogPort port = new VerilogPort(match.Groups[2].Value, "Output");
                        port.ToAssign = match.Groups[1].Value;
                        topPorts.Add(port);
                    }
                }
                Ports.Add("top", topPorts);
                sr.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("トップ回路の SystemVerilog ファイルの読込中にエラーが発生しました．\n" + ex.Message);
                return;
            }
        }

        // Generate メソッド(1): トップ回路・テストベンチの生成
        public override void Generate(string template, string fullFileName, IList<UserPortItem> UserPorts, Dictionary<string, string> UnusedPorts = null)
        {
            string userCode = GetUserCodeToPreserve(fullFileName);
            string userEntity = Entities[0].Name;
            List<HDLPort> HDLPorts = Ports[Entities[0].Name];
            bool preserved = false;

            string[] strs = template.Replace("\r\n", "\n").Split(new[] { '\n' });
            try
            {
                StreamWriter sw = File.CreateText(fullFileName);
                foreach (string str in strs)
                {
                    if (str.StartsWith("// USER_SIGNAL"))
                    {
                        bool withPrefix = str.StartsWith("// USER_SIGNAL_PREFIX");
                        foreach (HDLPort port in HDLPorts)
                            if (port is VerilogPort vport)
                                sw.WriteLine("    " + vport.ToString(true, withPrefix) + ";");
                    }
                    else if (str.StartsWith("// USER_INSTANCE"))
                    {
                        bool withPrefix = str.StartsWith("// USER_INSTANCE_PREFIX");
                        string prefix = withPrefix ? "_usr_" : "";
                        sw.WriteLine("    " + userEntity + " usr (");
                        int i = 0;
                        foreach (HDLPort port in HDLPorts)
                        {
                            string sep = (i == HDLPorts.Count - 1) ? ");" : ",";
                            if (port is VerilogPort vport)
                                sw.WriteLine("        ." + vport.Name + "(" + prefix + vport.Name + ")" + sep);
                            i += 1;
                        }
                        if (withPrefix)
                        {
                            foreach (UserPortItem port in UserPorts)
                            {
                                if (port.Direction == "Input")
                                {
                                    string target = (port.TopPort != "") ? port.TopPort : "1'b0";
                                    sw.WriteLine("    assign " + prefix + port.Name + " = " + target + ";");
                                }
                                else if (port.TopPort != "")
                                {
                                    sw.WriteLine("    assign " + port.TopPort + " = " + prefix + port.Name + ";");
                                }
                            }
                            foreach (KeyValuePair<string, string> def in UnusedPorts)
                            {
                                string val = (def.Value == "'0'") ? "1'b0" : "1'b1";
                                sw.WriteLine("    assign " + def.Key + " = " + val + ";");
                            }
                        }
                    }
                    else if (str.StartsWith("// vvv"))
                    {
                        sw.WriteLine(str);
                        if (userCode != "")
                        {
                            sw.Write(userCode);
                            preserved = true;
                        }
                    }
                    else if (str.StartsWith("// ^^^"))
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
                MsgBox.Warn("SystemVerilog ファイルの作成中にエラーが発生しました．\n" + ex.Message);
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
                    if (str.StartsWith("// USER_PORT"))
                    {
                        int i = 0;
                        foreach (TemplatePortItem port in TemplatePorts)
                        {
                            string sep = (i == TemplatePorts.Count - 1) ? "" : ",";
                            sw.WriteLine("    " + (new VerilogPort(port)).ToString() + sep);
                            i += 1;
                        }
                    }
                    else
                    {
                        sw.WriteLine(str.Replace("DR_ENTITY_NAME", Entities[0].Name));
                    }
                }
                sw.Close();
            }
            catch (IOException ex)
            {
                MsgBox.Warn("SystemVerilog ファイルの作成中にエラーが発生しました．\n" + ex.Message);
                return;
            }
        }
    }

    // SystemVerilog の信号名チェックのための静的クラス
    public static class VerilogNameChecker
    {
        private static List<string> reservedNames = new List<string>
        {
            "accept_on", "alias", "always", "always_comb", "always_ff", "always_latch", "and",
            "assert", "assign", "assume", "automatic", "before", "begin", "bind", "bins",
            "binsof", "bit", "break", "buf", "bufif0", "bufif1", "byte", "case", "casex",
            "casez", "cell", "chandle", "checker", "class", "clocking", "cmos", "config",
            "const", "constraint", "context", "continue", "cover", "covergroup", "coverpoint",
            "cross", "deassign", "default", "defparam", "design", "disable", "dist", "do",
            "edge", "else", "end", "endcase", "endchecker", "endclass", "endclocking",
            "endconfig", "endfunction", "endgenerate", "endgroup", "endinterface",
            "endmodule", "endpackage", "endprimitive", "endprogram", "endproperty",
            "endspecify", "endsequence", "endtable", "endtask", "enum", "event", "eventually",
            "expect", "export", "extends", "extern", "final", "first_match", "for", "force",
            "foreach", "forever", "fork", "forkjoin", "function", "generate", "genvar",
            "global", "highz0", "highz1", "if", "iff", "ifnone", "ignore_bins",
            "illegal_bins", "implements", "implies", "import", "incdir", "include", "initial",
            "inout", "input", "inside", "instance", "int", "integer", "interconnect",
            "interface", "intersect", "join", "join_any", "join_none", "large", "let",
            "liblist", "library", "local", "localparam", "logic", "longint", "macromodule",
            "matches", "medium", "modport", "module", "nand", "negedge", "nettype", "new",
            "nexttime", "nmos", "nor", "noshowcancelled", "not", "notif0", "notif1", "null",
            "or", "output", "package", "packed", "parameter", "pmos", "posedge", "primitive",
            "priority", "program", "property", "protected", "pull0", "pull1", "pulldown",
            "pullup", "pulsestyle_ondetect", "pulsestyle_onevent", "pure", "rand", "randc",
            "randcase", "randsequence", "rcmos", "real", "realtime", "ref", "reg",
            "reject_on", "release", "repeat", "restrict", "return", "rnmos", "rpmos",
            "rtran", "rtranif0", "rtranif1", "s_always", "s_eventually", "s_nexttime",
            "s_until", "s_until_with", "scalared", "sequence", "shortint", "shortreal",
            "showcancelled", "signed", "small", "soft", "solve", "specify", "specparam",
            "static", "string", "strong", "strong0", "strong1", "struct", "super", "supply0",
            "supply1", "sync_accept_on", "sync_reject_on", "table", "tagged", "task", "this",
            "throughout", "time", "timeprecision", "timeunit", "tran", "tranif0", "tranif1",
            "tri", "tri0", "tri1", "triand", "trior", "trireg", "type", "typedef", "union",
            "unique", "unique0", "unsigned", "until", "until_with", "untyped", "use",
            "uwire", "var", "vectored", "virtual", "void", "wait", "wait_order", "wand",
            "weak", "weak0", "weak1", "while", "wildcard", "wire", "with", "within", "wor",
            "xnor", "xor"
        };

        public static bool Check(string name)
        {
            bool valid = (name != "");
            Match match = Regex.Match(name, @"^[a-zA-Z_][a-zA-Z0-9_$]*$");
            valid &= match.Success;
            valid &= !reservedNames.Contains(name);
            return valid;
        }
    }
}