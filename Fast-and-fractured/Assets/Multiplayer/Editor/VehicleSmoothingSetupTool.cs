using System.Collections.Generic;
using FishNet.Component.Transforming.Beta;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Makes the motion-smoothing setup identical on every networked car prefab (the cars had drifted apart).
    /// NetworkedVehicleSetupTests asserts the same invariants.
    /// </summary>
    public static class VehicleSmoothingSetupTool
    {
        /// <summary>
        /// None: Interpolate made remote cars shake, blending physics states the network keeps overwriting.
        /// Measure the remote peer with NetworkedVehicleDiagnostics before changing it.
        /// </summary>
        private const RigidbodyInterpolation INTERPOLATION = RigidbodyInterpolation.None;


        public static void Run()
        {
            List<string> paths = NetworkedCarFixes.FindCarPrefabPaths();
            if (paths.Count == 0)
            {
                Debug.LogError("[VehicleSmoothingSetupTool] No networked car prefabs found.");
                return;
            }

            int changed = 0;
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    bool dirty = false;
                    dirty |= ApplyInterpolation(root, path);
                    dirty |= EnsureSmoother(root, "Visuals", path);
                    if (root.transform.Find("Turret") != null)
                        dirty |= EnsureSmoother(root, "Turret", path);
                    dirty |= ClearGraphicalObject(root, path);
                    dirty |= EnsureCosmeticSiblingsSmoothed(root, path);
                    dirty |= EnsureVisualSmoother(root, path);

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[VehicleSmoothingSetupTool] Done. {changed}/{paths.Count} prefab(s) updated " +
                      $"(interpolation={INTERPOLATION}).");
        }

        /// <summary>Adds VehicleVisualSmoother; NetworkedVehicle stops the NetworkTickSmoothers at runtime.</summary>
        private static bool EnsureVisualSmoother(GameObject root, string path)
        {
            if (root.GetComponent<VehicleVisualSmoother>() != null) return false;

            root.AddComponent<VehicleVisualSmoother>();
            Debug.Log($"[VehicleSmoothingSetupTool] {path}: added VehicleVisualSmoother.");
            return true;
        }

        /// <summary>
        /// Tags cosmetic children of the car root (a Renderer, no smoother yet) like Visuals and Turret. Functional
        /// anchors have no Renderer and stay put: smoothing one would move where gameplay reads it.
        /// </summary>
        private static bool EnsureCosmeticSiblingsSmoothed(GameObject root, string path)
        {
            NetworkTickSmoother template = root.GetComponentInChildren<NetworkTickSmoother>(true);
            bool dirty = false;

            foreach (Transform child in root.transform)
            {
                if (child.GetComponent<NetworkTickSmoother>() != null) continue;
                if (child.GetComponentInChildren<Renderer>(true) == null) continue;

                NetworkTickSmoother added = child.gameObject.AddComponent<NetworkTickSmoother>();
                if (template != null && template != added)
                    EditorUtility.CopySerialized(template, added);

                Debug.Log($"[VehicleSmoothingSetupTool] {System.IO.Path.GetFileName(path)}: " +
                          $"added NetworkTickSmoother to cosmetic sibling '{child.name}'.");
                dirty = true;
            }

            return dirty;
        }

        private static bool ApplyInterpolation(GameObject root, string path)
        {
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogWarning($"[VehicleSmoothingSetupTool] {path}: no Rigidbody on the vehicle root.");
                return false;
            }
            if (rb.interpolation == INTERPOLATION) return false;

            Debug.Log($"[VehicleSmoothingSetupTool] {System.IO.Path.GetFileName(path)}: interpolation " +
                      $"{rb.interpolation} -> {INTERPOLATION}");
            rb.interpolation = INTERPOLATION;
            return true;
        }

        private static bool EnsureSmoother(GameObject root, string childName, string path)
        {
            Transform child = root.transform.Find(childName);
            if (child == null) return false;
            if (child.GetComponent<NetworkTickSmoother>() != null) return false;

            // Copy settings from a sibling smoother when one exists so all cars share tuning.
            NetworkTickSmoother template = root.GetComponentInChildren<NetworkTickSmoother>(true);
            NetworkTickSmoother added = child.gameObject.AddComponent<NetworkTickSmoother>();
            if (template != null && template != added)
                EditorUtility.CopySerialized(template, added);

            Debug.Log($"[VehicleSmoothingSetupTool] {System.IO.Path.GetFileName(path)}: added NetworkTickSmoother to '{childName}'.");
            return true;
        }

        private static bool ClearGraphicalObject(GameObject root, string path)
        {
            var nob = root.GetComponent<NetworkObject>();
            if (nob == null) return false;

            var field = typeof(NetworkObject).GetField("_graphicalObject",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return false;

            // Cast first: an unassigned Unity reference is a fake null that compares non-null as object.
            var current = field.GetValue(nob) as Transform;
            if (current == null) return false;

            Debug.Log($"[VehicleSmoothingSetupTool] {System.IO.Path.GetFileName(path)}: cleared _graphicalObject " +
                      "(it would enable a second, competing smoother).");
            field.SetValue(nob, null);
            return true;
        }
    }
}
