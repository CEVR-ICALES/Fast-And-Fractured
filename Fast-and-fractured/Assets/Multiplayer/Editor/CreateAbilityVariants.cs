using UnityEngine;
using UnityEditor;
using FishNet.Object;
using FastAndFractured.Multiplayer;
using FastAndFractured.Utilities;
using FishNet.Component.Transforming.Beta;
using System.Reflection;

namespace FastAndFractured.Multiplayer.Editor
{
    public class CreateAbilityVariants : MonoBehaviour
    {
        [MenuItem("Tools/Fast And Fractured/Setup/Create Ability Variants", priority = 23)]
        public static void CreateVariants()
        {
            CreateVariant("Assets/FastAndFractured/Characters/Pepe/Prefabs/AscendingTomato.prefab", "NetworkedAscendingTomato Variant", false);
            CreateVariant("Assets/FastAndFractured/MegaChicken/Prefabs/MegaChicken.prefab", "NetworkedMegaChicken Variant", false);
            // DescendingTomato doesn't need a networked variant if AscendingTomato logic runs on all clients (replicated via NetworkObject presence of spawner)
        }

        private static void CreateVariant(string sourcePath, string variantName, bool useProjectileLogic)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"Source prefab not found at {sourcePath}");
                return;
            }

            string outputFolder = "Assets/Multiplayer/Pool";
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Multiplayer", "Pool");
            }

            string outputPath = $"{outputFolder}/{variantName}.prefab";
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);

            try
            {
                // 1. Remove LocalEventRunner (if any)
                var localEventRunner = instance.GetComponentInChildren<LocalEventRunner>();
                if (localEventRunner != null) DestroyImmediate(localEventRunner);

                // 2. Add NetworkObject
                var networkObject = instance.GetComponent<NetworkObject>();
                if (networkObject == null) networkObject = instance.AddComponent<NetworkObject>();

                // Configure prediction
                var noProp = typeof(NetworkObject);
                var enablePredField = noProp.GetField("_enablePrediction", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enablePredField != null)
                    enablePredField.SetValue(networkObject, true);

                if (useProjectileLogic)
                {
                    // Bullet/Projectile logic (using NetworkedProjectile)
                    if (instance.GetComponent<MultiplayerRigidbodyAdapter>() == null)
                        instance.AddComponent<MultiplayerRigidbodyAdapter>();

                    if (instance.GetComponent<NetworkedProjectile>() == null)
                        instance.AddComponent<NetworkedProjectile>();

                    var tickSmoother = instance.GetComponent<NetworkTickSmoother>();
                    if (tickSmoother == null)
                        tickSmoother = instance.AddComponent<NetworkTickSmoother>();
                    EnsureTickSmootherTargetTransform(tickSmoother, instance.transform);
                }
                else
                {
                    // Ability/Character logic (using NetworkTransform)
                    // Remove Rigidbodies if they interfere? No, NetworkTransform handles them if configured.
                    
                    var netTransform = instance.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
                    if (netTransform == null)
                    {
                        netTransform = instance.AddComponent<FishNet.Component.Transforming.NetworkTransform>();
                        Debug.Log("[CreateAbilityVariants] Added NetworkTransform");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                Debug.Log($"Created networked variant: {outputPath}");
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        private static void EnsureTickSmootherTargetTransform(NetworkTickSmoother tickSmoother, Transform targetTransform)
        {
            if (tickSmoother == null || targetTransform == null) return;

            var field = typeof(NetworkTickSmoother).GetField("_initializationSettings",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return;

            object boxed = field.GetValue(tickSmoother);
            if (!(boxed is InitializationSettings))
                return;

            InitializationSettings settings = (InitializationSettings)boxed;
            settings.TargetTransform = targetTransform;
            field.SetValue(tickSmoother, settings);
        }
    }
}
