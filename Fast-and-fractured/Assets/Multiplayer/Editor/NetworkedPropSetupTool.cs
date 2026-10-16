using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Scans the project for singleplayer hazard prefabs that have no networked variant yet,
    /// and creates prefab variants in Assets/Multiplayer/Props/ with the NetworkObject +
    /// the appropriate server-authoritative wrapper component.
    ///
    /// Scene-only objects (e.g. boundary walls placed directly in a scene without a prefab)
    /// are out of scope — handle them via the scene migration tool.
    ///
    /// After creating variants, run Tools > Fish-Networking > Refresh Default Prefabs and
    /// Tools > Fish-Networking > Refresh All NetworkObjects so FishNet registers the new
    /// prefabs and populates their NetworkBehaviours lists.
    ///
    /// Open via: Tools > Fast And Fractured > Networked Prop Setup
    /// </summary>
    public class NetworkedPropSetupTool : EditorWindow
    {
        private const string OUTPUT_FOLDER     = "Assets/Multiplayer/Props";
        private const string OUTPUT_FOLDER_PARENT = "Assets/Multiplayer";
        private const string OUTPUT_SUBFOLDER     = "Props";

        // ── Hazard component → wrapper mapping ─────────────────────────────────
        private sealed class HazardDef
        {
            public string DisplayName;
            public Type   SingleplayerType;
            public Type   WrapperType;
            public string SceneOnlyNote;     // non-null if this may not exist as a prefab
            // A prefab variant of this hazard gets its own networked variant. For pickups the
            // variant IS the pickup (RecoverHealth Variant is ShootingDamage with the health
            // model, icon, colours and sound); without its own networked variant the maps
            // placed NetworkedShootingDamage and health pickups looked like damage boosts.
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
                // ParabolicImpulse is the trampoline brain. NetworkedTrampoline is a thin
                // server-authoritative wrapper that disables non-server impulse logic and
                // broadcasts the release animation. Trampoline.prefab GUID is
                // b7b7d367759108a47b95ebd71043b191 — but the scan finds it by component, no
                // GUID hardcoding needed here.
                DisplayName      = "Trampoline  (ParabolicImpulse)",
                SingleplayerType = typeof(ParabolicImpulse),
                WrapperType      = typeof(NetworkedTrampoline),
            },
            // World systems added in the 2026-06 sync audit. Wrappers existed but nothing
            // generated their prefab variants, so the scene migration swap silently skipped
            // them (SandstormParent.prefab instance in every map had no networked variant).
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
                // Golf balls (dev, 2026-10): server-simulated, streamed to clients.
                DisplayName      = "Golf Ball",
                SingleplayerType = typeof(GolfBallBehaviour),
                WrapperType      = typeof(NetworkedGolfBall),
            },
        };

        // ── State ──────────────────────────────────────────────────────────────
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
            w.minSize = new Vector2(620f, 480f);
            w.Refresh();
            w.Show();
        }

        private void OnEnable() => Refresh();

        // ── Scan ──────────────────────────────────────────────────────────────
        private void Refresh()
        {
            _byDef.Clear();
            _needsFishNetRefresh = false;
            ScanPrefabs(_byDef);
            Repaint();
        }

        /// <summary>
        /// Static scan shared by the window and the headless menu item. Fills the map with one
        /// PrefabEntry per (hazard def, source prefab) pair found in the project.
        /// </summary>
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
                    // Scan the entire prefab tree (Trampoline.prefab has ParabolicImpulse on
                    // its TrampolineParabolicLogic child, so root-only scans miss it). BUT we
                    // must filter out CONTAINER prefabs that hold the hazard via nested prefab
                    // instances — e.g. Trampolines.prefab and TrampolineMountainVillage.prefab
                    // contain Trampoline.prefab instances. For those, the swap should be done
                    // on the nested Trampoline instance at scene-migration time, not by creating
                    // a NetworkedTrampolines.prefab variant.
                    var spComponent = FindOwnedComponentInPrefab(prefab, def.SingleplayerType);
                    if (spComponent == null && def.VariantsGetOwnPrefab)
                        spComponent = FindVariantRootComponent(prefab, def.SingleplayerType);
                    if (spComponent == null) continue;

                    // Skip if any GO in the tree already carries the wrapper.
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

        /// <summary>
        /// Headless "Create All Missing Variants" — same as the window button, invocable from
        /// the menu / MCP automation. Run this BEFORE the Scene Migration re-apply pass so the
        /// swap finds every variant it needs (e.g. NetworkedSandstormParent.prefab).
        /// </summary>
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

        // ── GUI ───────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawToolbar();
            DrawInfoBox();

            if (_needsFishNetRefresh)
                DrawFishNetReminder();

            int totalMissing = _byDef.Values.Sum(l => l.Count(e => !e.VariantExists));

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(totalMissing == 0))
            {
                if (GUILayout.Button($"Create All Missing Variants  ({totalMissing})", GUILayout.Height(30f)))
                {
                    foreach (var kvp in _byDef)
                        foreach (var e in kvp.Value.Where(e => !e.VariantExists).ToList())
                            CreateVariant(e);
                    _needsFishNetRefresh = true;
                    Refresh();
                }
            }

            EditorGUILayout.Space(6f);
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
                    EditorGUILayout.Space(2f);
                    return;
                }

                foreach (var entry in entries)
                    DrawEntry(entry);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawEntry(PrefabEntry entry)
        {
            Color bg = entry.VariantExists
                ? new Color(0.2f, 0.4f, 0.2f, 0.3f)
                : new Color(0.35f, 0.15f, 0.15f, 0.3f);

            var rect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, bg);

            // Status icon
            var prevCol = GUI.color;
            GUI.color = entry.VariantExists ? Color.green : Color.yellow;
            GUILayout.Label(entry.VariantExists ? "✓" : "○", GUILayout.Width(18));
            GUI.color = prevCol;

            // Names
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

            // Action button
            if (entry.VariantExists)
            {
                if (GUILayout.Button("Select", GUILayout.Width(72), GUILayout.Height(40)))
                    Selection.activeObject = entry.ExistingVariant;
            }
            else
            {
                var prev2 = GUI.color;
                GUI.color = Color.yellow;
                if (GUILayout.Button("Create\nVariant", GUILayout.Width(72), GUILayout.Height(40)))
                {
                    CreateVariant(entry);
                    _needsFishNetRefresh = true;
                    Refresh();
                    GUIUtility.ExitGUI();
                }
                GUI.color = prev2;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
        }

        // ── Prefab variant creation ────────────────────────────────────────────
        private static void CreateVariant(PrefabEntry entry)
        {
            EnsureOutputFolder();

            // Instantiate as a prefab instance — SaveAsPrefabAsset will record the base
            // relationship and store only the overrides, producing a true prefab variant.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.OriginalPrefab);
            if (instance == null)
            {
                Debug.LogError($"[NetworkedPropSetup] Could not instantiate {entry.OriginalPath}");
                return;
            }

            try
            {
                // The singleplayer component may live on a child of the prefab (e.g.
                // ParabolicImpulse sits on Trampoline/TrampolineParabolicLogic, not on the
                // Trampoline root). Find that GameObject and attach the wrapper there — putting
                // it on the root would either fail [RequireComponent] checks or, worse, force
                // a duplicate singleplayer component to be auto-added to the root.
                var spComponent = instance.GetComponentInChildren(entry.Def.SingleplayerType, includeInactive: true);
                if (spComponent == null)
                {
                    Debug.LogError($"[NetworkedPropSetup] Singleplayer component {entry.Def.SingleplayerType.Name} " +
                                   $"vanished from instance of {entry.OriginalPath}. Variant NOT created.");
                    return;
                }
                GameObject targetGO = spComponent.gameObject;

                // Add NetworkObject first so [RequireComponent] on the wrapper finds it.
                if (targetGO.GetComponent<NetworkObject>() == null)
                    targetGO.AddComponent<NetworkObject>();

                // Add the server-authoritative wrapper on the same GO as the singleplayer component.
                if (targetGO.GetComponent(entry.Def.WrapperType) == null)
                    targetGO.AddComponent(entry.Def.WrapperType);

                // Save. Because instance was created via InstantiatePrefab, Unity records
                // this as a prefab variant of entry.OriginalPrefab.
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, entry.OutputPath);
                if (saved != null)
                {
                    // Force FishNet to populate the NetworkBehaviours list immediately
                    // by re-saving the asset via SerializedObject (mirrors what FishNet's
                    // "Refresh All NetworkObjects" menu item does).
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

        /// <summary>
        /// Attempts to trigger FishNet's NetworkObject serialization so the NetworkBehaviours
        /// list is populated immediately (mirrors the per-asset part of "Refresh All NetworkObjects").
        /// Falls back silently if the FishNet editor API is not accessible.
        /// </summary>
        private static void TryRefreshNetworkObject(GameObject prefab)
        {
            try
            {
                // NetworkObject may not be on the root (Trampoline's variant has it on
                // TrampolineParabolicLogic). Search the whole prefab tree.
                var nob = prefab.GetComponentInChildren<NetworkObject>(includeInactive: true);
                if (nob == null) return;

                // Call FishNet's internal ReserializeEditorSetValues to fully populate the
                // NetworkBehaviours list. Without this the runtime throws
                // "ObjectId [65535] is expected to be initialized but was not" on spawn.
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

        /// <summary>
        /// Returns a singleplayer component owned DIRECTLY by this prefab asset (i.e. not coming
        /// from a nested prefab instance embedded within it). For a Prefab Asset loaded via
        /// AssetDatabase.LoadAssetAtPath, PrefabUtility.GetCorrespondingObjectFromSource on a
        /// GameObject returns:
        ///   - null  : the GO is owned by THIS prefab directly
        ///   - non-null : the GO is part of a nested prefab instance within this asset
        /// We only want to create variants for prefabs that OWN the hazard logic, otherwise we'd
        /// generate redundant NetworkedTrampolines.prefab / NetworkedTrampolineMountainVillage.prefab
        /// containers that just re-wrap the same nested logic.
        /// </summary>
        /// <summary>
        /// The hazard component of a prefab VARIANT, when it sits on the variant's own root
        /// (inherited from the base, not inside a nested prefab). Containers holding hazard
        /// instances are still skipped.
        /// </summary>
        private static Component FindVariantRootComponent(GameObject prefab, Type spType)
        {
            if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant) return null;
            var c = prefab.GetComponent(spType);
            return c != null && PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject) == prefab ? c : null;
        }

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
