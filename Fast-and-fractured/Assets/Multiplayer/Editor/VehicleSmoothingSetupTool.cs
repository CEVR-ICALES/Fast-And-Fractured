using System.Collections.Generic;
using FishNet.Component.Transforming.Beta;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Makes the motion-smoothing setup identical across every networked car prefab.
    ///
    /// The four cars had drifted apart: Pepe and Josefino ship with Rigidbody interpolation None,
    /// while Carme and MariaAntonia ship with Interpolate. Whichever value is right, having two of
    /// each guarantees that some characters feel different from others and that any tuning done on
    /// one is invisible on the next — which is how "it trembles" stayed unreproducible for so long.
    ///
    /// The intended setup, applied here:
    ///   * Rigidbody.interpolation — one shared value (see INTERPOLATION below).
    ///   * NetworkTickSmoother on Visuals and Turret, so body and turret are smoothed together.
    ///   * NetworkObject._graphicalObject left EMPTY: populating it switches on FishNet's built-in
    ///     PredictionSmoother, which would be a second smoother over the same transforms.
    ///
    /// Run from Tools ▸ Fast And Fractured ▸ Vehicle Smoothing - Unify (Headless).
    /// NetworkedVehicleSetupTests asserts the same invariants so drift fails the tests.
    /// </summary>
    public static class VehicleSmoothingSetupTool
    {
        /// <summary>
        /// Shared Rigidbody interpolation for every networked car.
        ///
        /// None, decided by testing: setting Interpolate made the REMOTE view much worse, with
        /// abnormal shaking. That is consistent with how a non-owned car moves — its position is
        /// being corrected by the network (reconcile / replicated state), and Unity's rigidbody
        /// interpolation then blends between physics states that are themselves being overwritten,
        /// so the two corrections oscillate against each other.
        ///
        /// Between-tick smoothing is the NetworkTickSmoother's job on the visual children; the body
        /// itself must follow the simulation exactly. Do not switch this back to Interpolate without
        /// measuring the REMOTE peer with NetworkedVehicleDiagnostics — the owner's view alone will
        /// not show the regression.
        /// </summary>
        private const RigidbodyInterpolation INTERPOLATION = RigidbodyInterpolation.None;

        private const string CarFolder = "Assets/Multiplayer";

        [MenuItem("Tools/Fast And Fractured/Vehicle Smoothing - Unify (Headless)")]
        public static void Run()
        {
            List<string> paths = FindCarPrefabPaths();
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

        /// <summary>
        /// Adds VehicleVisualSmoother, which is what actually draws the car smoothly. The
        /// NetworkTickSmoothers above are kept because they mark which children are graphical, but
        /// NetworkedVehicle stops them at runtime - see ApplyVisualSmoothingForOwnership for why -
        /// so without this the model is drawn straight off the tick-stepped physics transform.
        /// </summary>
        private static bool EnsureVisualSmoother(GameObject root, string path)
        {
            if (root.GetComponent<VehicleVisualSmoother>() != null) return false;

            root.AddComponent<VehicleVisualSmoother>();
            Debug.Log($"[VehicleSmoothingSetupTool] {path}: added VehicleVisualSmoother.");
            return true;
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

            // Cast to Transform before the null check: an unassigned Unity object reference is a
            // "fake null" that compares non-null as a plain object, which made this report a change
            // on prefabs that were already clean.
            var current = field.GetValue(nob) as Transform;
            if (current == null) return false;

            Debug.Log($"[VehicleSmoothingSetupTool] {System.IO.Path.GetFileName(path)}: cleared _graphicalObject " +
                      "(it would enable a second, competing smoother).");
            field.SetValue(nob, null);
            return true;
        }
    }
}
