using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Creates the networked prefab variants (NetworkObject + server wrapper) of singleplayer hazard prefabs in
    /// Assets/Multiplayer/Props. Run FishNet's Refresh Default Prefabs afterwards.
    /// </summary>
    public class NetworkedPropSetupTool : EditorWindow
    {
        private const string OUTPUT_FOLDER     = "Assets/Multiplayer/Props";
        private const string OUTPUT_FOLDER_PARENT = "Assets/Multiplayer";
        private const string OUTPUT_SUBFOLDER     = "Props";
        private const float SMALL_SPACING = 2f;
        private const float SPACING = 4f;
        private const float SECTION_SPACING = 6f;
        private const float PRIMARY_BUTTON_HEIGHT = 30f;
        private const float ROW_BUTTON_WIDTH = 72f;
        private const float ROW_BUTTON_HEIGHT = 40f;
        private static readonly Vector2 MIN_WINDOW_SIZE = new Vector2(620f, 480f);
        private static readonly Color ROW_DONE_COLOR = new Color(0.2f, 0.4f, 0.2f, 0.3f);
        private static readonly Color ROW_MISSING_COLOR = new Color(0.35f, 0.15f, 0.15f, 0.3f);

        private sealed class HazardDef
        {
            public string DisplayName;
            public Type   SingleplayerType;
            public Type   WrapperType;
            public string SceneOnlyNote;     // non-null if this may not exist as a prefab
            // A prefab variant gets its own networked variant: a pickup variant (RecoverHealth) is a different pickup.
            public bool   VariantsGetOwnPrefab;
        }

        private static readonly HazardDef[] HAZARD_DEFS =
        {
            new HazardDef
            {
                DisplayName      = "Dead Walls  (map boundary kill zone)",
                SingleplayerType = typeof(DeadWallsBehaivour),
                WrapperType      = typeof(NetworkedDeadWallsBehaviour),
                SceneOnlyNote    = "Boundary walls are often placed directly in scenes; " +
                                   "if none appear here they are scene-only — handled by the scene migration tool.",
            },
            new HazardDef
            {
                DisplayName      = "Breakable Platform",
                SingleplayerType = typeof(BreakablePlatform),
                WrapperType      = typeof(NetworkedBreakablePlatform),
            },
            new HazardDef
            {
                DisplayName      = "Point Explosion  (TNT crate)",
                SingleplayerType = typeof(PointExplosion),
                WrapperType      = typeof(NetworkedPointExplosion),
            },
            new HazardDef
            {
                DisplayName      = "Mega Chicken Boss",
                SingleplayerType = typeof(ChickenBrain),
                WrapperType      = typeof(NetworkedMegaChickenBoss),
                SceneOnlyNote    = "The boss may be placed directly in the scene as a unique entity.",
            },
            new HazardDef
            {
                DisplayName      = "Trampoline  (ParabolicImpulse)",
                SingleplayerType = typeof(ParabolicImpulse),
                WrapperType      = typeof(NetworkedTrampoline),
            },
            new HazardDef
            {
                DisplayName      = "Sandstorm  (SandstormController)",
                SingleplayerType = typeof(SandstormController),
                WrapperType      = typeof(NetworkedSandstormController),
            },
            new HazardDef
            {
                DisplayName      = "Stats Boost Pickup",
                VariantsGetOwnPrefab = true,
                SingleplayerType = typeof(StatsBoostInteractable),
                WrapperType      = typeof(NetworkedStatsBoostInteractable),
            },
            new HazardDef
            {
                DisplayName      = "Skin Unlock Pickup",
                VariantsGetOwnPrefab = true,
                SingleplayerType = typeof(SkinUnlockerInteractable),
                WrapperType      = typeof(NetworkedSkinUnlockerInteractable),
            },
            new HazardDef
            {
                DisplayName      = "Sucking Building",
                SingleplayerType = typeof(SuckingBuilding),
                WrapperType      = typeof(NetworkedSuckingBuilding),
            },
            new HazardDef
            {
                DisplayName      = "Building Dead Zone",
                SingleplayerType = typeof(BuildingDeadZone),
                WrapperType      = typeof(NetworkedBuildingDeadZone),
            },
            new HazardDef
            {
                DisplayName      = "Golf Ball",
                SingleplayerType = typeof(GolfBallBehaviour),
                WrapperType      = typeof(NetworkedGolfBall),
            },
        };

        private sealed class PrefabEntry
        {
            public HazardDef   Def;
            public GameObject  OriginalPrefab;
            public string      OriginalPath;
            public string      OutputPath;
            public bool        VariantExists;
            public GameObject  ExistingVariant;
        }

        private readonly Dictionary<HazardDef, List<PrefabEntry>> _byDef
            = new Dictionary<HazardDef, List<PrefabEntry>>();

        private Vector2 _scroll;
        private bool    _needsFishNetRefresh;

        [MenuItem("Tools/Fast And Fractured/Networked Prop Setup", priority = 11)]
        public static void ShowWindow()
        {
            var w = GetWindow<NetworkedPropSetupTool>("Networked Props");
            w.minSize = MIN_WINDOW_SIZE;
            w.Refresh();
            w.Show();
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            _byDef.Clear();
            _needsFishNetRefresh = false;
            ScanPrefabs(_byDef);
            Repaint();
        }

        /// <summary>Fills the map with one entry per (hazard, source prefab); shared with the headless menu item.</summary>
        private static void ScanPrefabs(Dictionary<HazardDef, List<PrefabEntry>> byDef)
        {
            EnsureOutputFolder();

            var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

            foreach (var def in HAZARD_DEFS)
                byDef[def] = new List<PrefabEntry>();

            foreach (var guid in allPrefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Never re-process our own output folder.
                if (path.StartsWith(OUTPUT_FOLDER + "/", StringComparison.OrdinalIgnoreCase)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var def in HAZARD_DEFS)
                {
                    // Scan the whole tree (the trampoline logic is on a child) but skip containers that only hold
                    // the hazard through nested prefab instances.
                    var spComponent = FindOwnedComponentInPrefab(prefab, def.SingleplayerType);
                    if (spComponent == null && def.VariantsGetOwnPrefab)
                        spComponent = FindVariantRootComponent(prefab, def.SingleplayerType);
                    if (spComponent == null) continue;

                    if (prefab.GetComponentInChildren(def.WrapperType, includeInactive: true) != null) continue;

                    string variantName = $"Networked{prefab.name}.prefab";
                    string outputPath  = $"{OUTPUT_FOLDER}/{variantName}";
                    var existing       = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);

                    byDef[def].Add(new PrefabEntry
                    {
                        Def             = def,
                        OriginalPrefab  = prefab,
                        OriginalPath    = path,
                        OutputPath      = outputPath,
                        VariantExists   = existing != null,
                        ExistingVariant = existing,
                    });
                }
            }
        }

        /// <summary>Headless "Create All Missing Variants"; run before the scene migration so the swap finds every variant.</summary>
        [MenuItem("Tools/Fast And Fractured/Networked Prop Setup - Create All Missing (Headless)", priority = 11)]
        public static void CreateAllMissingHeadless()
        {
            var byDef = new Dictionary<HazardDef, List<PrefabEntry>>();
            ScanPrefabs(byDef);

            int created = 0;
            foreach (var kvp in byDef)
            {
                foreach (var e in kvp.Value.Where(e => !e.VariantExists))
                {
                    CreateVariant(e);
                    created++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PropSetup] Headless create-all complete: {created} variant(s) generated. " +
                      "Remember to run Fish-Networking > Refresh Default Prefab Objects if any were created.");
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawInfoBox();

            if (_needsFishNetRefresh)
                DrawFishNetReminder();

            int totalMissing = _byDef.Values.Sum(l => l.Count(e => !e.VariantExists));

            EditorGUILayout.Space(SPACING);
            using (new EditorGUI.DisabledScope(totalMissing == 0))
            {
                if (GUILayout.Button($"Create All Missing Variants  ({totalMissing})", GUILayout.Height(PRIMARY_BUTTON_HEIGHT)))
                {
                    foreach (var kvp in _byDef)
                        foreach (var e in kvp.Value.Where(e => !e.VariantExists).ToList())
                            CreateVariant(e);
                    _needsFishNetRefresh = true;
                    Refresh();
                }
            }

            EditorGUILayout.Space(SECTION_SPACING);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var def in HAZARD_DEFS)
            {
                var entries = _byDef[def];
                DrawSection(def, entries);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Networked Prop Setup", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Refresh();
            }
        }

        private static void DrawInfoBox()
        {
            EditorGUILayout.HelpBox(
                "Scans the project for singleplayer hazard prefabs and creates prefab variants " +
                "in  " + OUTPUT_FOLDER + "/  with NetworkObject + the matching server-authoritative wrapper.\n" +
                "Scene-only objects (placed directly in a scene without a prefab source) are not " +
                "shown here — handle them via the scene migration tool.",
                MessageType.Info);
        }

        private static void DrawFishNetReminder()
        {
            EditorGUILayout.HelpBox(
                "Variants created!  Run these two menu items so FishNet registers them:\n" +
                "  Tools > Fish-Networking > Refresh Default Prefabs\n" +
                "  Tools > Fish-Networking > Refresh All NetworkObjects",
                MessageType.Warning);
        }

        private void DrawSection(HazardDef def, List<PrefabEntry> entries)
        {
            EditorGUILayout.LabelField(def.DisplayName, EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (entries.Count == 0)
                {
                    string msg = def.SceneOnlyNote ?? "No prefabs with this component found in the project.";
                    EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.Space(SMALL_SPACING);
                    return;
                }

                foreach (var entry in entries)
                    DrawEntry(entry);
            }

            EditorGUILayout.Space(SPACING);
        }

        private void DrawEntry(PrefabEntry entry)
        {
            Color bg = entry.VariantExists
                ? ROW_DONE_COLOR
                : ROW_MISSING_COLOR;

            var rect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, bg);

            var prevCol = GUI.color;
            GUI.color = entry.VariantExists ? Color.green : Color.yellow;
            GUILayout.Label(entry.VariantExists ? "✓" : "○", GUILayout.Width(18));
            GUI.color = prevCol;

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(entry.OriginalPrefab.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(entry.OriginalPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                entry.VariantExists
                    ? $"→ {entry.OutputPath}"
                    : $"→ {entry.OutputPath}  [missing]",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (entry.VariantExists)
            {
                if (GUILayout.Button("Select", GUILayout.Width(ROW_BUTTON_WIDTH), GUILayout.Height(ROW_BUTTON_HEIGHT)))
                    Selection.activeObject = entry.ExistingVariant;
            }
            else
            {
                var prev2 = GUI.color;
                GUI.color = Color.yellow;
                if (GUILayout.Button("Create\nVariant", GUILayout.Width(ROW_BUTTON_WIDTH), GUILayout.Height(ROW_BUTTON_HEIGHT)))
                {
                    CreateVariant(entry);
                    _needsFishNetRefresh = true;
                    Refresh();
                    GUIUtility.ExitGUI();
                }
                GUI.color = prev2;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(SMALL_SPACING);
        }

        private static void CreateVariant(PrefabEntry entry)
        {
            EnsureOutputFolder();

            // A prefab instance, so SaveAsPrefabAsset produces a variant.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.OriginalPrefab);
            if (instance == null)
            {
                Debug.LogError($"[NetworkedPropSetup] Could not instantiate {entry.OriginalPath}");
                return;
            }

            try
            {
                // The singleplayer component may sit on a child: the wrapper goes on that GameObject.
                var spComponent = instance.GetComponentInChildren(entry.Def.SingleplayerType, includeInactive: true);
                if (spComponent == null)
                {
                    Debug.LogError($"[NetworkedPropSetup] Singleplayer component {entry.Def.SingleplayerType.Name} " +
                                   $"vanished from instance of {entry.OriginalPath}. Variant NOT created.");
                    return;
                }
                GameObject targetGO = spComponent.gameObject;

                // NetworkObject first so the wrapper's [RequireComponent] finds it.
                if (targetGO.GetComponent<NetworkObject>() == null)
                    targetGO.AddComponent<NetworkObject>();

                if (targetGO.GetComponent(entry.Def.WrapperType) == null)
                    targetGO.AddComponent(entry.Def.WrapperType);

                var saved = PrefabUtility.SaveAsPrefabAsset(instance, entry.OutputPath);
                if (saved != null)
                {
                    TryRefreshNetworkObject(saved);

                    Debug.Log($"[NetworkedPropSetup] Created: {entry.OutputPath} (wrapper on '{targetGO.name}')", saved);
                }
                else
                {
                    Debug.LogError($"[NetworkedPropSetup] SaveAsPrefabAsset failed for {entry.OutputPath}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void TryRefreshNetworkObject(GameObject prefab)
        {
            try
            {
                var nob = prefab.GetComponentInChildren<NetworkObject>(includeInactive: true);
                if (nob == null) return;

                var miReserialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                miReserialize?.Invoke(nob, new object[] { /*setWasActiveDuringEdit*/ true, /*setSceneId*/ false });

                EditorUtility.SetDirty(nob);
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssetIfDirty(prefab);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkedPropSetup] Could not auto-refresh NetworkObject: {ex.Message}. " +
                                  "Run  Tools > Fish-Networking > Refresh All NetworkObjects  manually.");
            }
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
                AssetDatabase.CreateFolder(OUTPUT_FOLDER_PARENT, OUTPUT_SUBFOLDER);
        }

        /// <summary>The hazard component on a prefab variant's own root (inherited from its base).</summary>
        private static Component FindVariantRootComponent(GameObject prefab, Type spType)
        {
            if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant) return null;
            var c = prefab.GetComponent(spType);
            return c != null && PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject) == prefab ? c : null;
        }

        /// <summary>The hazard component owned by the prefab itself, not by a nested prefab instance in it.</summary>
        private static Component FindOwnedComponentInPrefab(GameObject prefab, Type spType)
        {
            var components = prefab.GetComponentsInChildren(spType, includeInactive: true);
            foreach (var c in components)
            {
                if (c == null) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject) == null)
                    return c;
            }
            return null;
        }
    }
}
