#if UNITY_EDITOR

using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NoobK1ller
{
    [Serializable]
    public class ConfigData
    {
        public bool Enable = false;
        public bool EncryptData = false;
        public bool EncryptKeys = false;
        public bool StripMono = false;
        public bool RenameSymbols = false;
        public bool ClearBeeCache = false;
    }

    public static class Settings
    {
        public const string nver = "1.0.1";
        public const bool liteVersion = true;
        public static char PathSeparator = Path.DirectorySeparatorChar;
        public static string cfgPath = $"Assets{PathSeparator}Editor{PathSeparator}NoobK1ller{PathSeparator}cfg.json";
        public static ConfigData cfg = GetConfig();

        private static ConfigData GetConfig()
        {
            if (!File.Exists(cfgPath))
            {
                File.WriteAllText(cfgPath, JsonUtility.ToJson(new ConfigData(), true));
            }
            try
            {
                return JsonUtility.FromJson<ConfigData>(File.ReadAllText(cfgPath));
            }
            catch
            {
                File.WriteAllText(cfgPath, JsonUtility.ToJson(new ConfigData(), true));
                return JsonUtility.FromJson<ConfigData>(File.ReadAllText(cfgPath));
            }
        }

        public static void ReloadConfig()
        {
            cfg = GetConfig();
        }

        public static void SaveConfig()
        {
            if (cfg == null) return;
            string json = JsonUtility.ToJson(cfg, true);
            File.WriteAllText(cfgPath, json);
        }
    }

    public class ConfigWindow : EditorWindow
    {
        private static Vector2 windowSize = new Vector2(350f, 350f);

        [MenuItem("Tools/Noob K1ller config")]
        public static void Open()
        {
            string title = "Noob K1ller v" + Settings.nver.ToString() + (Settings.liteVersion ? " (lite)" : "");
            ConfigWindow window = GetWindow<ConfigWindow>(false, title);
            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            minSize = windowSize;
            maxSize = windowSize;
            void SetMargin(VisualElement element)
            {
                element.style.marginTop = element.style.marginRight = element.style.marginLeft = 4f;
                element.style.marginBottom = 0f;
            }
            FieldT Add<ValueT, FieldT>(VisualElement root, ValueT v, string label, Action<ValueT> callback, string tooltip = null, bool enable = false) where FieldT : BaseField<ValueT>, new()
            {
                FieldT element = new FieldT
                {
                    label = label,
                    tooltip = tooltip
                };
                SetMargin(element);
                element.Q<Label>().style.minWidth = 192f;
                element.SetValueWithoutNotify(v);
                element.SetEnabled(enable);
                element.RegisterValueChangedCallback<ValueT>(e =>
                {
                    callback.Invoke(e.newValue);
                    Settings.SaveConfig();
                    if (typeof(ValueT) != typeof(string)) {
                        root.Clear();
                        CreateGUI();
                    }
                });
                root.Add(element);
                return element;
            }
            void AddButton(VisualElement root, string text, Action callback, string tooltip = null, bool enable = true)
            {
                Button button = new Button()
                {
                    text = text,
                    tooltip = tooltip
                };
                SetMargin(button);
                button.style.paddingTop = button.style.paddingBottom = button.style.paddingRight = button.style.paddingLeft = 4f;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.SetEnabled(enable);
                root.Add(button);
                button.clicked += callback;
            }
            void AddSpacer(VisualElement root, float height)
            {
                VisualElement spacer = new VisualElement();
                if (height <= 0f)
                {
                    spacer.style.flexGrow = 1f;
                }
                else
                {
                    spacer.style.height = height;
                }
                root.Add(spacer);
            }
            Add<bool, Toggle>(root, Settings.cfg.Enable, "<b>Enable</b>", v => Settings.cfg.Enable = v, null, true);
            Add<bool, Toggle>(root, Settings.cfg.EncryptData, "Encrypt Data", v => Settings.cfg.EncryptData = v, "Encrypts main metadata sections to prevent easy dumping.", Settings.cfg.Enable);
            Add<bool, Toggle>(root, Settings.cfg.EncryptKeys, "Encrypt Keys", v => Settings.cfg.EncryptKeys = v, "Encrypts keys used to decrypt metadata sections, which makes reversing much harder. Requires C++14.", Settings.cfg.Enable);
            Add<bool, Toggle>(root, Settings.cfg.RenameSymbols, "Rename IL2CPP Exports", v => Settings.cfg.RenameSymbols = v, "Renames IL2CPP API exports to make runtime dumping a bit harder.", Settings.cfg.Enable);
            Add<bool, Toggle>(root, Settings.cfg.StripMono, "Strip Mono Symbols", v => Settings.cfg.StripMono = v, "Removes Mono exports to reduce binary size and make reversing harder.", Settings.cfg.Enable);
            Add<bool, Toggle>(root, Settings.cfg.ClearBeeCache, "Force Clear Bee Cache", v => Settings.cfg.ClearBeeCache = v, "Clears build cache before building. Enable only if you encounter build errors.", Settings.cfg.Enable);
            AddSpacer(root, 0f);
            AddButton(root, "<b>Restore IL2CPP</b>", () =>
            {
                int r = Utils.RestoreAllBaks(Utils.GetIl2CppPath());
                Utils.LOGD("Restored " + r.ToString() + " il2cpp files");
            }, "Restore IL2CPP API files from backups");
            AddButton(root, "<b>Check For Updates</b>", () =>
            {
                string url = Settings.liteVersion ? "https://raw.githubusercontent.com/id9909/NoobK1ller/refs/heads/main/actualLite.ver" : "https://raw.githubusercontent.com/id9909/NoobK1ller/refs/heads/main/actual.ver";
                System.Net.WebClient client = new System.Net.WebClient();
                client.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error == null)
                    {
                        string result = e.Result;
                        while (result.EndsWith(" ") || result.EndsWith("\n")) result = result.TrimEnd();
                        //Utils.LOGD("Actual version: " + result + "\nUser version: " + Settings.nver);
                        if (result == Settings.nver)
                        {
                            Utils.LOGD("You have the latest version");
                        }
                        else
                        {
                            Utils.LOGW("New version " + result + " found");
                        }
                    }
                    else
                    {
                        Utils.LOGW("Get version error: " + e.Error.Message);
                    }
                    client.Dispose();
                };
                client.DownloadStringAsync(new System.Uri(url));
            }, "Check for plugin updates.");
            AddButton(root, "<b>Contact Developer</b>", () =>
            {
                Application.OpenURL("https://t.me/id9909");
            }, "Open Telegram contact (@id9909 / @uid9909)");
            AddSpacer(root, 8f);
        }
    }
}

#endif
