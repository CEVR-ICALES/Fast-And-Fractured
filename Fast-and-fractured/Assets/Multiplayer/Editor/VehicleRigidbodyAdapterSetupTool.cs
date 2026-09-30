using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Leaves every networked car with exactly one ICustomRigidbody: the MultiplayerRigidbodyAdapter.
    ///
    /// The networked cars are variants of the singleplayer cars, which carry a UnityRigidbodyAdapter.
    /// Pepe's, Josefino's and MariaAntonia's variants remove it; Carme's did not, and
    /// GetComponent&lt;ICustomRigidbody&gt;() returned the singleplayer adapter first. For Carme,
    /// NetworkedVehicle.CreateReconcile then never produced a reconcile (it only reconciles a
    /// MultiplayerRigidbodyAdapter), so her car was never corrected by the server: measured
    /// 2026-09-30, a server-side move of 520 m never reached her own screen, and every physics
    /// component drove her car through the non-predicted adapter.
    ///
    /// Run from Tools ▸ Fast And Fractured ▸ Vehicle Setup - Single Rigidbody Adapter (Headless).
    /// NetworkedVehicleSetupTests asserts the result.
    /// </summary>
    public static class VehicleRigidbodyAdapterSetupTool
    {
        private const string CarFolder = "Assets/Multiplayer";

        [MenuItem("Tools/Fast And Fractured/Vehicle Setup - Single Rigidbody Adapter (Headless)")]
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
