using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Leaves each networked car with only the MultiplayerRigidbodyAdapter: a leftover singleplayer adapter is
    /// found first and the car is never reconciled. NetworkedVehicleSetupTests asserts it.
    /// </summary>
    public static class VehicleRigidbodyAdapterSetupTool
    {

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
                    if (!RemoveSingleplayerAdapters(root)) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                    Debug.Log($"[VehicleRigidbodyAdapterSetupTool] Removed the singleplayer UnityRigidbodyAdapter from {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[VehicleRigidbodyAdapterSetupTool] Checked {checkedCount} networked cars, changed {changed}.");
        }

        /// <summary>Returns true when something was removed.</summary>
        internal static bool RemoveSingleplayerAdapters(GameObject root)
        {
            if (root.GetComponent<MultiplayerRigidbodyAdapter>() == null) return false;
            bool removed = false;
            foreach (var adapter in root.GetComponents<UnityRigidbodyAdapter>())
            {
                Object.DestroyImmediate(adapter, true);
                removed = true;
            }
            return removed;
        }
    }
}
