#if UNITY_EDITOR

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEditor;

namespace NoobK1ller
{
    public class Service : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject, IPostprocessBuildWithReport
    {
        public int callbackOrder => 48997;
        private static bool enable;
        private static bool gradleModified;
        private static bool arm32;
        private static bool arm64;
        private static bool x86_64;
        private static int[] randKey;
        private static int metadataHeaderOffset;
        private static int metadataHeaderSize;
        private static string gradlePath;
        private static string metadataName;
        private static string headerStructBody;
        private static Il2CppSymbolRenamer symbolRenamer;
        private static Il2CppStructShuffler headerShuffler;
        private static bool fieldsInited = false;
        private static readonly string origMetadataName = "global-metadata.dat";
        private static readonly char PathSeparator = Path.DirectorySeparatorChar;
        private static readonly string il2cppPath = Utils.GetIl2CppPath();
        private static readonly System.Random rnd = new System.Random();

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Utils.isNormalReport(report)) return;
            Utils.RestoreAllBaks(il2cppPath);
            File.WriteAllText(Utils.LogFile, "");
            if (!fieldsInited)
            {
                ResetFields();
            }
            if (!Settings.cfg.Enable)
            {
                enable = false;
                Utils.LOGW("Disabled, ignoring build");
                return;
            }
            AndroidArchitecture androidTargetArchitectures = PlayerSettings.Android.targetArchitectures;
            arm32 = (androidTargetArchitectures & AndroidArchitecture.ARMv7) != 0;
            arm64 = (androidTargetArchitectures & AndroidArchitecture.ARM64) != 0;
            x86_64 = (androidTargetArchitectures & AndroidArchitecture.X86_64) != 0;
            if (!arm32 && !arm64 && !x86_64)
            {
                enable = false;
                Utils.LOGE("Invalid build arch");
                return;
            }
            enable = true;
            gradleModified = false;
            Utils.generatedStrings.Clear();
            if (Settings.cfg.ClearBeeCache)
            {
                Utils.ClearBeeCache();
            }
            BackupApi();
            ModifyApi();
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!enable || gradleModified) return;
            gradleModified = true;
            gradlePath = path;
            ProcessLibs(path + $"{PathSeparator}src{PathSeparator}main{PathSeparator}jniLibs");
            ProcessMetadata(path + $"{PathSeparator}src{PathSeparator}main{PathSeparator}assets{PathSeparator}bin{PathSeparator}Data{PathSeparator}Managed{PathSeparator}Metadata{PathSeparator}{origMetadataName}");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!enable || !Utils.isNormalReport(report)) return;
            RestoreApi();
            if (Settings.cfg.ClearBeeCache)
            {
                Utils.ClearBeeCache();
            }
            else
            {
                Directory.Delete(gradlePath + $"{PathSeparator}src{PathSeparator}main", true);
            }
        }

        private static void ProcessLibs(string libpath)
        {
            if (!Directory.Exists(libpath))
            {
                Utils.LOGE("Invalid lib path: " + libpath);
                return;
            }
            if (Settings.cfg.RenameSymbols)
            {
                if (arm32)
                {
                    byte[] libunity = File.ReadAllBytes(libpath + $"{PathSeparator}armeabi-v7a{PathSeparator}libunity.so");
                    symbolRenamer.ProcessLib(ref libunity);
                    File.WriteAllBytes(libpath + $"{PathSeparator}armeabi-v7a{PathSeparator}libunity.so", libunity);
                }
                if (arm64)
                {
                    byte[] libunity = File.ReadAllBytes(libpath + $"{PathSeparator}arm64-v8a{PathSeparator}libunity.so");
                    symbolRenamer.ProcessLib(ref libunity);
                    File.WriteAllBytes(libpath + $"{PathSeparator}arm64-v8a{PathSeparator}libunity.so", libunity);
                }
                if (x86_64)
                {
                    byte[] libunity = File.ReadAllBytes(libpath + $"{PathSeparator}x86_64{PathSeparator}libunity.so");
                    symbolRenamer.ProcessLib(ref libunity);
                    File.WriteAllBytes(libpath + $"{PathSeparator}x86_64{PathSeparator}libunity.so", libunity);
                }
            }
        }

        private static void ProcessMetadata(string metadataPath)
        {
            if (!File.Exists(metadataPath))
            {
                Utils.LOGE("Invalid metadata path: " + metadataPath);
                return;
            }
            byte[] data = File.ReadAllBytes(metadataPath);
            if (data[0] != 0xAF || data[1] != 0x1B || data[2] != 0xB1 || data[3] != 0xFA)
            {
                Utils.LOGW("Invalid metadata sanity, ignoring");
                return;
            }
            File.Delete(metadataPath);
            File.Delete(metadataPath + ".bak");
            //File.WriteAllBytes(metadataPath + "_orig.bak", data);//
            int stringLiteralOffset = GetHeaderValue(ref data, "stringLiteralOffset");
            int stringLiteralSize = GetHeaderValue(ref data, "stringLiteralSize");
            int stringLiteralDataOffset = GetHeaderValue(ref data, "stringLiteralDataOffset");
            int stringLiteralDataSize = GetHeaderValue(ref data, "stringLiteralDataSize");
            int stringOffset = GetHeaderValue(ref data, "stringOffset");
            int stringSize = GetHeaderValue(ref data, "stringSize");
            int eventsOffset = GetHeaderValue(ref data, "eventsOffset");
            int eventsSize = GetHeaderValue(ref data, "eventsSize");
            int propertiesOffset = GetHeaderValue(ref data, "propertiesOffset");
            int propertiesSize = GetHeaderValue(ref data, "propertiesSize");
            int methodsOffset = GetHeaderValue(ref data, "methodsOffset");
            int methodsSize = GetHeaderValue(ref data, "methodsSize");
            int fieldAndParameterDefaultValueDataOffset = GetHeaderValue(ref data, "fieldAndParameterDefaultValueDataOffset");
            int fieldAndParameterDefaultValueDataSize = GetHeaderValue(ref data, "fieldAndParameterDefaultValueDataSize");
            int parametersOffset = GetHeaderValue(ref data, "parametersOffset");
            int parametersSize = GetHeaderValue(ref data, "parametersSize");
            int fieldsOffset = GetHeaderValue(ref data, "fieldsOffset");
            int fieldsSize = GetHeaderValue(ref data, "fieldsSize");
            int typeDefinitionsOffset = GetHeaderValue(ref data, "typeDefinitionsOffset");
            int typeDefinitionsSize = GetHeaderValue(ref data, "typeDefinitionsSize");
            int imagesOffset = GetHeaderValue(ref data, "imagesOffset");
            int imagesSize = GetHeaderValue(ref data, "imagesSize");
            int assembliesOffset = GetHeaderValue(ref data, "assembliesOffset");
            int assembliesSize = GetHeaderValue(ref data, "assembliesSize");
            int attributeDataOffset = GetHeaderValue(ref data, "attributeDataOffset");
            int attributeDataSize = GetHeaderValue(ref data, "attributeDataSize");
            int exportedTypeDefinitionsOffset = GetHeaderValue(ref data, "exportedTypeDefinitionsOffset");
            int exportedTypeDefinitionsSize = GetHeaderValue(ref data, "exportedTypeDefinitionsSize");
            data[0] = (byte)rnd.Next(0, 256);
            data[1] = (byte)rnd.Next(0, 256);
            data[2] = (byte)rnd.Next(0, 256);
            data[3] = 0;
            data[4] = (byte)rnd.Next(0, 256);
            data[5] = (byte)rnd.Next(0, 256);
            data[6] = (byte)rnd.Next(0, 256);
            data[7] = 0;
            if (Settings.cfg.EncryptData)
            {
                var sections = new List<(Action<int> setOffset, int offset, int size, string offsetName, string sizeName, int keyIndex)>
                {
                    (v => metadataHeaderOffset = v, metadataHeaderOffset, metadataHeaderSize, "", "", 0),
                    (v => stringLiteralOffset = v, stringLiteralOffset, stringLiteralSize, "stringLiteralOffset", "stringLiteralSize", 150),
                    (v => stringLiteralDataOffset = v, stringLiteralDataOffset, stringLiteralDataSize, "stringLiteralDataOffset", "stringLiteralDataSize", 152),
                    (v => stringOffset = v, stringOffset, stringSize, "stringOffset", "stringSize", 154),
                    (v => eventsOffset = v, eventsOffset, eventsSize, "eventsOffset", "eventsSize", 156),
                    (v => propertiesOffset = v, propertiesOffset, propertiesSize, "propertiesOffset", "propertiesSize", 158),
                    (v => methodsOffset = v, methodsOffset, methodsSize, "methodsOffset", "methodsSize", 160),
                    (v => fieldAndParameterDefaultValueDataOffset = v, fieldAndParameterDefaultValueDataOffset, fieldAndParameterDefaultValueDataSize, "fieldAndParameterDefaultValueDataOffset", "fieldAndParameterDefaultValueDataSize", 162),
                    (v => parametersOffset = v, parametersOffset, parametersSize, "parametersOffset", "parametersSize", 164),
                    (v => fieldsOffset = v, fieldsOffset, fieldsSize, "fieldsOffset", "fieldsSize", 166),
                    (v => imagesOffset = v, imagesOffset, imagesSize, "imagesOffset", "imagesSize", 168),
                    (v => assembliesOffset = v, assembliesOffset, assembliesSize, "assembliesOffset", "assembliesSize", 170),
                    (v => attributeDataOffset = v, attributeDataOffset, attributeDataSize, "attributeDataOffset", "attributeDataSize", 172),
                    (v => exportedTypeDefinitionsOffset = v, exportedTypeDefinitionsOffset, exportedTypeDefinitionsSize, "exportedTypeDefinitionsOffset", "exportedTypeDefinitionsSize", 174)
                };
                sections = sections.OrderBy(x => rnd.Next()).ToList();
                foreach (var section in sections)
                {
                    byte[] garbage = new byte[rnd.Next(25400, 87501) & ~3];
                    rnd.NextBytes(garbage);
                    data = data.Concat(garbage).ToArray();
                    int size = section.size;
                    int oldOffset = section.offset;
                    int newOffset = data.Length;
                    Array.Resize(ref data, data.Length + size);
                    Buffer.BlockCopy(data, oldOffset, data, newOffset, size);
                    rnd.NextBytes(data.AsSpan(oldOffset, size));
                    section.setOffset(newOffset);
                    SetHeaderValue(ref data, section.offsetName, newOffset ^ randKey[section.keyIndex]);
                    SetHeaderValue(ref data, section.sizeName, size ^ randKey[section.keyIndex + 1]);
                }
                byte[] garbag = new byte[rnd.Next(25400, 87501) & ~3];
                rnd.NextBytes(garbag);
                data = data.Concat(garbag).ToArray();
                BitConverter.GetBytes(metadataHeaderOffset ^ randKey[444]).CopyTo(data, 0);
            }
            headerShuffler.ParseMetadata(ref data, metadataHeaderOffset, metadataHeaderSize);
            if (Settings.cfg.EncryptData)
            {
                byte prevByte = (byte)randKey[1100];
                for (int i = 0; i < metadataHeaderSize; i++)
                {
                    prevByte = data[metadataHeaderOffset + i] ^= (byte)(((i * (randKey[1] - prevByte)) + randKey[2]) ^ ((i >> 9) | (i << 15)));
                }
                prevByte = (byte)randKey[1101];
                for (int i = 0; i < stringLiteralSize; i++)
                {
                    prevByte = data[stringLiteralOffset + i] ^= (byte)(((i * (randKey[3] - prevByte)) + randKey[4]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1102];
                for (int i = 0; i < stringLiteralDataSize; i++)
                {
                    prevByte = data[stringLiteralDataOffset + i] ^= (byte)(((i * (randKey[5] - prevByte)) + randKey[6]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1103];
                for (int i = 0; i < stringSize; i++)
                {
                    prevByte = data[stringOffset + i] ^= (byte)(((i * (randKey[7] - prevByte)) + randKey[8]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1104];
                for (int i = 0; i < eventsSize; i++)
                {
                    prevByte = data[eventsOffset + i] ^= (byte)(((i * (randKey[9] - prevByte)) + randKey[10]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1105];
                for (int i = 0; i < propertiesSize; i++)
                {
                    prevByte = data[propertiesOffset + i] ^= (byte)(((i * (randKey[11] - prevByte)) + randKey[12]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1106];
                for (int i = 0; i < methodsSize; i++)
                {
                    prevByte = data[methodsOffset + i] ^= (byte)(((i * (randKey[13] - prevByte)) + randKey[14]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1107];
                for (int i = 0; i < fieldAndParameterDefaultValueDataSize; i++)
                {
                    prevByte = data[fieldAndParameterDefaultValueDataOffset + i] ^= (byte)(((i * (randKey[15] - prevByte)) + randKey[16]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1108];
                for (int i = 0; i < parametersSize; i++)
                {
                    prevByte = data[parametersOffset + i] ^= (byte)(((i * (randKey[17] - prevByte)) + randKey[18]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1109];
                for (int i = 0; i < fieldsSize; i++)
                {
                    prevByte = data[fieldsOffset + i] ^= (byte)(((i * (randKey[19] - prevByte)) + randKey[20]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1110];
                for (int i = 0; i < typeDefinitionsSize; i++)
                {
                    prevByte = data[typeDefinitionsOffset + i] ^= (byte)(((i * (randKey[21] - prevByte)) + randKey[22]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1111];
                for (int i = 0; i < imagesSize; i++)
                {
                    prevByte = data[imagesOffset + i] ^= (byte)(((i * (randKey[23] - prevByte)) + randKey[24]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1112];
                for (int i = 0; i < assembliesSize; i++)
                {
                    prevByte = data[assembliesOffset + i] ^= (byte)(((i * (randKey[25] - prevByte)) + randKey[26]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1113];
                for (int i = 0; i < attributeDataSize; i++)
                {
                    prevByte = data[attributeDataOffset + i] ^= (byte)(((i * (randKey[27] - prevByte)) + randKey[28]) ^ ((i >> 3) | (i << 5)));
                }
                prevByte = (byte)randKey[1114];
                for (int i = 0; i < exportedTypeDefinitionsSize; i++)
                {
                    prevByte = data[exportedTypeDefinitionsOffset + i] ^= (byte)(((i * (randKey[29] - prevByte)) + randKey[30]) ^ ((i >> 3) | (i << 5)));
                }
            }
            data = data.Concat(new byte[] { /*0x00, 0xF0, 0x9F, 0xAB, 0x83, 0xF0, 0x9F, 0x8F, 0xBF, 0x00,*/ 0x00, 0x99, 0x09, 0x00 }).ToArray();
            File.WriteAllBytes(metadataPath, data);
            Utils.LOGD("Metadata modified at " + metadataPath);
        }

        private static void ModifyApi()
        {
            string capipath = il2cppPath + $"{PathSeparator}il2cpp-api.cpp";
            string hapipath = il2cppPath + $"{PathSeparator}il2cpp-api-functions.h";
            string cipath = il2cppPath + $"{PathSeparator}il2cpp-class-internals.h";
            string gpath = il2cppPath + $"{PathSeparator}vm{PathSeparator}GlobalMetadata.cpp";
            string igpath = il2cppPath + $"{PathSeparator}vm{PathSeparator}GlobalMetadataFileInternals.h";
            if (Settings.cfg.EncryptKeys)
            {
                File.Copy($"Assets{PathSeparator}Editor{PathSeparator}NoobK1ller{PathSeparator}assets{PathSeparator}oxorany.cpp.txt", il2cppPath + $"{PathSeparator}oxorany.cpp", true);
                File.Copy($"Assets{PathSeparator}Editor{PathSeparator}NoobK1ller{PathSeparator}assets{PathSeparator}oxorany.h.txt", il2cppPath + $"{PathSeparator}oxorany.h", true);
            }
            else
            {
                File.Delete(il2cppPath + $"{PathSeparator}oxorany.cpp");
                File.Delete(il2cppPath + $"{PathSeparator}oxorany.h");
            }
            string capiddata = File.ReadAllText(capipath);
            string hapiddata = File.ReadAllText(hapipath);
            string cidata = File.ReadAllText(cipath);
            string gdata = File.ReadAllText(gpath);
            string igdata = File.ReadAllText(igpath);
            gdata = Regex.Replace(gdata, @"^\s*IL2CPP_ASSERT\(.*?\);\s*$", "", RegexOptions.Multiline);
            var structMatch = Regex.Match(igdata, @"typedef\s+struct\s+Il2CppGlobalMetadataHeader\s*\{([\s\S]*?)\}\s*Il2CppGlobalMetadataHeader\s*;", RegexOptions.Multiline);
            headerStructBody = structMatch.Groups[1].Value;
            headerStructBody = Regex.Replace(headerStructBody, @";[^\n]+", ";").Replace("\t", "").Replace("    ", "");
            metadataHeaderOffset = 0;
            metadataHeaderSize = Regex.Matches(headerStructBody, @"\bint32_t\b").Count * 4;
            metadataName = origMetadataName;
            gdata = gdata.Replace($"vm::MetadataLoader::LoadMetadataFile(\"{origMetadataName}\");", "vm::MetadataLoader::LoadMetadataFile(oxorany(\"" + metadataName + "\"));");
            if (Settings.cfg.EncryptData)
            {
                gdata = gdata.Replace("static void* s_GlobalMetadata;", Utils.ShuffleLines(@$"
static void* s_GlobalMetadata;
static int32_t noob_header_stringLiteralOffset = oxorany({randKey[150]});
static int32_t noob_header_stringLiteralSize = oxorany({randKey[151]});
static int32_t noob_header_stringLiteralDataOffset = oxorany({randKey[152]});
static int32_t noob_header_stringLiteralDataSize = oxorany({randKey[153]});
static int32_t noob_header_stringOffset = oxorany({randKey[154]});
static int32_t noob_header_stringSize = oxorany({randKey[155]});
static int32_t noob_header_eventsOffset = oxorany({randKey[156]});
static int32_t noob_header_eventsSize = oxorany({randKey[157]});
static int32_t noob_header_propertiesOffset = oxorany({randKey[158]});
static int32_t noob_header_propertiesSize = oxorany({randKey[159]});
static int32_t noob_header_methodsOffset = oxorany({randKey[160]});
static int32_t noob_header_methodsSize = oxorany({randKey[161]});
static int32_t noob_header_fieldAndParameterDefaultValueDataOffset = oxorany({randKey[162]});
static int32_t noob_header_fieldAndParameterDefaultValueDataSize = oxorany({randKey[163]});
static int32_t noob_header_parametersOffset = oxorany({randKey[164]});
static int32_t noob_header_parametersSize = oxorany({randKey[165]});
static int32_t noob_header_fieldsOffset = oxorany({randKey[166]});
static int32_t noob_header_fieldsSize = oxorany({randKey[167]});
static int32_t noob_header_imagesOffset = oxorany({randKey[168]});
static int32_t noob_header_imagesSize = oxorany({randKey[169]});
static int32_t noob_header_assembliesOffset = oxorany({randKey[170]});
static int32_t noob_header_assembliesSize = oxorany({randKey[171]});
static int32_t noob_header_attributeDataOffset = oxorany({randKey[172]});
static int32_t noob_header_attributeDataSize = oxorany({randKey[173]});
static int32_t noob_header_exportedTypeDefinitionsOffset = oxorany({randKey[174]});
static int32_t noob_header_exportedTypeDefinitionsSize = oxorany({randKey[175]});
static uint8_t* noob_header;
static uint8_t* noob_stringLiteral;
static uint8_t* noob_stringLiteralData;
static uint8_t* noob_string;
static uint8_t* noob_events;
static uint8_t* noob_properties;
static uint8_t* noob_methods;
static uint8_t* noob_fieldAndParameterDefaultValueData;
static uint8_t* noob_parameters;
static uint8_t* noob_fields;
static uint8_t* noob_typeDefinitions;
static uint8_t* noob_images;
static uint8_t* noob_assemblies;
static uint8_t* noob_attributeData;
static uint8_t* noob_exportedTypeDefinitions;
"));
                gdata = gdata.Replace("vm::MetadataLoader::UnloadMetadataFile(s_GlobalMetadata);", "vm::MetadataLoader::UnloadMetadataFile(s_GlobalMetadata);" + Utils.ShuffleLines(@"
FreeAndNull((void**)&noob_header);
FreeAndNull((void**)&noob_stringLiteral);
FreeAndNull((void**)&noob_stringLiteralData);
FreeAndNull((void**)&noob_string);
FreeAndNull((void**)&noob_events);
FreeAndNull((void**)&noob_properties);
FreeAndNull((void**)&noob_methods);
FreeAndNull((void**)&noob_parameters);
FreeAndNull((void**)&noob_fieldAndParameterDefaultValueData);
FreeAndNull((void**)&noob_fields);
FreeAndNull((void**)&noob_typeDefinitions);
FreeAndNull((void**)&noob_images);
FreeAndNull((void**)&noob_assemblies);
FreeAndNull((void**)&noob_attributeData);
FreeAndNull((void**)&noob_exportedTypeDefinitions);
"));
                gdata = gdata.Replace("s_GlobalMetadataHeader = (const Il2CppGlobalMetadataHeader*)s_GlobalMetadata;", @$"
int32_t metadataHeaderOffset = *(int32_t*)s_GlobalMetadata;
int32_t metadataHeaderSize = oxorany({metadataHeaderSize});
int _0 = metadataHeaderSize - metadataHeaderSize;
int _1 = metadataHeaderSize / metadataHeaderSize;
int _2 = _1 + _1;
int _3 = _2 + _1;
int _5 = _3 + _2;
int _9 = _5 + _3 + _1;
int _12 = _9 + _3;
int _15 = _12 + _3;
int _21 = _15 + _9 - _3;
int _00 = _3 - _5 + _2;
uint8_t prevByte = oxorany({randKey[1100]});

metadataHeaderOffset ^= oxorany({randKey[444]});
noob_header = (uint8_t*)malloc(metadataHeaderSize);
memcpy(noob_header, (uint8_t*)s_GlobalMetadata + metadataHeaderOffset, metadataHeaderSize);

for (int i = _0; i < metadataHeaderSize; i++)
    prevByte = noob_header[i] + (noob_header[i] ^= ((i * (oxorany({randKey[1]}) - prevByte)) + oxorany({randKey[2]})) ^ ((i >> _9) | (i << _15)), _00);

s_GlobalMetadataHeader = (const Il2CppGlobalMetadataHeader*)noob_header;
Il2CppGlobalMetadataHeader* nc_GlobalMetadataHeader = const_cast<Il2CppGlobalMetadataHeader*>(s_GlobalMetadataHeader);

" + "\n///FIX_HEADER_HERE///\n" + Utils.ShuffleLines(@$"

noob_stringLiteral = (uint8_t*)malloc(s_GlobalMetadataHeader->stringLiteralSize);
memcpy(noob_stringLiteral, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->stringLiteralOffset), s_GlobalMetadataHeader->stringLiteralSize);
prevByte = oxorany({randKey[1101]});
for (int i = _0; i < s_GlobalMetadataHeader->stringLiteralSize; i++)
    prevByte = noob_stringLiteral[i] + (noob_stringLiteral[i] ^= ((i * (oxorany({randKey[3]}) - prevByte)) + oxorany({randKey[4]})) ^ ((i >> _3) | (i << _5)), _00);

noob_stringLiteralData = (uint8_t*)malloc(s_GlobalMetadataHeader->stringLiteralDataSize);
memcpy(noob_stringLiteralData, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->stringLiteralDataOffset), s_GlobalMetadataHeader->stringLiteralDataSize);
prevByte = oxorany({randKey[1102]});
for (int i = _0; i < s_GlobalMetadataHeader->stringLiteralDataSize; i++)
    prevByte = noob_stringLiteralData[i] + (noob_stringLiteralData[i] ^= ((i * (oxorany({randKey[5]}) - prevByte)) + oxorany({randKey[6]})) ^ ((i >> _3) | (i << _5)), _00);

noob_string = (uint8_t*)malloc(s_GlobalMetadataHeader->stringSize);
memcpy(noob_string, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->stringOffset), s_GlobalMetadataHeader->stringSize);
prevByte = oxorany({randKey[1103]});
for (int i = _0; i < s_GlobalMetadataHeader->stringSize; i++)
    prevByte = noob_string[i] + (noob_string[i] ^= ((i * (oxorany({randKey[7]}) - prevByte)) + oxorany({randKey[8]})) ^ ((i >> _3) | (i << _5)), _00);

noob_events = (uint8_t*)malloc(s_GlobalMetadataHeader->eventsSize);
memcpy(noob_events, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->eventsOffset), s_GlobalMetadataHeader->eventsSize);
prevByte = oxorany({randKey[1104]});
for (int i = _0; i < s_GlobalMetadataHeader->eventsSize; i++)
    prevByte = noob_events[i] + (noob_events[i] ^= ((i * (oxorany({randKey[9]}) - prevByte)) + oxorany({randKey[10]})) ^ ((i >> _3) | (i << _5)), _00);

noob_properties = (uint8_t*)malloc(s_GlobalMetadataHeader->propertiesSize);
memcpy(noob_properties, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->propertiesOffset), s_GlobalMetadataHeader->propertiesSize);
prevByte = oxorany({randKey[1105]});
for (int i = _0; i < s_GlobalMetadataHeader->propertiesSize; i++)
    prevByte = noob_properties[i] + (noob_properties[i] ^= ((i * (oxorany({randKey[11]}) - prevByte)) + oxorany({randKey[12]})) ^ ((i >> _3) | (i << _5)), _00);

noob_methods = (uint8_t*)malloc(s_GlobalMetadataHeader->methodsSize);
memcpy(noob_methods, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->methodsOffset), s_GlobalMetadataHeader->methodsSize);
prevByte = oxorany({randKey[1106]});
for (int i = _0; i < s_GlobalMetadataHeader->methodsSize; i++)
    prevByte = noob_methods[i] + (noob_methods[i] ^= ((i * (oxorany({randKey[13]}) - prevByte)) + oxorany({randKey[14]})) ^ ((i >> _3) | (i << _5)), _00);

noob_fieldAndParameterDefaultValueData = (uint8_t*)malloc(s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataSize);
memcpy(noob_fieldAndParameterDefaultValueData, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataOffset), s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataSize);
prevByte = oxorany({randKey[1107]});
for (int i = _0; i < s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataSize; i++)
    prevByte = noob_fieldAndParameterDefaultValueData[i] + (noob_fieldAndParameterDefaultValueData[i] ^= ((i * (oxorany({randKey[15]}) - prevByte)) + oxorany({randKey[16]})) ^ ((i >> _3) | (i << _5)), _00);

noob_parameters = (uint8_t*)malloc(s_GlobalMetadataHeader->parametersSize);
memcpy(noob_parameters, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->parametersOffset), s_GlobalMetadataHeader->parametersSize);
prevByte = oxorany({randKey[1108]});
for (int i = _0; i < s_GlobalMetadataHeader->parametersSize; i++)
    prevByte = noob_parameters[i] + (noob_parameters[i] ^= ((i * (oxorany({randKey[17]}) - prevByte)) + oxorany({randKey[18]})) ^ ((i >> _3) | (i << _5)), _00);

noob_fields = (uint8_t*)malloc(s_GlobalMetadataHeader->fieldsSize);
memcpy(noob_fields, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->fieldsOffset), s_GlobalMetadataHeader->fieldsSize);
prevByte = oxorany({randKey[1109]});
for (int i = _0; i < s_GlobalMetadataHeader->fieldsSize; i++)
    prevByte = noob_fields[i] + (noob_fields[i] ^= ((i * (oxorany({randKey[19]}) - prevByte)) + oxorany({randKey[20]})) ^ ((i >> _3) | (i << _5)), _00);

noob_typeDefinitions = (uint8_t*)malloc(s_GlobalMetadataHeader->typeDefinitionsSize);
memcpy(noob_typeDefinitions, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->typeDefinitionsOffset), s_GlobalMetadataHeader->typeDefinitionsSize);
prevByte = oxorany({randKey[1110]});
for (int i = _0; i < s_GlobalMetadataHeader->typeDefinitionsSize; i++)
    prevByte = noob_typeDefinitions[i] + (noob_typeDefinitions[i] ^= ((i * (oxorany({randKey[21]}) - prevByte)) + oxorany({randKey[22]})) ^ ((i >> _3) | (i << _5)), _00);

noob_images = (uint8_t*)malloc(s_GlobalMetadataHeader->imagesSize);
memcpy(noob_images, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->imagesOffset), s_GlobalMetadataHeader->imagesSize);
prevByte = oxorany({randKey[1111]});
for (int i = _0; i < s_GlobalMetadataHeader->imagesSize; i++)
    prevByte = noob_images[i] + (noob_images[i] ^= ((i * (oxorany({randKey[23]}) - prevByte)) + oxorany({randKey[24]})) ^ ((i >> _3) | (i << _5)), _00);

noob_assemblies = (uint8_t*)malloc(s_GlobalMetadataHeader->assembliesSize);
memcpy(noob_assemblies, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->assembliesOffset), s_GlobalMetadataHeader->assembliesSize);
prevByte = oxorany({randKey[1112]});
for (int i = _0; i < s_GlobalMetadataHeader->assembliesSize; i++)
    prevByte = noob_assemblies[i] + (noob_assemblies[i] ^= ((i * (oxorany({randKey[25]}) - prevByte)) + oxorany({randKey[26]})) ^ ((i >> _3) | (i << _5)), _00);

noob_attributeData = (uint8_t*)malloc(s_GlobalMetadataHeader->attributeDataSize);
memcpy(noob_attributeData, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->attributeDataOffset), s_GlobalMetadataHeader->attributeDataSize);
prevByte = oxorany({randKey[1113]});
for (int i = _0; i < s_GlobalMetadataHeader->attributeDataSize; i++)
    prevByte = noob_attributeData[i] + (noob_attributeData[i] ^= ((i * (oxorany({randKey[27]}) - prevByte)) + oxorany({randKey[28]})) ^ ((i >> _3) | (i << _5)), _00);

noob_exportedTypeDefinitions = (uint8_t*)malloc(s_GlobalMetadataHeader->exportedTypeDefinitionsSize);
memcpy(noob_exportedTypeDefinitions, (uint8_t*)s_GlobalMetadata + (s_GlobalMetadataHeader->exportedTypeDefinitionsOffset), s_GlobalMetadataHeader->exportedTypeDefinitionsSize);
prevByte = oxorany({randKey[1114]});
for (int i = _0; i < s_GlobalMetadataHeader->exportedTypeDefinitionsSize; i++)
    prevByte = noob_exportedTypeDefinitions[i] + (noob_exportedTypeDefinitions[i] ^= ((i * (oxorany({randKey[29]}) - prevByte)) + oxorany({randKey[30]})) ^ ((i >> _3) | (i << _5)), _00);
", 5));
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->stringLiteralOffset", "noob_stringLiteral");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->stringLiteralOffset", "(noob_stringLiteral, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->stringLiteralDataOffset", "noob_stringLiteralData");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->stringLiteralDataOffset", "(noob_stringLiteralData, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->stringOffset", "noob_string");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->stringOffset", "(noob_string, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->eventsOffset", "noob_events");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->eventsOffset", "(noob_events, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->propertiesOffset", "noob_properties");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->propertiesOffset", "(noob_properties, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->methodsOffset", "noob_methods");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->methodsOffset", "(noob_methods, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataOffset", "noob_fieldAndParameterDefaultValueData");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataOffset", "(noob_fieldAndParameterDefaultValueData, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->parametersOffset", "noob_parameters");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->parametersOffset", "(noob_parameters, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->fieldsOffset", "noob_fields");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->fieldsOffset", "(noob_fields, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->typeDefinitionsOffset", "noob_typeDefinitions");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->typeDefinitionsOffset", "(noob_typeDefinitions, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->imagesOffset", "noob_images");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->imagesOffset", "(noob_images, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->assembliesOffset", "noob_assemblies");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->assembliesOffset", "(noob_assemblies, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->attributeDataOffset", "noob_attributeData");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->attributeDataOffset", "(noob_attributeData, 0");
                gdata = gdata.Replace("s_GlobalMetadata + s_GlobalMetadataHeader->exportedTypeDefinitionsOffset", "noob_exportedTypeDefinitions");
                gdata = gdata.Replace("(s_GlobalMetadata, s_GlobalMetadataHeader->exportedTypeDefinitionsOffset", "(noob_exportedTypeDefinitions, 0");
                gdata = gdata.Replace("s_GlobalMetadataHeader->stringLiteralOffset", "noob_header_stringLiteralOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->stringLiteralSize", "noob_header_stringLiteralSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->stringLiteralDataOffset", "noob_header_stringLiteralDataOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->stringLiteralDataSize", "noob_header_stringLiteralDataSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->stringOffset", "noob_header_stringOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->stringSize", "noob_header_stringSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->eventsOffset", "noob_header_eventsOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->eventsSize", "noob_header_eventsSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->propertiesOffset", "noob_header_propertiesOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->propertiesSize", "noob_header_propertiesSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->methodsOffset", "noob_header_methodsOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->methodsSize", "noob_header_methodsSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataOffset", "noob_header_fieldAndParameterDefaultValueDataOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->fieldAndParameterDefaultValueDataSize", "noob_header_fieldAndParameterDefaultValueDataSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->parametersOffset", "noob_header_parametersOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->parametersSize", "noob_header_parametersSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->fieldsOffset", "noob_header_fieldsOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->fieldsSize", "noob_header_fieldsSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->imagesOffset", "noob_header_imagesOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->imagesSize", "noob_header_imagesSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->assembliesOffset", "noob_header_assembliesOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->assembliesSize", "noob_header_assembliesSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->attributeDataOffset", "noob_header_attributeDataOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->attributeDataSize", "noob_header_attributeDataSize");
                gdata = gdata.Replace("s_GlobalMetadataHeader->exportedTypeDefinitionsOffset", "noob_header_exportedTypeDefinitionsOffset");
                gdata = gdata.Replace("s_GlobalMetadataHeader->exportedTypeDefinitionsSize", "noob_header_exportedTypeDefinitionsSize");
                gdata = gdata.Replace("///FIX_HEADER_HERE///", Utils.ShuffleLines(@$"
noob_header_stringLiteralOffset ^= nc_GlobalMetadataHeader->stringLiteralOffset;
nc_GlobalMetadataHeader->stringLiteralOffset = _0;
noob_header_stringLiteralSize ^= nc_GlobalMetadataHeader->stringLiteralSize;
nc_GlobalMetadataHeader->stringLiteralSize = _0;
noob_header_stringLiteralDataOffset ^= nc_GlobalMetadataHeader->stringLiteralDataOffset;
nc_GlobalMetadataHeader->stringLiteralDataOffset = _0;
noob_header_stringLiteralDataSize ^= nc_GlobalMetadataHeader->stringLiteralDataSize;
nc_GlobalMetadataHeader->stringLiteralDataSize = _0;
noob_header_stringOffset ^= nc_GlobalMetadataHeader->stringOffset;
nc_GlobalMetadataHeader->stringOffset = _0;
noob_header_stringSize ^= nc_GlobalMetadataHeader->stringSize;
nc_GlobalMetadataHeader->stringSize = _0;
noob_header_eventsOffset ^= nc_GlobalMetadataHeader->eventsOffset;
nc_GlobalMetadataHeader->eventsOffset = _0;
noob_header_eventsSize ^= nc_GlobalMetadataHeader->eventsSize;
nc_GlobalMetadataHeader->eventsSize = _0;
noob_header_propertiesOffset ^= nc_GlobalMetadataHeader->propertiesOffset;
nc_GlobalMetadataHeader->propertiesOffset = _0;
noob_header_propertiesSize ^= nc_GlobalMetadataHeader->propertiesSize;
nc_GlobalMetadataHeader->propertiesSize = _0;
noob_header_methodsOffset ^= nc_GlobalMetadataHeader->methodsOffset;
nc_GlobalMetadataHeader->methodsOffset = _0;
noob_header_methodsSize ^= nc_GlobalMetadataHeader->methodsSize;
nc_GlobalMetadataHeader->methodsSize = _0;
noob_header_fieldAndParameterDefaultValueDataOffset ^= nc_GlobalMetadataHeader->fieldAndParameterDefaultValueDataOffset;
nc_GlobalMetadataHeader->fieldAndParameterDefaultValueDataOffset = _0;
noob_header_fieldAndParameterDefaultValueDataSize ^= nc_GlobalMetadataHeader->fieldAndParameterDefaultValueDataSize;
nc_GlobalMetadataHeader->fieldAndParameterDefaultValueDataSize = _0;
noob_header_parametersOffset ^= nc_GlobalMetadataHeader->parametersOffset;
nc_GlobalMetadataHeader->parametersOffset = _0;
noob_header_parametersSize ^= nc_GlobalMetadataHeader->parametersSize;
nc_GlobalMetadataHeader->parametersSize = _0;
noob_header_fieldsOffset ^= nc_GlobalMetadataHeader->fieldsOffset;
nc_GlobalMetadataHeader->fieldsOffset = _0;
noob_header_fieldsSize ^= nc_GlobalMetadataHeader->fieldsSize;
nc_GlobalMetadataHeader->fieldsSize = _0;
noob_header_imagesOffset ^= nc_GlobalMetadataHeader->imagesOffset;
nc_GlobalMetadataHeader->imagesOffset = _0;
noob_header_imagesSize ^= nc_GlobalMetadataHeader->imagesSize;
nc_GlobalMetadataHeader->imagesSize = _0;
noob_header_assembliesOffset ^= nc_GlobalMetadataHeader->assembliesOffset;
nc_GlobalMetadataHeader->assembliesOffset = _0;
noob_header_assembliesSize ^= nc_GlobalMetadataHeader->assembliesSize;
nc_GlobalMetadataHeader->assembliesSize = _0;
noob_header_attributeDataOffset ^= nc_GlobalMetadataHeader->attributeDataOffset;
nc_GlobalMetadataHeader->attributeDataOffset = _0;
noob_header_attributeDataSize ^= nc_GlobalMetadataHeader->attributeDataSize;
nc_GlobalMetadataHeader->attributeDataSize = _0;
noob_header_exportedTypeDefinitionsOffset ^= nc_GlobalMetadataHeader->exportedTypeDefinitionsOffset;
nc_GlobalMetadataHeader->exportedTypeDefinitionsOffset = _0;
noob_header_exportedTypeDefinitionsSize ^= nc_GlobalMetadataHeader->exportedTypeDefinitionsSize;
nc_GlobalMetadataHeader->exportedTypeDefinitionsSize = _0;
", 2));
            }
            igdata = Regex.Replace(igdata, @"/\*.*?\*/", "", RegexOptions.Singleline);
            igdata = Regex.Replace(igdata, @"//.*?$", "", RegexOptions.Multiline);
            foreach (var kvp in new[] { ("TypeIndex", "int32_t"), ("TypeDefinitionIndex", "int32_t"), ("FieldIndex", "int32_t"), ("DefaultValueIndex", "int32_t"), ("DefaultValueDataIndex", "int32_t"), ("CustomAttributeIndex", "int32_t"), ("ParameterIndex", "int32_t"), ("MethodIndex", "int32_t"), ("GenericMethodIndex", "int32_t"), ("PropertyIndex", "int32_t"), ("EventIndex", "int32_t"), ("GenericContainerIndex", "int32_t"), ("GenericParameterIndex", "int32_t"), ("GenericParameterConstraintIndex", "int16_t"), ("NestedTypeIndex", "int32_t"), ("InterfacesIndex", "int32_t"), ("VTableIndex", "int32_t"), ("RGCTXIndex", "int32_t"), ("StringIndex", "int32_t"), ("StringLiteralIndex", "int32_t"), ("GenericInstIndex", "int32_t"), ("ImageIndex", "int32_t"), ("AssemblyIndex", "int32_t"), ("InteropDataIndex", "int32_t"), ("TypeFieldIndex", "int32_t"), ("TypeMethodIndex", "int32_t"), ("MethodParameterIndex", "int32_t"), ("TypePropertyIndex", "int32_t"), ("TypeEventIndex", "int32_t"), ("TypeInterfaceIndex", "int32_t"), ("TypeNestedTypeIndex", "int32_t"), ("TypeInterfaceOffsetIndex", "int32_t"), ("GenericContainerParameterIndex", "int32_t"), ("AssemblyTypeIndex", "int32_t"), ("AssemblyExportedTypeIndex", "int32_t") })
            {
                igdata = Regex.Replace(igdata, $" {kvp.Item1} ", $" {kvp.Item2} ");
            }
            igdata = headerShuffler.Shuffle(igdata);
            if (Settings.cfg.StripMono)
            {
                File.WriteAllText(il2cppPath + $"{PathSeparator}il2cpp-mono-api.cpp", "");
                File.WriteAllText(il2cppPath + $"{PathSeparator}il2cpp-mono-api.h", "");
                File.WriteAllText(il2cppPath + $"{PathSeparator}il2cpp-mono-api-functions.h", "");
            }
            capiddata = capiddata.Replace("#include \"il2cpp-api.h\"\n", "#include \"il2cpp-api.h\"\n" + (Settings.cfg.EncryptKeys ? "#include \"oxorany.h\"\n" : "#define oxorany(a) a\n"));
            gdata = gdata.Replace("#include \"GlobalMetadataFileInternals.h\"\n", "#include \"GlobalMetadataFileInternals.h\"\n" + (Settings.cfg.EncryptKeys ? "#include \"oxorany.h\"\n" : "#define oxorany(a) a\n"));
            File.WriteAllText(capipath, capiddata);
            File.WriteAllText(cipath, cidata);
            File.WriteAllText(gpath, gdata);
            File.WriteAllText(igpath, igdata);
            if (Settings.cfg.RenameSymbols)
            {
                hapiddata = symbolRenamer.ProcessFiles(il2cppPath, hapiddata);
            }
            File.WriteAllText(hapipath, hapiddata);
            Utils.LOGD("Il2Cpp api modified at " + il2cppPath);
        }

        private static void BackupApi()
        {
            string[] allFiles = Directory.GetFiles(il2cppPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext != ".h" && ext != ".cpp") continue;
                try
                {
                    Utils.BackupFile(file);
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    content = Utils.RemoveComments(content);
                    File.WriteAllText(file, content, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Utils.LOGE($"Error processing {file}: {ex.Message}");
                }
            }
        }

        private static void RestoreApi()
        {
            Utils.RestoreAllBaks(il2cppPath);
            //File.Delete(il2cppPath + "{PathSeparator}oxorany.cpp");
            //File.Delete(il2cppPath + "{PathSeparator}oxorany.h");
            Utils.LOGD("Il2cpp api restored");
        }

        private static int GetHeaderValue(ref byte[] data, string fieldName, bool ignore = false)
        {
            int pos = Utils.GetLine(headerStructBody, $"int32_t {fieldName};", ignore);
            if (pos < 0) return -1;
            return BitConverter.ToInt32(data, metadataHeaderOffset + pos * 4);
        }

        private static void SetHeaderValue(ref byte[] data, string fieldName, int value)
        {
            if (fieldName.Length <= 1) return;
            int pos = Utils.GetLine(headerStructBody, $"int32_t {fieldName};");
            BitConverter.GetBytes(value).CopyTo(data, metadataHeaderOffset + pos * 4);
        }

        private static void ResetFields()
        {
            Utils.ResetStaticFields(typeof(NoobK1ller.Service));
            enable = false;
            randKey = new int[2000];
            for (int i = 0; i < 1000; i++)
            {
                int r = 0;
                do
                {
                    r = rnd.Next(1111182398, 2111183647);
                } while (randKey.Contains(r));
                randKey[i] = r;
            }
            int prevr = 0;
            for (int i = 1000; i < 2000; i++)
            {
                do
                {
                    randKey[i] = rnd.Next(11, 256);
                } while(prevr == randKey[i]);
                prevr = randKey[i];
            }
            metadataHeaderSize = 0;
            headerStructBody = "";
            symbolRenamer = new Il2CppSymbolRenamer();
            headerShuffler = new Il2CppStructShuffler("Il2CppGlobalMetadataHeader");
            fieldsInited = true;
        }
    }
}

#endif
