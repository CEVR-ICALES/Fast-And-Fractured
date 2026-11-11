using FastAndFractured.Multiplayer;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>Generates the LobbyManagerSpawnable prefab (IsGlobal=true) used by LobbyServerBootstrapper.</summary>
    public static class LobbyManagerSpawnablePrefabGenerator
    {
        public const string PREFAB_PATH    = "Assets/Multiplayer/Lobby/Resources/LobbyManagerSpawnable.prefab";
        public const string RESOURCES_DIR  = "Assets/Multiplayer/Lobby/Resources";

        public static GameObject Generate()
        {
            if (!System.IO.Directory.Exists(RESOURCES_DIR))
            {
                System.IO.Directory.CreateDirectory(RESOURCES_DIR);
                AssetDatabase.Refresh();
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existing != null)
            {
                PatchIsGlobalIfMissing(existing);
                return existing;
            }

            var root = new GameObject("LobbyManagerSpawnable");
            try
            {
                var nob = root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkedLobbyManager>();

                SetIsGlobal(nob, true);

                bool success;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out success);
                if (success)
                {
                    AssetDatabase.Refresh();
                    return prefab;
                }
                Debug.LogError("[LobbyManagerSpawnablePrefabGenerator] SaveAsPrefabAsset failed.");
                return null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Required so the lobby manager survives the menu→map scene transition (its
        // PlayerCharacterSelections / ResolvedMapId need to outlive the menu scene).
        private static void PatchIsGlobalIfMissing(GameObject prefab)
        {
            var nob = prefab.GetComponent<NetworkObject>();
            if (nob == null) return;
            var so = new SerializedObject(nob);
            var p  = so.FindProperty("_isGlobal");
            if (p != null && !p.boolValue)
            {
                p.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(nob);
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
            }
        }

        private static void SetIsGlobal(NetworkObject nob, bool value)
        {
            var so = new SerializedObject(nob);
            var p  = so.FindProperty("_isGlobal");
            if (p != null) p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(nob);
        }
    }
}
