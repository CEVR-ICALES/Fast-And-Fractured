using FishNet.Component.Transforming.Beta;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>Adds a NetworkTickSmoother, configured like the Visuals one, to the Turret of every networked car.</summary>
    public static class TurretSmootherSetupTool
    {
        private static System.Collections.Generic.List<string> CarPrefabPaths => NetworkedCarFixes.FindCarPrefabPaths();

        public static void Run()
        {
            int changed = 0;
            var carPrefabPaths = CarPrefabPaths;
            foreach (string path in carPrefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogError($"[TurretSmootherSetupTool] Could not load prefab at '{path}'.");
                    continue;
                }

                try
                {
                    Transform visuals = root.transform.Find("Visuals");
                    Transform turret = root.transform.Find("Turret");
                    if (visuals == null || turret == null)
                    {
                        Debug.LogWarning($"[TurretSmootherSetupTool] '{path}' missing Visuals or Turret child. Skipped.");
                        continue;
                    }

                    NetworkTickSmoother template = visuals.GetComponent<NetworkTickSmoother>();
                    if (template == null)
                    {
                        // A car without any smoother: borrow another car's settings and fix Visuals too.
                        template = LoadTemplateFromAnyOtherCar(path);
                        if (template == null)
                        {
                            Debug.LogWarning($"[TurretSmootherSetupTool] '{path}' has no smoother on Visuals and no other car provides a template. Skipped.");
                            continue;
                        }

                        NetworkTickSmoother visualsSmoother = visuals.gameObject.AddComponent<NetworkTickSmoother>();
                        EditorUtility.CopySerialized(template, visualsSmoother);
                        Debug.Log($"[TurretSmootherSetupTool] '{path}' was missing the Visuals smoother — added it from template.");
                    }

                    NetworkTickSmoother existing = turret.GetComponent<NetworkTickSmoother>();
                    if (existing == null)
                        existing = turret.gameObject.AddComponent<NetworkTickSmoother>();

                    EditorUtility.CopySerialized(template, existing);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                    Debug.Log($"[TurretSmootherSetupTool] Turret smoother ensured on '{path}'.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TurretSmootherSetupTool] Done. {changed}/{carPrefabPaths.Count} prefabs updated.");
        }

        /// <summary>The Visuals smoother of another car prefab, as a read-only settings template.</summary>
        private static NetworkTickSmoother LoadTemplateFromAnyOtherCar(string excludePath)
        {
            foreach (string otherPath in CarPrefabPaths)
            {
                if (otherPath == excludePath) continue;
                GameObject other = AssetDatabase.LoadAssetAtPath<GameObject>(otherPath);
                if (other == null) continue;
                Transform otherVisuals = other.transform.Find("Visuals");
                if (otherVisuals == null) continue;
                NetworkTickSmoother smoother = otherVisuals.GetComponent<NetworkTickSmoother>();
                if (smoother != null) return smoother;
            }
            return null;
        }
    }
}
