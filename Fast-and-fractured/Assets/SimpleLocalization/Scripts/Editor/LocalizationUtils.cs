using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.SimpleLocalization.Scripts.Editor
{
    public class LocalizationUtils
    {
        public static string GetInternalError(UnityWebRequest request)
        {
            var matches = Regex.Matches(request.downloadHandler.text, @">(?<Message>.+?)<\/div>");

            if (matches.Count == 0 && !request.downloadHandler.text.Contains("Google Script ERROR:")) return null;

            var error = matches.Count > 0 ? matches[1].Groups["Message"].Value.Replace("quot;", "") : request.downloadHandler.text;

            return error;
        }

        public static IEnumerator SubmitChanges(List<Dictionary<string, string>> rows, long sheetId, string tableId, string googleScriptUrl, Action callback = null)
        {
            if (string.IsNullOrEmpty(tableId))
            {
                EditorUtility.DisplayDialog("Error", "Table Id is empty!", "OK");

                yield break;
            }

            var json = JsonConvert.SerializeObject(rows);
            var form = new WWWForm();

            form.AddField("tableId", tableId);
            form.AddField("sheetId", sheetId.ToString());
            form.AddField("data", json);

            using var request = UnityWebRequest.Post(googleScriptUrl, form);

            if (EditorUtility.DisplayCancelableProgressBar("Submitting data...", "[0%]", 0))
            {
                yield break;
            }

            yield return request.SendWebRequest();

            EditorUtility.ClearProgressBar();

            var error = request.error ?? LocalizationUtils.GetInternalError(request);

            if (string.IsNullOrEmpty(error))
            {
                callback?.Invoke();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Can't save data to table: {error}", "OK");
            }
        }
        
        public static Dictionary<string, string> CreateRow(string key, ActionType action, Dictionary<string, string> keys, Dictionary<string, SortedDictionary<string, string>> sheetDictionary) // OnDestroy required
        {
            var dict = new Dictionary<string, string> { { "Key", action == ActionType.Add ? "" : key }, { "NewKey", action == ActionType.Delete ? "" : keys[key] } };

            foreach (var language in sheetDictionary.Keys)
            {
                dict.Add(language, action == ActionType.Delete ? "" : sheetDictionary[language][key]);
            }

            return dict;
        }
    }
}
