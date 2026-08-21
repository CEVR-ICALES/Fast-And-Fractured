using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.EditorTools
{
    /// <summary>Restricted setup: only mutates projectile prefabs already inside Assets/Multiplayer.</summary>
    public static class PredictedSpawnSetupTool
    {
        [MenuItem("Tools/Fast And Fractured/Multiplayer/Enable Predicted Projectile Spawns")]
        public static void Setup()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Multiplayer" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<NetworkedProjectile>() == null ||
                    prefab.GetComponent<NetworkedPredictedProjectileSpawn>() != null)
                    continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.AddComponent<NetworkedPredictedProjectileSpawn>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PredictedSpawnSetupTool] Added FishNet predicted spawning to {changed} Multiplayer projectile prefabs.");
        }

        [MenuItem("Tools/Fast And Fractured/Multiplayer/Enable Predicted Ultimate Spawns")]
        public static void SetupUltimates()
        {
            ConfigureAbilityPrefab("Assets/Multiplayer/Pool/NetworkedAscendingTomato Variant.prefab",
                PredictedAbilityObjectKind.PepeAscendingTomato);
            ConfigureAbilityPrefab("Assets/Multiplayer/Props/NetworkedMcChickenAbility.prefab",
                PredictedAbilityObjectKind.CarmeChicken);
            AssetDatabase.SaveAssets();
            Debug.Log("[PredictedSpawnSetupTool] Enabled predicted spawning on Pepe and Carme Multiplayer ultimate prefabs.");
        }

        private static void ConfigureAbilityPrefab(string path, PredictedAbilityObjectKind kind)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component = root.GetComponent<NetworkedPredictedAbilitySpawn>();
                if (component == null) component = root.AddComponent<NetworkedPredictedAbilitySpawn>();
                var serialized = new SerializedObject(component);
                serialized.FindProperty("_kind").enumValueIndex = (int)kind;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
