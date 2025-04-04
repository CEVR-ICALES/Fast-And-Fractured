using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR

using UnityEditor;
using Unity.EditorCoroutines.Editor;

#endif

namespace Assets.SimpleLocalization.Scripts
{
    [CreateAssetMenu(fileName = "LocalizationSettings", menuName = "◆ Simple Localization/Settings")]
    public class LocalizationSettings : ScriptableObject
    {
        /// <summary>
        /// Table Id on Google Sheets.
        /// Let's say your table has the following URL https://docs.google.com/spreadsheets/d/1RvKY3VE_y5FPhEECCa5dv4F7REJ7rBtGzQg9Z_B_DE4/edit#gid=331980525
        /// In this case, Table Id is "1RvKY3VE_y5FPhEECCa5dv4F7REJ7rBtGzQg9Z_B_DE4" and Sheet Id is "331980525" (the gid parameter).
        /// </summary>
        public string TableId;

        public List<Sheet> Sheets = new();

        public UnityEngine.Object SaveFolder;

        public static string UrlPattern = "https://docs.google.com/spreadsheets/d/{0}/export?format=csv&gid={1}";

        public static DateTime Timestamp;

        public static LocalizationSettings Instance
        {
            get
            {
                if (_instance == null) _instance = LoadSettings();

                return _instance;
            }
        }

        public static event Action OnRunEditor = () => {};

        private static LocalizationSettings _instance;

        private static LocalizationSettings LoadSettings()
        {
            const string path = @"Assets/SimpleLocalization/Resources/LocalizationSettings.asset";

            var settings = Resources.Load<LocalizationSettings>(Path.GetFileNameWithoutExtension(path));

            if (settings != null) return settings;

            #if UNITY_EDITOR

            settings = CreateInstance<LocalizationSettings>();

            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            #else

            throw new Exception($"Localization settings not found: {path}");

            #endif

            return settings;
        }

        #if UNITY_EDITOR

        public void Awake()
        {
            if (string.IsNullOrEmpty(TableId) && Sheets == null && SaveFolder == null)
            {
                Reset();
            }
        }

        public void DownloadGoogleSheets(Action callback = null)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(DownloadGoogleSheetsCoroutine(callback));
        }
        
        public IEnumerator DownloadGoogleSheetsCoroutine(Action callback = null, bool silent = false)
        {
            if (string.IsNullOrEmpty(TableId) || Sheets.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Table Id is empty.", "OK");
                yield break;
            }

            if (SaveFolder == null)
            {
                EditorUtility.DisplayDialog("Error", "Save Folder is not set.", "OK");
                yield break;
            }

            if (Sheets.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Sheets are empty.", "OK");
                yield break;
            }

            if ((DateTime.UtcNow - Timestamp).TotalSeconds < 2)
            {
                if (EditorUtility.DisplayDialog("Message", "Too many requests! Try again later.", "OK"))
                {
                    yield break;
                }
            }
            
            Timestamp = DateTime.UtcNow;

            if (!silent) ClearSaveFolder();

            for (var i = 0; i < Sheets.Count; i++)
            {
                var sheet = Sheets[i];
                var url = string.Format(UrlPattern, TableId, sheet.Id);

                Debug.Log($"Downloading <color=grey>{url}</color>");

                var request = UnityWebRequest.Get(url);
                var progress = (float) (i + 1) / Sheets.Count;

                if (EditorUtility.DisplayCancelableProgressBar("Downloading sheets...", $"[{(int)(100 * progress)}%] [{i + 1}/{Sheets.Count}] Downloading {sheet.Name}...", progress))
                {
                    yield break;
                }

                yield return request.SendWebRequest();

                var error = request.error ?? (request.downloadHandler.text.Contains("signin/identifier") ? "It seems that access to this document is denied." : null);

                if (string.IsNullOrEmpty(error))
                {
                    var path = Path.Combine(AssetDatabase.GetAssetPath(SaveFolder), sheet.Name + ".csv");

                    File.WriteAllBytes(path, request.downloadHandler.data);
                    AssetDatabase.Refresh();
                    Sheets[i].TextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    EditorUtility.SetDirty(this);
                    Debug.LogFormat($"Sheet <color=yellow>{sheet.Name}</color> ({sheet.Id}) saved to <color=grey>{path}</color>");
                }
                else
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Error", error.Contains("404") ? "Table Id is wrong!" : error, "OK");

                    yield break;
                }
            }

            yield return null;

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            callback?.Invoke();

            if (!silent) EditorUtility.DisplayDialog("Message", $"{Sheets.Count} localization sheets downloaded!", "OK");

            void ClearSaveFolder()
            {
                var files = Directory.GetFiles(AssetDatabase.GetAssetPath(SaveFolder));

                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }

