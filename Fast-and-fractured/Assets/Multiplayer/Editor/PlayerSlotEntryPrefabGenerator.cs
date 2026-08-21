using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>Generates the PlayerSlotEntry prefab (lobby player row). Called by LobbySetupWizard.</summary>
    public static class PlayerSlotEntryPrefabGenerator
    {
        private const string PREFAB_PATH = "Assets/Multiplayer/Lobby/Prefabs/PlayerSlotEntry.prefab";
        private const string PREFABS_DIR = "Assets/Multiplayer/Lobby/Prefabs";

        public static PlayerSlotUI Generate()
        {
            if (!System.IO.Directory.Exists(PREFABS_DIR))
            {
                System.IO.Directory.CreateDirectory(PREFABS_DIR);
                AssetDatabase.Refresh();
            }

            var root = new GameObject("PlayerSlotEntry");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(400f, 120f);

            // Background tint — slot color reflects ready state.
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            var slot = root.AddComponent<PlayerSlotUI>();

            // Character icon — left, vertically centered (icon is square; the wider slot now
            // makes room for a larger icon).
            var iconGO = new GameObject("CharIcon");
            iconGO.transform.SetParent(root.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.pivot     = new Vector2(0f, 0.5f);
            iconRT.offsetMin = new Vector2(8f,   8f);
            iconRT.offsetMax = new Vector2(112f, -8f);
            var icon = iconGO.AddComponent<Image>();
            icon.color           = Color.white;
            icon.preserveAspect  = true;

            // Player name (and character code) — top center band.
            var nameGO = CreateLabel(root.transform, "NameLabel", "Player ?",
                new Vector2(0.28f, 0.55f), new Vector2(0.78f, 0.95f),
                fontSize: 22f, align: TextAlignmentOptions.MidlineLeft);

            // READY badge — small pill below the name.
            var readyBadgeGO = new GameObject("ReadyBadge");
            readyBadgeGO.transform.SetParent(root.transform, false);
            var rbRT = readyBadgeGO.AddComponent<RectTransform>();
            rbRT.anchorMin = new Vector2(0.28f, 0.10f);
            rbRT.anchorMax = new Vector2(0.44f, 0.45f);
            rbRT.offsetMin = rbRT.offsetMax = Vector2.zero;
            var rbImg = readyBadgeGO.AddComponent<Image>();
            rbImg.color = new Color(0.20f, 0.60f, 0.20f, 1f);
            var readyLblGO = CreateLabel(readyBadgeGO.transform, "Label", "READY",
                Vector2.zero, Vector2.one, fontSize: 18f, align: TextAlignmentOptions.Center);

            // SELECT button container — sits between the name and the map vote.
            // Hidden on remote slots via PlayerSlotUI.SetData.
            var selectContainerGO = new GameObject("SelectButtonRoot");
            selectContainerGO.transform.SetParent(root.transform, false);
            var scRT = selectContainerGO.AddComponent<RectTransform>();
            scRT.anchorMin = new Vector2(0.45f, 0.10f);
            scRT.anchorMax = new Vector2(0.78f, 0.50f);
            scRT.offsetMin = scRT.offsetMax = Vector2.zero;
            var selectBtnGO = new GameObject("SelectButton");
            selectBtnGO.transform.SetParent(selectContainerGO.transform, false);
            var sbRT = selectBtnGO.AddComponent<RectTransform>();
            sbRT.anchorMin = Vector2.zero; sbRT.anchorMax = Vector2.one;
            sbRT.offsetMin = sbRT.offsetMax = Vector2.zero;
            var sbImg = selectBtnGO.AddComponent<Image>();
            sbImg.color = new Color(0.20f, 0.50f, 0.90f, 1f);
            var selectButton = selectBtnGO.AddComponent<Button>();
            CreateLabel(selectBtnGO.transform, "Label", "SELECT CHARACTER & MAP",
                Vector2.zero, Vector2.one, fontSize: 16f, align: TextAlignmentOptions.Center);

            // Map vote thumbnail container — right side.
            var voteContainerGO = new GameObject("MapVoteContainer");
            voteContainerGO.transform.SetParent(root.transform, false);
            var vcRT = voteContainerGO.AddComponent<RectTransform>();
            vcRT.anchorMin = new Vector2(0.80f, 0.10f);
            vcRT.anchorMax = new Vector2(0.98f, 0.90f);
            vcRT.offsetMin = vcRT.offsetMax = Vector2.zero;

            var voteImg = voteContainerGO.AddComponent<Image>();
            voteImg.color          = Color.white;
            voteImg.preserveAspect = true;

            // Wire all SerializedFields on PlayerSlotUI.
            var so = new SerializedObject(slot);
            so.FindProperty("_charIcon").objectReferenceValue          = icon;
            so.FindProperty("_nameLabel").objectReferenceValue         = nameGO.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_readyBadge").objectReferenceValue        = readyBadgeGO;
            so.FindProperty("_readyLabel").objectReferenceValue        = readyLblGO.GetComponent<TextMeshProUGUI>();
            so.FindProperty("_backgroundTint").objectReferenceValue    = bg;
            so.FindProperty("_mapVoteThumbnail").objectReferenceValue  = voteImg;
            so.FindProperty("_mapVoteContainer").objectReferenceValue  = voteContainerGO;
            so.FindProperty("_selectButton").objectReferenceValue      = selectButton;
            so.FindProperty("_selectButtonRoot").objectReferenceValue  = selectContainerGO;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out success);
            Object.DestroyImmediate(root);

            if (success)
            {
                Debug.Log($"[PlayerSlotEntryPrefabGenerator] Prefab saved at {PREFAB_PATH}");
                AssetDatabase.Refresh();
                return prefab.GetComponent<PlayerSlotUI>();
            }
            Debug.LogError("[PlayerSlotEntryPrefabGenerator] Failed to save prefab.");
            return null;
        }

        private static GameObject CreateLabel(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4f, 0f);
            rt.offsetMax = new Vector2(-4f, 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = align;
            tmp.color     = Color.white;
            return go;
        }
    }
}
