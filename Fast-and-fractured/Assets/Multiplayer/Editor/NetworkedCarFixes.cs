using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// The idempotent fixes every networked car needs on top of NetworkedVehicleSetupTool's components, run whenever
    /// that tool creates or updates a car.
    /// </summary>
    public static class NetworkedCarFixes
    {
        private const string CarFolder = "Assets/Multiplayer";

        public static void ApplyToAllCars()
        {
            VehicleRigidbodyAdapterSetupTool.Run();
            VehicleTickSmootherTargetSetupTool.Run();
            TurretSmootherSetupTool.Run();
            VehicleSmoothingSetupTool.Run();
            AssetDatabase.SaveAssets();
            Debug.Log("[NetworkedCarFixes] Car fixes applied to every networked car.");
        }

        /// <summary>Every networked car prefab (a NetworkedVehicle at its root) under Assets/Multiplayer.</summary>
        public static List<string> FindCarPrefabPaths()
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
