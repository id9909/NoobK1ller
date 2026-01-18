#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NoobK1ller
{
    public class Il2CppStructShuffler
    {
        private string structName;
        private List<string> whiteList;
        private Guid randSeed;
        private List<FieldInfo> structure = new List<FieldInfo>();
        private int structSize = 0;
        private static Dictionary<string, int> typeSizes = new Dictionary<string, int> { { "int32_t", 4 }, { "uint32_t", 4 }, { "int", 4 }, { "uint", 4 }, { "float", 4 }, { "int16_t", 2 }, { "uint16_t", 2 }, { "short", 2 }, { "ushort", 2 }, { "int8_t", 1 }, { "uint8_t", 1 }, { "char", 1 }, { "byte", 1 }, { "bool", 1 }, { "int64_t", 8 }, { "uint64_t", 8 }, { "long", 8 }, { "ulong", 8 }, { "double", 8 } };

        public Il2CppStructShuffler(string structName, List<string> whiteList = null)
        {
            this.structName = structName;
            this.whiteList = whiteList;
            randSeed = Guid.NewGuid();
        }

        private int GetTypeSize(string type)
        {
            type = type.Trim();
            if (!typeSizes.ContainsKey(type)) return -1;
            return typeSizes[type];
        }

        public static void ShuffleSimple(ref string fileContent, string structName)
        {
            string pattern = $@"(typedef\s+)?struct\s+{structName}\s*\{{([^}}]+)\}}(\s*{structName}\s*;)?";
            var match = Regex.Match(fileContent, pattern, RegexOptions.Singleline);
            if (!match.Success) return;
            string originalStructDef = match.Value;
            string body = match.Groups[2].Value;
            string shuffledBody = Utils.ShuffleLines(body);
            var sb = new StringBuilder();
            sb.AppendLine($"typedef struct {structName}");
            sb.AppendLine("{");
            sb.Append(shuffledBody);
            sb.AppendLine($"\n}} {structName};");
            fileContent = fileContent.Replace(originalStructDef, sb.ToString());
        }

        public string Shuffle(string fileContent)
        {
            structure.Clear();
            string pattern = $@"(typedef\s+)?struct\s+{structName}\s*\{{([^}}]+)\}}(\s*{structName}\s*;)?";
            var match = Regex.Match(fileContent, pattern, RegexOptions.Singleline);
            if (!match.Success) return fileContent;
            string originalStructDef = match.Value;
            string body = match.Groups[2].Value;
            body = body.Replace(" **", "** ").Replace(" *", "* ");
            body = Regex.Replace(body, @"(\r?\n)[ \t]*(\r?\n)+", "$1");
            var fieldMatches = Regex.Matches(body, @"(const|unsigned\s+)?([_a-zA-Z][_a-zA-Z0-9]*)\s*(\*{1,2})?\s+(const\s+)?([_a-zA-Z][_a-zA-Z0-9]*)\s*;");
            int currentOffset = 0;
            foreach (Match m in fieldMatches)
            {
                string constPrefix = m.Groups[1].Value.Trim();
                string baseType = m.Groups[2].Value.Trim();
                string pointer = m.Groups[3].Value.Trim();
                string constSuffix = m.Groups[4].Value.Trim();
                string name = m.Groups[5].Value.Trim();
                string type = constPrefix;
                if (!string.IsNullOrEmpty(type)) type += " ";
                type += baseType;
                if (!string.IsNullOrEmpty(pointer)) type += pointer;
                if (!string.IsNullOrEmpty(constSuffix)) type += " " + constSuffix;
                type = type.Trim();
                int size = GetTypeSize(type);
                int align = Math.Min(size, 4);
                currentOffset = (currentOffset + (align - 1)) & ~(align - 1);
                structure.Add(new FieldInfo(name, type, size, currentOffset));
                currentOffset += size;
            }
            if (structure.Count == 0) return fileContent;
            structSize = currentOffset;
            List<FieldInfo> shuffled = null;
            bool orderChanged = false;
            do
            {
                if (whiteList != null)
                {
                    shuffled = new List<FieldInfo>();
                    var groupedBySize = structure.GroupBy(f => f.size);
                    foreach (var group in groupedBySize)
                    {
                        var groupShuffled = group.OrderBy(x => (randSeed + x.name).GetHashCode()).ToList();
                        shuffled.AddRange(groupShuffled);
                    }
                    orderChanged = true;
                    for (int i = 0; i < structure.Count; i++)
                    {
                        if (whiteList.Contains(structure[i].name) && structure[i].name == shuffled[i].name)
                        {
                            orderChanged = false;
                            break;
                        }
                    }
                }
                else
                {
                    shuffled = structure.OrderBy(x => (randSeed + x.name).GetHashCode()).ToList();
                    orderChanged = true;
                    for (int i = 0; i < structure.Count; i++)
                    {
                        if (structure[i].name == shuffled[i].name)
                        {
                            orderChanged = false;
                        }
                    }
                }
                if (orderChanged) break;
                else
                {
                    randSeed = Guid.NewGuid();
                }
            } while (!orderChanged);
            //shuffled = structure;//
            int cOffset = 0;
            foreach (FieldInfo field in shuffled)
            {
                field.newOffset = cOffset;
                cOffset += field.size;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"typedef struct {structName}");
            sb.AppendLine("{");
            foreach (FieldInfo field in shuffled)
            {
                sb.AppendLine($"    {field.type} {field.name};");
            }
            sb.AppendLine($"}} {structName};");
            return fileContent.Replace(originalStructDef, sb.ToString());
        }

        public void ParseMetadata(ref byte[] data, int start, int size)
        {
            if (size % structSize != 0)
            {
                Utils.LOGE("Invalid struct size " + structName + " " + structSize);
            }
            for (int i = 0; i < size; i += structSize)
            {
                int pos = start + i;
                byte[] oldData = new byte[structSize];
                byte[] newData = new byte[structSize];
                Buffer.BlockCopy(data, pos, oldData, 0, structSize);
                foreach (FieldInfo fi in structure)
                {
                    byte[] temp = new byte[fi.size];
                    Buffer.BlockCopy(oldData, fi.oldOffset, temp, 0, temp.Length);
                    Buffer.BlockCopy(temp, 0, newData, fi.newOffset, temp.Length);
                }
                Buffer.BlockCopy(newData, 0, data, pos, newData.Length);
            }
        }

        public override string ToString()
        {
            int i = 0;
            string r = "";
            foreach (FieldInfo fi in structure)
            {
                r += i.ToString() + ": " + fi.ToString() + "\n";
                i++;
            }
            return r;
        }

        public class FieldInfo
        {
            public string name;
            public string type;
            public int size;
            public int oldOffset;
            public int newOffset;

            public FieldInfo(string n, string t, int s, int oo, int no = 0)
            {
                name = n; type = t; size = s; oldOffset = oo; newOffset = no;
            }

            public override string ToString()
            {
                return "n: " + name + " t: " + type + " s: " + size.ToString() + " oo: " + oldOffset.ToString() + " no: " + newOffset.ToString();
            }
        }
    }

    public class Il2CppSymbolRenamer
    {
        public Dictionary<string, string> mapping = new Dictionary<string, string>();

        public string ProcessFiles(string il2cppPath, string hdata)
        {
            List<string> methodNames = new List<string>();
            MatchCollection matches = new Regex(@"(DO_API|DO_API_NO_RETURN)\s*\([^,]*,\s*(il2cpp_\w+)\s*,", RegexOptions.Singleline).Matches(hdata);
            int minSymbLen = 99;
            foreach (Match match in matches)
            {
                string methodName = match.Groups[2].Value;
                if (!methodNames.Contains(methodName))
                {
                    methodNames.Add(methodName);
                    if (methodName.Length < minSymbLen)
                    {
                        minSymbLen = methodName.Length;
                    }
                }
            }
            if (methodNames.Count == 0) return hdata;
            mapping = new Dictionary<string, string>();
            HashSet<string> used = new HashSet<string>();
            foreach (string methodName in methodNames)
            {
                string newName = Utils.GetRandString(minSymbLen);
                mapping[methodName] = newName;
            }
            string[] allFiles = Directory.GetFiles(il2cppPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext != ".h" && ext != ".cpp") continue;
                try
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    string result = content;
                    foreach (var kvp in mapping)
                    {
                        result = Regex.Replace(result, @"\b" + Regex.Escape(kvp.Key) + @"\b", kvp.Value);
                    }
                    content = result;
                    File.WriteAllText(file, content, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Utils.LOGE($"Error processing {file}: {ex.Message}");
                }
            }
            foreach (var kvp in mapping)
            {
                hdata = Regex.Replace(hdata, $@"\b{Regex.Escape(kvp.Key)}\b", kvp.Value);
            }
            return hdata;
        }

        public void ProcessLib(ref byte[] libunity)
        {
            foreach (var s in mapping)
            {
                string oldName = s.Key;
                string newName = s.Value;
                byte[] oldBytes = Encoding.UTF8.GetBytes("\x00" + oldName + "\x00");
                byte[] newBytes = Encoding.UTF8.GetBytes("\x00" + newName + "\x00");
                Utils.ReplaceBytes(ref libunity, "il2cpp: function " + oldName + " not found", 0);
                if (oldBytes.Length == newBytes.Length)
                {
                    Utils.ReplaceBytes(ref libunity, oldBytes, newBytes);
                }
                else if (oldBytes.Length > newBytes.Length)
                {
                    int diff = oldBytes.Length - newBytes.Length;
                    byte[] padding = new byte[diff];
                    byte[] paddedNew = newBytes.Take(newBytes.Length - 1).Concat(padding).Concat(new byte[] { 0 }).ToArray();
                    Utils.ReplaceBytes(ref libunity, oldBytes, paddedNew);
                }
                else
                {
                    Utils.LOGE($"Invalid symbol mapping: {oldName} -> {newName}");
                }
            }
        }
    }
}

#endif