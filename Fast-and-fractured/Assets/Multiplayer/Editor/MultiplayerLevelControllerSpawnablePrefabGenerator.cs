using FastAndFractured.Multiplayer;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>Generates the MultiplayerLevelControllerSpawnable prefab dynamically spawned by MultiplayerLevelControllerAutoInitGate.</summary>
    public static class MultiplayerLevelControllerSpawnablePrefabGenerator
    {
        public const string PREFAB_PATH   = "Assets/Multiplayer/Lobby/Resources/MultiplayerLevelControllerSpawnable.prefab";
        public const string RESOURCES_DIR = "Assets/Multiplayer/Lobby/Resources";

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
                EnsureComponents(existing);
                return existing;
            }

            var root = new GameObject("MultiplayerLevelControllerSpawnable");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkedLevelControllerWrapper>();
                root.AddComponent<NetworkedLevelControllerManager>();

                bool success;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out success);
                if (success)
                {
                    AssetDatabase.Refresh();
                    return prefab;
                }
                Debug.LogError("[MultiplayerLevelControllerSpawnablePrefabGenerator] SaveAsPrefabAsset failed.");
                return null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureComponents(GameObject prefab)
        {
            bool changed = false;

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                prefab.AddComponent<NetworkObject>();
                changed = true;
            }
            if (prefab.GetComponent<NetworkedLevelControllerWrapper>() == null)
            {
                prefab.AddComponent<NetworkedLevelControllerWrapper>();
                changed = true;
            }
            if (prefab.GetComponent<NetworkedLevelControllerManager>() == null)
            {
                prefab.AddComponent<NetworkedLevelControllerManager>();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
