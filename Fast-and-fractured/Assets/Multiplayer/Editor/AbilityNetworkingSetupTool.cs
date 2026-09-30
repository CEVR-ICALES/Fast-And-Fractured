using System;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.EditorTools
{
    /// <summary>
    /// One-click setup for the ability networking that needs prefab surgery (Carme's McChicken
    /// and Pepe's tomatoes). Headless-safe: no modal dialogs, loggable from MCP.
    ///
    /// What it does:
    ///  1. NetworkedAscendingTomato Variant: adds the missing NetworkedAscendingTomato component.
    ///  2. Creates NetworkedDescendingTomato Variant (NetworkObject + NetworkTransform +
    ///     NetworkedDescendingTomato) from the SP DescendingTomato prefab.
    ///  3. Pepe car variant: adds NetworkedPepeUniqueAbility wired to both tomato prefabs and
    ///     clears the generic NetworkedUniqueAbility._networkedAbilityPrefab (the generic spawn
    ///     path would double-spawn an uninitialized tomato otherwise).
    ///  4. Creates NetworkedMcChickenAbility prefab (NetworkObject + NetworkedMcChickenBehaviour;
    ///     no NetworkTransform — the behaviour syncs position itself) from McChickenUniqueAbility.
    ///  5. Carme car variant: adds NetworkedCarmeUniqueAbility wired to the chicken prefab.
    /// </summary>
    public static class AbilityNetworkingSetupTool
    {
        private const string ASCENDING_VARIANT = "Assets/Multiplayer/Pool/NetworkedAscendingTomato Variant.prefab";
        private const string DESCENDING_SOURCE = "Assets/FastAndFractured/Characters/Pepe/Prefabs/DescendingTomato.prefab";
        private const string DESCENDING_VARIANT = "Assets/Multiplayer/Pool/NetworkedDescendingTomato Variant.prefab";
        private const string PEPE_CAR = "Assets/Multiplayer/Vehicles/Prefabs/NetworkedPepeGameplayCar Variant.prefab";
        private const string CHICKEN_SOURCE = "Assets/FastAndFractured/Characters/Carme/McChicken/Prefab/McChickenUniqueAbility.prefab";
        private const string CHICKEN_VARIANT = "Assets/Multiplayer/Props/NetworkedMcChickenAbility.prefab";
        private const string CARME_CAR = "Assets/Multiplayer/Vehicles/Prefabs/NetworkedCarmeGameplayCar Variant.prefab";

        private const string INTERACTABLES_SPAWNABLE = "Assets/Multiplayer/Lobby/Resources/NetworkedInteractablesSpawnable.prefab";
        private const string GOLBAT_VARIANT = "Assets/Multiplayer/Pool/NetworkedPushShootGolBat Variant.prefab";

        [MenuItem("Tools/Fast And Fractured/Ability Networking - Setup (Headless)")]
        public static void SetupAll()
        {
            try
            {
                // Runtime-spawned interactable-sync wrapper (scene-placing it breaks SceneId init).
                EnsureSpawnablePrefab(INTERACTABLES_SPAWNABLE, "NetworkedInteractablesSpawnable",
                    typeof(NetworkObject), typeof(NetworkedInteractableHandler));

                // Josefino's push projectile shipped without its NetworkedProjectile wrapper.
                EnsureComponentOnPrefab(GOLBAT_VARIANT, typeof(NetworkedProjectile));
                EnsureComponentOnPrefab(GOLBAT_VARIANT, typeof(NetworkedPredictedProjectileSpawn));
                EnsurePredictedSpawnOnAllProjectiles();

                EnsureComponentOnPrefab(ASCENDING_VARIANT, typeof(NetworkedAscendingTomato));

                EnsureVariantWithComponents(DESCENDING_SOURCE, DESCENDING_VARIANT,
                    typeof(NetworkObject), typeof(NetworkTransform), typeof(NetworkedDescendingTomato));

                EnsureVariantWithComponents(CHICKEN_SOURCE, CHICKEN_VARIANT,
                    typeof(NetworkObject), typeof(NetworkedMcChickenBehaviour));

                WirePepeCar();
                WireCarmeCar();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("<color=lime>[AbilityNetworkingSetupTool] Setup complete. Review the prefabs and let FishNet regenerate DefaultPrefabObjects.</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbilityNetworkingSetupTool] Setup failed: {e}");
            }
        }

        private static void EnsurePredictedSpawnOnAllProjectiles()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Multiplayer" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<NetworkedProjectile>() != null)
                    EnsureComponentOnPrefab(path, typeof(NetworkedPredictedProjectileSpawn));
            }
        }

        /// <summary>Creates a fresh (non-variant) prefab with the given components if missing.</summary>
        private static void EnsureSpawnablePrefab(string prefabPath, string rootName, params Type[] components)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var go = new GameObject(rootName);
                try
                {
                    foreach (var component in components)
                        go.AddComponent(component);
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    Debug.Log($"[AbilityNetworkingSetupTool] Created spawnable prefab {prefabPath}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return;
            }

            foreach (var component in components)
                EnsureComponentOnPrefab(prefabPath, component);
        }

        private static void EnsureComponentOnPrefab(string prefabPath, Type componentType)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent(componentType) == null)
                {
                    root.AddComponent(componentType);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[AbilityNetworkingSetupTool] Added {componentType.Name} to {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureVariantWithComponents(string sourcePath, string variantPath, params Type[] components)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null)
                {
                    Debug.LogError($"[AbilityNetworkingSetupTool] Source prefab missing: {sourcePath}");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
                Debug.Log($"[AbilityNetworkingSetupTool] Created variant {variantPath}");
            }

            foreach (var component in components)
                EnsureComponentOnPrefab(variantPath, component);
        }

        private static void WirePepeCar()
        {
            var ascending = AssetDatabase.LoadAssetAtPath<GameObject>(ASCENDING_VARIANT);
            var descending = AssetDatabase.LoadAssetAtPath<GameObject>(DESCENDING_VARIANT);

            EditPrefab(PEPE_CAR, root =>
            {
                var pepeAbility = root.GetComponent<NetworkedPepeUniqueAbility>();
                if (pepeAbility == null)
                    pepeAbility = root.AddComponent<NetworkedPepeUniqueAbility>();

                var so = new SerializedObject(pepeAbility);
                so.FindProperty("_networkedAscendingTomatoPrefab").objectReferenceValue = ascending;
                so.FindProperty("_networkedDescendingTomatoPrefab").objectReferenceValue = descending;
                so.ApplyModifiedPropertiesWithoutUndo();

                // The generic spawn path would double-spawn an uninitialized tomato.
                var generic = root.GetComponentInChildren<NetworkedUniqueAbility>(true);
                if (generic != null)
                {
                    var genericSo = new SerializedObject(generic);
                    genericSo.FindProperty("_networkedAbilityPrefab").objectReferenceValue = null;
                    genericSo.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }

        private static void WireCarmeCar()
        {
            var chicken = AssetDatabase.LoadAssetAtPath<GameObject>(CHICKEN_VARIANT);
            if (chicken == null)
            {
                Debug.LogError("[AbilityNetworkingSetupTool] Chicken variant missing, Carme not wired.");
                return;
            }

            EditPrefab(CARME_CAR, root =>
            {
                var carmeAbility = root.GetComponent<NetworkedCarmeUniqueAbility>();
                if (carmeAbility == null)
                    carmeAbility = root.AddComponent<NetworkedCarmeUniqueAbility>();

                var so = new SerializedObject(carmeAbility);
                so.FindProperty("_networkedChickenPrefab").objectReferenceValue = chicken;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void EditPrefab(string prefabPath, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[AbilityNetworkingSetupTool] Updated {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
