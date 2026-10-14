using System.Collections.Generic;
using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Gives a LocalizedText to the texts the editor tools write into a scene (lobby screens by
    /// the Lobby Setup Wizard, play-mode menu by MainMenuPlayModesTool): every text without one
    /// whose content is the English text of a Multiplayer-sheet key ("MP.") gets that key.
    /// Re-run it after those tools rebuild their screens.
    /// </summary>
    public static class LocalizeSceneTextsTool
    {
        private const string KEY_PREFIX = "MP.";

        [MenuItem("Tools/Fast And Fractured/Localization/Localize Multiplayer Scene Texts")]
        public static void RunOnActiveScene()
        {
            int added = Localize(SceneManager.GetActiveScene());
            Debug.Log($"[LocalizeSceneTextsTool] {added} text(s) localized. Save the scene to keep them.");
        }

        public static int Localize(Scene scene)
        {
            var keyByEnglish = KeysByEnglishText();
            int added = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text.GetComponent<LocalizedText>() != null) continue;
                    if (!keyByEnglish.TryGetValue(text.text.Trim(), out string key)) continue;
                    var localized = Undo.AddComponent<LocalizedText>(text.gameObject);
                    localized.LocalizationKey = key;
                    added++;
                }
            if (added > 0) EditorSceneManager.MarkSceneDirty(scene);
            return added;
        }

        /// <summary>English text → key, for the multiplayer keys (first key wins on duplicates).</summary>
        public static Dictionary<string, string> KeysByEnglishText()
        {
            LocalizationManager.Dictionary.Clear();
            LocalizationManager.Read();
            var result = new Dictionary<string, string>();
            if (!LocalizationManager.Dictionary.TryGetValue("English", out var english)) return result;
            foreach (var pair in english)
                if (pair.Key.StartsWith(KEY_PREFIX) && !string.IsNullOrWhiteSpace(pair.Value) && !result.ContainsKey(pair.Value.Trim()))
                    result[pair.Value.Trim()] = pair.Key;
            return result;
        }
    }
}
