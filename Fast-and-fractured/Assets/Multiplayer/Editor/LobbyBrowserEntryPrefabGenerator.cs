using FastAndFractured.Multiplayer.Lobby;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>Generates the LobbyBrowserEntry prefab. Called by LobbySetupWizard.</summary>
    public static class LobbyBrowserEntryPrefabGenerator
    {
        private const string PREFAB_PATH = "Assets/Multiplayer/Lobby/Prefabs/LobbyBrowserEntry.prefab";
        private const string PREFABS_DIR = "Assets/Multiplayer/Lobby/Prefabs";
        private static readonly Vector2 ENTRY_SIZE = new Vector2(400f, 60f);
        private static readonly Vector2 LABEL_PADDING = new Vector2(4f, 0f);
        private static readonly Color BACKGROUND_COLOR = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        private static readonly Color JOIN_BUTTON_COLOR = new Color(0.2f, 0.6f, 0.2f, 1f);

        public static LobbyBrowserEntry Generate()
        {
            if (!System.IO.Directory.Exists(PREFABS_DIR))
            {
                System.IO.Directory.CreateDirectory(PREFABS_DIR);
                AssetDatabase.Refresh();
            }

            var root = new GameObject("LobbyBrowserEntry");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = ENTRY_SIZE;

            var bg = root.AddComponent<Image>();
            bg.color = BACKGROUND_COLOR;

            var entry = root.AddComponent<LobbyBrowserEntry>();

            var roomLabelGO = CreateLabel(root.transform, "RoomNameText", "Room Name",
                new Vector2(0f, 0f), new Vector2(0.55f, 1f), 18f, TextAlignmentOptions.MidlineLeft);

            var playersLabelGO = CreateLabel(root.transform, "PlayersText", "0/8",
                new Vector2(0.55f, 0f), new Vector2(0.75f, 1f), 16f, TextAlignmentOptions.Center);

            var pingLabelGO = CreateLabel(root.transform, "PingText", "--",
                new Vector2(0.75f, 0f), new Vector2(0.85f, 1f), 14f, TextAlignmentOptions.Center);

            var joinGO = new GameObject("JoinButton");
            joinGO.transform.SetParent(root.transform, false);
            var joinRT = joinGO.AddComponent<RectTransform>();
            joinRT.anchorMin = new Vector2(0.86f, 0.1f);
            joinRT.anchorMax = new Vector2(0.99f, 0.9f);
            joinRT.offsetMin = joinRT.offsetMax = Vector2.zero;
            joinGO.AddComponent<Image>().color = JOIN_BUTTON_COLOR;
            joinGO.AddComponent<Button>();
            CreateLabel(joinGO.transform, "Label", "Join",
                Vector2.zero, Vector2.one, 16f, TextAlignmentOptions.Center);

            var so = new SerializedObject(entry);
            so.FindProperty("_roomNameText").objectReferenceValue = roomLabelGO.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_playersText").objectReferenceValue  = playersLabelGO.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_pingText").objectReferenceValue     = pingLabelGO.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_joinButton").objectReferenceValue   = joinGO.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            bool success;
            var prefabGO = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out success);
            Object.DestroyImmediate(root);

            if (success)
            {
                Debug.Log($"[LobbyBrowserEntryPrefabGenerator] Prefab saved at {PREFAB_PATH}");
                AssetDatabase.Refresh();
                return prefabGO.GetComponent<LobbyBrowserEntry>();
            }
            else
            {
                Debug.LogError("[LobbyBrowserEntryPrefabGenerator] Failed to save prefab.");
                return null;
            }
        }

        private static GameObject CreateLabel(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = LABEL_PADDING;
            rt.offsetMax = -LABEL_PADDING;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = alignment;
            tmp.color     = Color.white;
            return go;
        }
    }
}