        public void OpenGoogleSheets()
        {
            if (string.IsNullOrEmpty(TableId))
            {
                Debug.LogWarning("Table ID is empty.");
            }
            else
            {
                Application.OpenURL(string.Format(Constants.TableUrlPattern, TableId));
            }
        }

        public void LeaveReview()
        {
            Application.OpenURL(Constants.LocalizationEditorUrl == "" ? Constants.AssetUrlFree : Constants.AssetUrlPro);
        }

        public void Reset()
        {
            TableId = Constants.ExampleTableId;
            Sheets = Constants.ExampleSheets.Select(i => new Sheet { Name = i.Key, Id = i.Value }).ToList();
            SaveFolder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(@"Assets\SimpleLocalization\Resources\Localization");
        }

        public void ResolveGoogleSheets()
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(ResolveGoogleSheetsCoroutine());

            IEnumerator ResolveGoogleSheetsCoroutine()
            {
                if (string.IsNullOrEmpty(TableId))
                {
                    EditorUtility.DisplayDialog("Error", "Table Id is empty.", "OK");

                    yield break;
                }

                using var request = UnityWebRequest.Get($"{Constants.SheetResolverUrl}?tableUrl={TableId}");

                if (EditorUtility.DisplayCancelableProgressBar("Resolving sheets...", "Executing Google App Script...", 1))
                {
                    yield break;
                }

                yield return request.SendWebRequest();

                EditorUtility.ClearProgressBar();

                if (request.error == null)
                {
                    var error = GetInternalError(request);

                    if (error != null)
                    {
                        EditorUtility.DisplayDialog("Error", "Table not found or public read permission not set.", "OK");

                        yield break;
                    }

                    var sheetsDict = JsonConvert.DeserializeObject<Dictionary<string, long>>(request.downloadHandler.text);

                    if (sheetsDict == null) throw new NullReferenceException(nameof(sheetsDict));

                    Sheets.Clear();

                    foreach (var item in sheetsDict)
                    {
                        Sheets.Add(new Sheet { Id = item.Value, Name = item.Key });
                    }

                    EditorUtility.DisplayDialog("Message", $"{Sheets.Count} sheets resolved: {string.Join(", ", Sheets.Select(i => i.Name))}.", "OK");
                }
                else
                {
                    throw new Exception(request.error);
                }
            }
        }

        public static string GetInternalError(UnityWebRequest request)
        {
            var matches = Regex.Matches(request.downloadHandler.text, @">(?<Message>.+?)<\/div>");

            if (matches.Count == 0 && !request.downloadHandler.text.Contains("Google Script ERROR:")) return null;

            var error = matches.Count > 0 ? matches[1].Groups["Message"].Value.Replace("quot;", "") : request.downloadHandler.text;

            return error;
        }

        public void DisplayHelp()
        {
            EditorGUILayout.HelpBox("1. Set Table Id and Save Folder\n2. Press Resolve Sheets*\n3. Press Download Sheets\n*You can set Sheets manually: fill Name and Id, leave Text Asset empty", MessageType.None);
        }

        public void DisplayButtons()
        {
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fixedHeight = 30 };

            if (GUILayout.Button("↺ Resolve Sheets", buttonStyle))
            {
                ResolveGoogleSheets();
            }

            if (GUILayout.Button("▼ Download Sheets", buttonStyle))
            {
                DownloadGoogleSheets();
            }

            if (GUILayout.Button("❖ Open Google Sheets", buttonStyle))
            {
                OpenGoogleSheets();
            }

            if (GUILayout.Button("❖ Open Editor", buttonStyle))
            {
                OnRunEditor();
            }

            if (GUILayout.Button("★ Leave Review", buttonStyle))
            {
                LeaveReview();
            }

            if (Constants.LocalizationEditorUrl == "" && GUILayout.Button("$ Buy Simple Localization PRO", buttonStyle))
            {
                Application.OpenURL(Constants.AssetUrlPro);
            }
        }

        public void DisplayWarnings()
        {
            if (TableId == "")
            {
                EditorGUILayout.HelpBox("Table Id is empty.", MessageType.Warning);
            }
            else if (SaveFolder == null)
            {
                EditorGUILayout.HelpBox("Save Folder is not set.", MessageType.Warning);
            }
            else if (Sheets.Count == 0)
            {
                EditorGUILayout.HelpBox("Sheets are empty.", MessageType.Warning);
            }
            else if (Sheets.Any(i => i.TextAsset == null))
            {
                EditorGUILayout.HelpBox("Sheets are not downloaded.", MessageType.Warning);
            }
            else if (TableId == Constants.ExampleTableId)
            {
                EditorGUILayout.HelpBox("Example Table Id is used.", MessageType.Warning);
            }
        }

        #endif
    }
}