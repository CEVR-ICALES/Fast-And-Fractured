using System.Collections.Generic;
using FishNet.Component.Transforming.Beta;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Points every NetworkTickSmoother of a networked car at a transform of that same car.
    ///
    /// Carme's variant was built from Pepe's: both of its smoothers (Visuals, Turret) targeted the
    /// root of NetworkedPepeGameplayCar Variant, a transform inside ANOTHER prefab asset. FishNet
    /// starts the smoother in OnStartClient and parents a tracker to that target, so every peer
    /// that spawned Carme's car logged "Setting the parent of a transform which resides in a
    /// Prefab Asset is disabled to prevent data corruption" twice (measured 2026-09-30, host and
    /// client). The other cars target their own root; a foreign target is replaced by the
    /// transform at the same path in the car itself.
    ///
    /// The copied smoothers also still named Pepe's NetworkObject as theirs (_addedNetworkObject,
    /// _networkObjectCache). FishNet re-caches the latter when it registers the prefab, so that only
    /// misled the editor, but they are repointed to the car's own NetworkObject as well.
    ///
    /// Run from Tools ▸ Fast And Fractured ▸ Vehicle Setup - Tick Smoother Targets (Headless).
    /// NetworkedVehicleSetupTests asserts the result.
    /// </summary>
    public static class VehicleTickSmootherTargetSetupTool
    {
        private const string CarFolder = "Assets/Multiplayer";
        private const string TargetProperty = "_initializationSettings.TargetTransform";
        private static readonly string[] NetworkObjectProperties = { "_addedNetworkObject", "_networkObjectCache" };

        [MenuItem("Tools/Fast And Fractured/Vehicle Setup - Tick Smoother Targets (Headless)")]
        public static void Run()
        {
            int changed = 0, checkedCount = 0;
            foreach (string path in FindCarPrefabPaths())
            {
                checkedCount++;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;
                try
                {
                    int fixedCount = RetargetForeignSmoothers(root);
                    if (fixedCount == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                    Debug.Log($"[VehicleTickSmootherTargetSetupTool] Fixed {fixedCount} foreign reference(s) on the tick smoothers of {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[VehicleTickSmootherTargetSetupTool] Checked {checkedCount} networked cars, changed {changed}.");
        }

        /// <summary>Returns how many references were repointed.</summary>
        internal static int RetargetForeignSmoothers(GameObject root)
        {
            int fixedCount = 0;
            var ownNetworkObject = root.GetComponent<NetworkObject>();
            foreach (var smoother in root.GetComponentsInChildren<NetworkTickSmoother>(true))
            {
                var so = new SerializedObject(smoother);

                foreach (string field in NetworkObjectProperties)
                {
                    SerializedProperty property = so.FindProperty(field);
                    var named = property?.objectReferenceValue as NetworkObject;
                    if (named == null || ownNetworkObject == null || named.transform.IsChildOf(root.transform)) continue;
                    property.objectReferenceValue = ownNetworkObject;
                    fixedCount++;
                }

                SerializedProperty target = so.FindProperty(TargetProperty);
                var current = target?.objectReferenceValue as Transform;
                if (current != null && !current.IsChildOf(root.transform))
                {
                    string relativePath = AnimationUtility.CalculateTransformPath(current, current.root);
                    Transform own = string.IsNullOrEmpty(relativePath) ? root.transform : root.transform.Find(relativePath);
                    if (own != null)
                    {
                        target.objectReferenceValue = own;
                        fixedCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[VehicleTickSmootherTargetSetupTool] {root.name}: no '{relativePath}' to replace the foreign smoother target on {smoother.name}.");
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return fixedCount;
        }

        private static List<string> FindCarPrefabPaths()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CarFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<NetworkedVehicle>() != null)
                    paths.Add(path);
            }
            return paths;
        }
    }
}
