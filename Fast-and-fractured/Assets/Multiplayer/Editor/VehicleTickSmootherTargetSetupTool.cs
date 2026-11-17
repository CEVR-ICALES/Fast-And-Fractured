using System.Collections.Generic;
using FishNet.Component.Transforming.Beta;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Points every NetworkTickSmoother of a networked car at a transform of that same car (Carme's targeted Pepe's
    /// prefab asset, which logged a parenting error on every spawn). NetworkedVehicleSetupTests asserts it.
    /// </summary>
    public static class VehicleTickSmootherTargetSetupTool
    {
        private const string TargetProperty = "_initializationSettings.TargetTransform";
        private static readonly string[] NetworkObjectProperties = { "_addedNetworkObject", "_networkObjectCache" };

        public static void Run()
        {
            int changed = 0, checkedCount = 0;
            foreach (string path in NetworkedCarFixes.FindCarPrefabPaths())
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
    }
}
