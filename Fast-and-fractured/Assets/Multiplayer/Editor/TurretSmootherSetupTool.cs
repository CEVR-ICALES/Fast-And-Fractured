using FishNet.Component.Transforming.Beta;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Adds a NetworkTickSmoother to the Turret root of every networked gameplay car prefab.
    ///
    /// The car prefabs ship with a NetworkTickSmoother on 'Visuals' only. 'Turret' is a SIBLING of
    /// 'Visuals' (both direct children of the car root), so on non-owned cars the body interpolates
    /// smoothly between network ticks while the turret follows the raw rigidbody in tick-sized
    /// steps — the "trembling turret" effect. The turret needs the exact same smoothing component,
    /// configured identically to the one on 'Visuals'.
    /// </summary>
    public static class TurretSmootherSetupTool
    {
        private static readonly string[] CarPrefabPaths =
        {
            "Assets/Multiplayer/NetworkedPepeGameplayCar Variant.prefab",
            "Assets/Multiplayer/NetworkedJosefinoGameplayCar Variant.prefab",
            "Assets/Multiplayer/NetworkedCarmeGameplayCar Variant.prefab",
            "Assets/Multiplayer/NetworkedMariaAntoniaGameplayCar Variant.prefab",
        };

        [MenuItem("Tools/Fast And Fractured/Turret Smoother - Setup (Headless)")]
        public static void Run()
        {
            int changed = 0;
            foreach (string path in CarPrefabPaths)
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
                        // Carme's variant shipped without ANY smoother — her whole body jitters on
                        // remote clients, not just the turret. Borrow the settings from another car
                        // (they are identical across variants) and fix Visuals too.
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

                    // Copy every serialized setting from the Visuals smoother so both halves of the
                    // car interpolate identically.
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
            Debug.Log($"[TurretSmootherSetupTool] Done. {changed}/{CarPrefabPaths.Length} prefabs updated.");
        }

        /// <summary>
        /// Returns the Visuals NetworkTickSmoother from the first car prefab (other than
        /// <paramref name="excludePath"/>) that has one, to use as a settings template.
        /// The returned component belongs to a loaded asset — read-only use (CopySerialized source).
        /// </summary>
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
