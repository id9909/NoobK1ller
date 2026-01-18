#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NoobK1ller
{
    public static class Utils
    {
        public static char PathSeparator = Path.DirectorySeparatorChar;
        public static string LogFile = $"Assets{PathSeparator}Editor{PathSeparator}NoobK1ller{PathSeparator}log.txt";
        public static List<string> generatedStrings = new List<string>();
        private static readonly System.Random rnd = new System.Random();

        public static bool isNormalReport(BuildReport report)
        {
            return report.summary.platform == BuildTarget.Android && report.summary.result != BuildResult.Failed && report.summary.result != BuildResult.Cancelled;
        }

        public static void LOGD(string text, bool fileOnly = false)
        {
            if (!fileOnly) UnityEngine.Debug.Log("[NoobK1ller] " + text);
            File.AppendAllText(LogFile, "[D] " + text + "\n");
        }

        public static void LOGW(string text, bool fileOnly = false)
        {
            if (!fileOnly) UnityEngine.Debug.LogWarning("[NoobK1ller] " + text);
            File.AppendAllText(LogFile, "[W] " + text + "\n");
        }

        public static void LOGE(string text, bool fileOnly = false)
        {
            if (!fileOnly) UnityEngine.Debug.LogError("[NoobK1ller] " + text);
            File.AppendAllText(LogFile, "[E] " + text + "\n");
        }

        public static string GetIl2CppPath()
        {
            string il2cppPath = EditorApplication.applicationPath;
            if (!il2cppPath.EndsWith("Editor"))
            {
                int index = il2cppPath.LastIndexOf("Editor");
                if (index >= 0)
                {
                    il2cppPath = il2cppPath.Substring(0, index + "Editor".Length);
                }
            }
            if (!il2cppPath.EndsWith($"{PathSeparator}Data{PathSeparator}il2cpp{PathSeparator}libil2cpp"))
            {
                il2cppPath += $"{PathSeparator}Data{PathSeparator}il2cpp{PathSeparator}libil2cpp";
            }
            if (!Directory.Exists(il2cppPath))
            {
                LOGE("Invalid libil2pp path: " + il2cppPath);
                return null;
            }
            return il2cppPath;
        }

        public static string GetOutputPath(BuildReport report)
        {
            return Path.HasExtension(report.summary.outputPath) ? Path.GetDirectoryName(report.summary.outputPath) : report.summary.outputPath;
        }

        public static int GetLine(string lines, string str, bool ignore = false)
        {
            string[] liness = lines.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (liness[i].Contains(str))
                {
                    return i;
                }
            }
            if (!ignore)
            {
                LOGW("Failed to get line: " + str);
            }
            return -1;
        }

        public static string ToHexString(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                sb.Append("\\x");
                sb.Append(((byte)c).ToString("x2"));
            }
            return sb.ToString();
        }

        public static byte[] hex2bin(string data)
        {
            return Enumerable.Range(0, data.Length / 2).Select(i => Convert.ToByte(data.Substring(i * 2, 2), 16)).ToArray();
        }

        public static string GetRandString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int chars_len = chars.Length;
            System.Random random = new System.Random();
            for (int ii = 0; ii < 88888; ii++) {
                char prev = '\0';
                char[] result = new char[length];
                for (int i = 0; i < length; i++)
                {
                    char c;
                    do {
                        c = chars[random.Next(chars_len)];
                    } while (c == prev);
                    prev = c;
                    result[i] = c;
                }
                string rs = new string(result);
                if (!generatedStrings.Contains(rs)) {
                    generatedStrings.Add(rs);
                    return rs;
                }
            }
            Utils.LOGE("Failed to generate rand string");
            return null;
        }

        public static string GetRandBinaryString(int length)
        {
            const string chars = "01";
            int chars_len = chars.Length;
            System.Random random = new System.Random();
            for (int ii = 0; ii < 88888; ii++) {
                char[] result = new char[length];
                for (int i = 0; i < length; i++)
                {
                    char c = chars[random.Next(chars_len)];
                    result[i] = c;
                }
                string rs = new string(result);
                if (!generatedStrings.Contains(rs)) {
                    generatedStrings.Add(rs);
                    return rs;
                }
            }
            Utils.LOGE("Failed to generate rand string");
            return null;
        }

        public static string GetRandInvisibleString(int length)
        {
            var sb = new StringBuilder(length * 4);
            for (int i = 0; i < length; i++)
            {
                int code = 0xE0100 + rnd.Next(0xB0);
                sb.Append(char.ConvertFromUtf32(code));
            }
            return sb.ToString();
        }

        public static string GetRandBuggyString(int length)
        {
            int[] charset = new int[] { 0x200B, 0x200C, 0x200D, 0x2060, 0x180E, 0x00A0, 0x2800, 0x2062, 0x2063 };
            StringBuilder sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int code = charset[rnd.Next(charset.Length)];
                sb.Append(char.ConvertFromUtf32(code));
            }
            return sb.ToString();
        }

        public static string ShuffleLines(string lines, int groupSize = 1)
        {
            string[] allLines = lines.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (allLines.Length < 2) return lines;
            if (groupSize == 1)
            {
                string[] shuffled;
                bool allMoved;
                int attempts = 0;
                do
                {
                    shuffled = allLines.OrderBy(x => Guid.NewGuid()).ToArray();
                    allMoved = true;
                    for (int i = 0; i < allLines.Length; i++)
                    {
                        if (allLines[i] == shuffled[i])
                        {
                            allMoved = false;
                            break;
                        }
                    }
                    attempts++;
                    if (attempts > 88888) break;
                } while (!allMoved);
                return string.Join("\n", shuffled);
            }
            List<string[]> groups = new List<string[]>();
            for (int i = 0; i < allLines.Length; i += groupSize)
            {
                groups.Add(allLines.Skip(i).Take(groupSize).ToArray());
            }
            List<string[]> shuffledGroups;
            bool allGroupsMoved;
            int groupAttempts = 0;
            do
            {
                shuffledGroups = groups.OrderBy(x => Guid.NewGuid()).ToList();
                allGroupsMoved = true;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i] == shuffledGroups[i])
                    {
                        allGroupsMoved = false;
                        break;
                    }
                }
                groupAttempts++;
                if (groupAttempts > 88888) break;
            } while (!allGroupsMoved);
            return string.Join("\n", shuffledGroups.SelectMany(x => x));
        }

        public static string RemoveComments(string data)
        {
            StringBuilder result = new StringBuilder();
            bool inString = false;
            bool inChar = false;
            bool inBlockComment = false;
            bool inLineComment = false;
            bool escaped = false;
            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];
                char next = (i + 1 < data.Length) ? data[i + 1] : '\0';
                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }
                if (inLineComment)
                {
                    if (c == '\n')
                    {
                        inLineComment = false;
                        result.Append(c);
                    }
                    continue;
                }
                if (escaped)
                {
                    result.Append(c);
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    result.Append(c);
                    continue;
                }
                if (c == '"' && !inChar)
                {
                    inString = !inString;
                    result.Append(c);
                    continue;
                }
                if (c == '\'' && !inString)
                {
                    inChar = !inChar;
                    result.Append(c);
                    continue;
                }
                if (!inString && !inChar)
                {
                    if (c == '/' && next == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }
                    if (c == '/' && next == '/')
                    {
                        inLineComment = true;
                        i++;
                        continue;
                    }
                }
                result.Append(c);
            }
            return result.ToString();
        }

        public static string ReplaceFunctionBody(string code, string signature, string newBody)
        {
            int start = code.IndexOf(signature);
            if (start < 0) return code;
            int braceStart = code.IndexOf('{', start);
            if (braceStart < 0) return code;
            int pos = braceStart;
            int depth = 0;
            do
            {
                if (code[pos] == '{') depth++;
                else if (code[pos] == '}') depth--;
                pos++;
            }
            while (pos < code.Length && depth > 0);
            string replacement = signature + "\n{\n" + newBody + "\n}";
            return code.Substring(0, start) + replacement + code.Substring(pos);
        }

        public static void ReplaceBytes(ref byte[] source, string oldString, byte fillByte)
        {
            byte[] oldBytes = Encoding.UTF8.GetBytes(oldString);
            byte[] newBytes = Enumerable.Repeat(fillByte, oldBytes.Length).ToArray();
            ReplaceBytes(ref source, oldBytes, newBytes);
        }

        public static void ReplaceBytes(ref byte[] source, string oldString, string newString)
        {
            ReplaceBytes(ref source, Encoding.UTF8.GetBytes(oldString), Encoding.UTF8.GetBytes(newString));
        }

        public static void ReplaceBytes(ref byte[] source, byte[] oldBytes, byte[] newBytes)
        {
            if (oldBytes == null || newBytes == null) return;
            int oldLen = oldBytes.Length;
            int newLen = newBytes.Length;
            if (oldLen == newLen)
            {
                ReadOnlySpan<byte> oldSpan = oldBytes;
                Span<byte> srcSpan = source;
                int pos = 0;
                while (pos <= srcSpan.Length - oldLen)
                {
                    int index = srcSpan[pos..].IndexOf(oldSpan);
                    if (index == -1) break;
                    pos += index;
                    newBytes.CopyTo(srcSpan[pos..]);
                    pos += oldLen;
                }
                return;
            }
            ReadOnlySpan<byte> src = source;
            ReadOnlySpan<byte> oldPattern = oldBytes;
            int count = 0;
            int searchPos = 0;
            while (searchPos <= src.Length - oldLen)
            {
                int index = src[searchPos..].IndexOf(oldPattern);
                if (index == -1) break;
                count++;
                searchPos += index + oldLen;
            }
            if (count == 0) return;
            int resultSize = source.Length + (count * (newLen - oldLen));
            byte[] result = new byte[resultSize];
            Span<byte> resultSpan = result;
            int srcPos = 0;
            int dstPos = 0;
            while (srcPos < src.Length)
            {
                int index = src[srcPos..].IndexOf(oldPattern);
                if (index == -1)
                {
                    src[srcPos..].CopyTo(resultSpan[dstPos..]);
                    break;
                }
                src.Slice(srcPos, index).CopyTo(resultSpan[dstPos..]);
                dstPos += index;
                srcPos += index;
                if (newBytes != null)
                {
                    newBytes.CopyTo(resultSpan[dstPos..]);
                    dstPos += newLen;
                }
                srcPos += oldLen;
            }
            source = result;
        }

        public static void ClearBeeCache()
        {
            string cachePath = Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "Bee");
            if (Directory.Exists(cachePath))
            {
                try
                {
                    Directory.Delete(cachePath, true);
                }
                catch (Exception ex)
                {
                    LOGE($"Failed to delete directory \"{cachePath}\": {ex.Message}");
                }
            }
        }

        public static void BackupFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }
            string backupPath = filePath + ".noob_bak";
            if (File.Exists(backupPath))
            {
                return;
            }
            File.Copy(filePath, backupPath);
        }

        public static int RestoreAllBaks(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return -1;
            }
            int restored = 0;
            string[] bakFiles = Directory.GetFiles(folderPath, "*.noob_bak", SearchOption.AllDirectories);
            foreach (string file in bakFiles)
            {
                string oPath = file.Replace(".noob_bak", "");
                File.Copy(file, oPath, overwrite: true);
                File.Delete(file);
                restored += 1;
            }
            return restored;
        }

        public static void ResetStaticFields(Type type)
        {
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.IsInitOnly) continue;
                field.SetValue(null, null);
            }
        }
    }
}

#endif
