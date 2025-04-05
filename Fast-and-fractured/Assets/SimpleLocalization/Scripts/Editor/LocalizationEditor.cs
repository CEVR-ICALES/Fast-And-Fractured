using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Assets.SimpleLocalization.Scripts.Editor
{
    public class LocalizationEditor
    {
        public Dictionary<string, SortedDictionary<string, string>> SheetDictionary = new();
        public List<long> SheetIds = new();
        public List<string> SheetNames = new();

        public readonly Dictionary<string, ActionType> KeysActions = new();
        public readonly Dictionary<string, string> Keys = new();

        public string CurrentKey;
        public string PrevKey = "";

        public static LocalizationEditor Instance => _instance ??= new LocalizationEditor();

        private static LocalizationEditor _instance;

        private static LocalizationSettings Settings => LocalizationSettings.Instance;
        
        private static string SheetFileName(string sheetName) => AssetDatabase.GetAssetPath(Settings.SaveFolder) + "/" + sheetName + ".csv";

        public void LoadSetting()
        {
            SheetNames.Clear();
            SheetIds.Clear();

            Settings.Sheets.ForEach(i =>
            {
                SheetNames.Add(i.Name);
                SheetIds.Add(i.Id);
            });
        }

        public bool ReadSorted(string sheetName)
        {
            SheetDictionary.Clear();

            var fileName = SheetFileName(sheetName);

            if (!File.Exists(fileName))
            {
                if (EditorUtility.DisplayDialog("Error", $"File not found: {fileName}!\nPlease check your Settings and download sheets.", "OK"))
                {
                    return false;
                }
            }

            var lines = LocalizationManager.GetLines(File.ReadAllText(fileName));
            var languages = lines[0].Split(',').Select(i => i.Trim()).ToList();

            for (var i = 1; i < languages.Count; i++)
            {
                if (!SheetDictionary.ContainsKey(languages[i]))
                {
                    SheetDictionary.Add(languages[i], new SortedDictionary<string, string>());
                }
            }

            for (var i = 1; i < lines.Count; i++)
            {
                var columns = LocalizationManager.GetColumns(lines[i]);
                var key = columns[0];

                if (key == "") continue;

                for (var j = 1; j < languages.Count; j++)
                {
                    SheetDictionary[languages[j]].Add(key, columns[j]);
                }
            }

            return true;
        }

        public bool IsNewKey(string key)
        {
            return SheetDictionary.FirstOrDefault().Value.ContainsKey(key) && KeysActions.ContainsKey(key) && KeysActions[key] == ActionType.Add;
        }

        public string DeleteKey(string key)
        {
            if (IsNewKey(key))
            {
                KeysActions.Remove(key);
                Keys.Remove(key);
            }
            else
            {
                if (KeysActions.ContainsKey(key))
                {
                    KeysActions[key] = ActionType.Delete;
                }
                else
                {
                    KeysActions.Add(key, ActionType.Delete);
                }
            }

            foreach (var language in SheetDictionary.Keys)
            {
                SheetDictionary[language].Remove(key);
            }

            return "";
        }

        public string AddKey()
        {
            var key = $"New_key_{Guid.NewGuid().ToString().Substring(0, 8)}";

            Keys.Add(key, key);

            foreach (var language in SheetDictionary.Keys)
            {
                SheetDictionary[language].Add(key, "");
            }

            KeysActions.Add(key, ActionType.Add);

            return key;
        }
        
        public void ResetSheet()
        {
            CurrentKey = PrevKey != "" ? Keys[PrevKey] : PrevKey;
            SheetDictionary.Clear();
            Keys.Clear();
            KeysActions.Clear();
            PrevKey = "";
        }

        public void GetAllKeys()
        {
            if (Keys.Count != 0) return;

            var first = SheetDictionary.First();

            foreach (var keys in first.Value.Keys)
            {
                Keys.Add(keys, keys);
            }
        }
    }
}