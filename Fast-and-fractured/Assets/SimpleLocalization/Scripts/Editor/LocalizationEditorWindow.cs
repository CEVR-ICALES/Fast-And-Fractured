using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace Assets.SimpleLocalization.Scripts.Editor
{
    public class LocalizationEditorWindow : EditorWindow
    {
        public string SheetName;
        public long SheetId;

        private static LocalizationEditorWindow _window;
        private static LocalizationEditor Editor => LocalizationEditor.Instance;
        private static LocalizationSettings Settings => LocalizationSettings.Instance;
        
        private static int _columnWidth = 200;
        private static readonly Dictionary<string, Rect> LanguageButtons = new();

        private int _selectedSheetIndex;
        private Vector2 _scrollPosition;
        private DateTime _timeStamp;

        private string _filter = "";

        private const int MinColumnWidth = 200;
        private const int MaxColumnWidth = 500;
        private const int ButtonsColumnSizeFix = 7;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            LocalizationSettings.OnRunEditor += Open;
        }

        [MenuItem("Window/◆ Simple Localization/Editor [PRO]")]
        public static void Open()
        {
            if (_window == null)
            {
                _window = CreateInstance<LocalizationEditorWindow>();
                _window.titleContent = new GUIContent("Localization Editor");
                _window.minSize = new Vector2(1060, 200);
            }

            Editor.LoadSetting();
            _window.Show();
        }

        public void OnGUI()
        {
            if (Settings.Sheets.Count == 0 || Editor.SheetNames.Count == 0 || string.IsNullOrEmpty(Settings.TableId) || Settings.SaveFolder == null)
            {
                Close();

                if (EditorUtility.DisplayDialog("Error", "Wrong settings. Reopen?", "Yes", "No"))
                {
                    Open();
                }

                return;
            }
            
            if (Directory.GetFiles(AssetDatabase.GetAssetPath(Settings.SaveFolder)).Length == 0)
            {
                if ((DateTime.UtcNow - LocalizationSettings.Timestamp).TotalSeconds < 10)
                {
                    return;
                }
                else if (EditorUtility.DisplayDialog("Error", "Nothing to edit. You need to download sheets in the Settings window!", "OK"))
                {
                    Close();
                }

                return;
            }

            EditorGUILayout.Space();

            SheetName = MakeSheetsDropdown(Editor.SheetNames);

            if (Editor.SheetDictionary.Count == 0)
            {
                SheetId = Editor.SheetIds.ElementAt(_selectedSheetIndex);

                if (!Editor.ReadSorted(SheetName))
                {
                    Close();
                }

                if (Editor.SheetDictionary.Count == 0)
                {
                    return;
                }
            }

            EditorGUILayout.Space();
            MakeKeysDropdown();
            EditorGUILayout.Space();
            MakeFilterRow();
            EditorGUILayout.Space();
            MakeTable();
            EditorGUILayout.Space();

            if (Editor.KeysActions.Count > 0)
            {
                MakeBottomMenu();
            }
        }
        
        public void OnDestroy()
        {
            if (Editor.KeysActions.Count > 0)
            {
                if (EditorUtility.DisplayDialog("Simple Localization", "Do you want to submit changes before exiting?", "Yes", "No"))
                {
                    var keys = Editor.Keys.ToDictionary(entry => entry.Key, entry => entry.Value);
                    var sheetDictionary = Editor.SheetDictionary.ToDictionary(entry => entry.Key, entry => entry.Value);
                    var keysActions = Editor.KeysActions.ToDictionary(entry => entry.Key, entry => entry.Value);

                    EditorCoroutineUtility.StartCoroutine(SaveSheet(keys, keysActions, sheetDictionary), this);
                }
            }

            Editor.ResetSheet();
        }

        private void MakeFilterRow()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key filter:", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix));

            _filter = EditorGUILayout.TextField(_filter, GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix));

            GUILayout.EndHorizontal();
        }

        private void MakeTable()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.ExpandWidth(true));
            MakeLanguagesRow();

            if (Editor.CurrentKey != "")
            {
                MakeSheetRow(Editor.CurrentKey);
            }
            
            GUILayout.BeginHorizontal("box");

            if (GUILayout.Button("−", GUILayout.Width(25)))
            {
                _columnWidth -= 25;
            }

            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                _columnWidth += 25;
            }

            _columnWidth = Math.Max(_columnWidth, MinColumnWidth);
            _columnWidth = Math.Min(_columnWidth, MaxColumnWidth);

            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private static void MakeLanguagesRow()
        {
            var style = new GUIStyle { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };

            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal("box", GUILayout.MinWidth(MinColumnWidth)); 
            GUILayout.Label("Key" + (Editor.CurrentKey == "" ? "" : Editor.Keys[Editor.CurrentKey] != Editor.CurrentKey ? " *" : ""), style, GUILayout.MinWidth(MinColumnWidth), GUILayout.MinHeight(20));
            GUILayout.EndHorizontal();

            foreach (var language in Editor.SheetDictionary.Keys)
            {
                GUILayout.BeginHorizontal("box", GUILayout.MinWidth(_columnWidth - 30));
                GUILayout.Label(language + (language == LocalizedTranslate.PrimaryLanguage ? " [Primary]" : ""), style, GUILayout.MinWidth(_columnWidth - 28), GUILayout.MinHeight(20));

                if (GUILayout.Button("︰", GUILayout.Width(25)))
                {
                    LocalizedTranslate.DestinationLanguage = language;
                    PopupWindow.Show(LanguageButtons[language], new LocalizedTranslate());
                }

                if (Event.current.type == EventType.Repaint) LanguageButtons[language] = GUILayoutUtility.GetLastRect();

                GUILayout.EndHorizontal();
            }

            GUILayout.EndHorizontal();
        }

        private static void MakeSheetRow(string key)
        {
            GUILayout.BeginHorizontal();

            var newValue = EditorGUILayout.TextField(Editor.Keys[key], GUILayout.MinHeight(50), GUILayout.MaxWidth(MinColumnWidth), GUILayout.MinWidth(MinColumnWidth + 7)); 
            
            if (Editor.Keys[key] != newValue)
            {
                var duplicateKey = Editor.Keys.ContainsKey(newValue) ? Editor.Keys[newValue] : Editor.Keys.Values.Contains(newValue) ? Editor.Keys.First(i => i.Value == newValue).Key : "";

                if (duplicateKey != "" && Editor.KeysActions[duplicateKey] != ActionType.Delete || Editor.Keys.Count(i => i.Value == newValue) > 1)
                {
                    EditorUtility.DisplayDialog("Error", "A key with the same name already exists in the table!", "OK");
                }
                else
                {
                    Editor.Keys[key] = newValue;

                    if (!Editor.IsNewKey(key) && !Editor.KeysActions.ContainsKey(key))
                    {
                        Editor.KeysActions.Add(key, ActionType.Edit);
                    }
                }
            }

            foreach (var language in Editor.SheetDictionary.Keys)
            {
                var value = EditorGUILayout.TextArea(Editor.SheetDictionary[language][key], GUILayout.MinHeight(50), GUILayout.MaxWidth(_columnWidth), GUILayout.MinWidth(_columnWidth + 7));

                if (value == Editor.SheetDictionary[language][key]) continue;

                Editor.SheetDictionary[language][key] = value;

                if (!Editor.IsNewKey(key) && !Editor.KeysActions.ContainsKey(key))
                {
                    Editor.KeysActions.Add(key, ActionType.Edit);
                }
            }

            GUILayout.EndHorizontal();
        }

        private void MakeKeysDropdown()
        {
            GUILayout.BeginHorizontal();
            Editor.GetAllKeys();

            var filter = _filter.ToLower();
            var keysArray = filter.Length > 2
                ? Editor.Keys.Keys.Where(originalKey => !IsDeleted(originalKey) && originalKey.ToLower().Contains(filter) || originalKey == Editor.CurrentKey).ToArray()
                : Editor.Keys.Keys.Where(originalKey => !IsDeleted(originalKey)).ToArray();
            var valuesArray = Editor.Keys.Where(i => keysArray.Contains(i.Key)).Select(i => i.Value).ToArray();
            var selectedKeyArrayIndex = string.IsNullOrEmpty(Editor.CurrentKey) ? 0 : Array.IndexOf((Array) keysArray, Editor.CurrentKey);

            EditorGUILayout.LabelField("Key:", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix));
            selectedKeyArrayIndex = EditorGUILayout.Popup(selectedKeyArrayIndex, valuesArray, GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix));
            Editor.CurrentKey = keysArray.Length == 0 ? "" : keysArray[selectedKeyArrayIndex];

            if (GUILayout.Button("Add Key", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix)))
            {
                Editor.CurrentKey = Editor.AddKey();
            }

            if (GUILayout.Button("Delete Key", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix)))
            {
                if (EditorUtility.DisplayDialog("Simple Localization", "Do you want to delete this key?", "Yes", "No"))
                {
                    Editor.CurrentKey = Editor.DeleteKey(Editor.CurrentKey);
                }
            }

            if (GUILayout.Button("Auto Translate", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix)))
            {
                EditorCoroutineUtility.StartCoroutine(new LocalizedTranslate().TranslateAuto(), this);
            }

            GUILayout.EndHorizontal();

            bool IsDeleted(string key)
            {
                return Editor.KeysActions.ContainsKey(key) && Editor.KeysActions[key] == ActionType.Delete;
            }
        }

        private string MakeSheetsDropdown(List<string> sheetNames)
        {
            GUILayout.BeginHorizontal();

            var sheetNamesArray = sheetNames.ToArray(); 
            
            EditorGUILayout.LabelField("Sheet:", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix));
            
            var newSheetIndex = EditorGUILayout.Popup(_selectedSheetIndex, sheetNamesArray, GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix));
            
            if (GUILayout.Button("▼ Download Sheets", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix)))
            {
                Settings.DownloadGoogleSheets(Editor.ResetSheet);
            }

            if (GUILayout.Button("❖ Open Sheets", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix)))
            {
                Settings.OpenGoogleSheets();
            }

            if (GUILayout.Button("★ Leave Review", GUILayout.MinWidth(MinColumnWidth + ButtonsColumnSizeFix), GUILayout.MaxWidth(MinColumnWidth + ButtonsColumnSizeFix)))
            {
                Settings.LeaveReview();
            }

            GUILayout.EndHorizontal();

            if (_selectedSheetIndex == newSheetIndex) return sheetNamesArray[_selectedSheetIndex];

            if (Editor.KeysActions.Count > 0)
            {
                if (EditorUtility.DisplayDialog("Simple Localization", "Changes will be lost when switching sheet!\nDo you want to submit changes?", "Yes", "No"))
                {
                    EditorCoroutineUtility.StartCoroutine(SaveSheet(Editor.Keys, Editor.KeysActions, Editor.SheetDictionary), this);
                }
            }

            Editor.ResetSheet();
            _selectedSheetIndex = newSheetIndex;

            return sheetNamesArray[_selectedSheetIndex];
        }

        public void MakeBottomMenu()
        {
            GUILayout.BeginHorizontal("box");
            
            if (GUILayout.Button("Submit", GUILayout.MaxWidth(MinColumnWidth + 3)))
            {
                EditorCoroutineUtility.StartCoroutine(SaveSheet(Editor.Keys, Editor.KeysActions, Editor.SheetDictionary), this);
            }

            if (GUILayout.Button("Revert", GUILayout.MaxWidth(MinColumnWidth + 3)) && EditorUtility.DisplayDialog("Simple Localization", "Do you want to revert changes?", "Yes", "No"))
            {
                Editor.ResetSheet();
            }

            GUILayout.Label($"Pending submit of {Editor.KeysActions.Count} changes", new GUIStyle { normal = { textColor = Color.gray } });
            GUILayout.EndHorizontal();
        }

        public static bool IsPro()
        {
            if (Constants.LocalizationEditorUrl != "") return true;

            if (EditorUtility.DisplayDialog("SimpleLocalization", "This feature is available in the PRO version only!\nOpen a store website with it?", "Yes", "No"))
            {
                Application.OpenURL(Constants.AssetUrlPro);
            }

            return false;
        }

        private IEnumerator SaveSheet(Dictionary<string, string> keys, Dictionary<string, ActionType> actionTypes, Dictionary<string, SortedDictionary<string, string>> sheetDictionary)
        {
            if (!IsPro()) yield break;

            if ((DateTime.UtcNow - _timeStamp).TotalSeconds < 2)
            {
                if (EditorUtility.DisplayDialog("Error", "Too many requests! Try again later.", "OK"))
                {
                    yield break;
                }

                yield break;
            }

            _timeStamp = DateTime.UtcNow;
            
            var rows = new List<Dictionary<string, string>>();

            foreach (var action in actionTypes)
            {
                rows.Add(LocalizationUtils.CreateRow(action.Key, action.Value, keys, sheetDictionary));
            }

            var success = false;

            Editor.PrevKey = Editor.CurrentKey;

            yield return LocalizationUtils.SubmitChanges(rows, SheetId, Settings.TableId, Constants.LocalizationEditorUrl, () => success = true);

            if (!success)
            {
                yield break;
            }

            yield return Settings.DownloadGoogleSheetsCoroutine(() =>
            {
                Editor.ResetSheet();
                Editor.ReadSorted(SheetName);
                EditorUtility.DisplayDialog("Message", "Changes submitted!", "OK");
            }, silent: true);
        }
    }
}
