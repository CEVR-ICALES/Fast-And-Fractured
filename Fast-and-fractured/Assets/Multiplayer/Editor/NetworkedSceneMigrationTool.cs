using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastAndFractured.Multiplayer.Lobby;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Duplicates a singleplayer map scene into Assets/Multiplayer/Scenes/Networked&lt;Name&gt;.unity
    /// and swaps every hazard prefab instance for its networked variant from Assets/Multiplayer/Props/.
    /// Scene-only hazard GameObjects (not from a prefab) get NetworkObject + the wrapper added directly.
    ///
    /// The original singleplayer scene under Assets/FastAndFractured/ is NEVER modified — the only
    /// scene that gets touched is the duplicate written to Assets/Multiplayer/Scenes/.
    ///
    /// Open via: Tools > Fast And Fractured > Scene Migration
    /// </summary>
    public class NetworkedSceneMigrationTool : EditorWindow
    {
        private const string OUTPUT_FOLDER        = "Assets/Multiplayer/Scenes";
        private const string OUTPUT_FOLDER_PARENT = "Assets/Multiplayer";
        private const string OUTPUT_SUBFOLDER     = "Scenes";
        private const string PROPS_FOLDER         = "Assets/Multiplayer/Props";

        // ── Source map definitions (mirrors NetworkedMapRegistrationTool DEFAULT_MAPS) ─────
        private static readonly SourceMap[] SOURCE_MAPS =
        {
            new SourceMap { SceneGuid = "1c251044aeaa846428dbf86821c160d3", DisplayName = "Golf Map" },
            new SourceMap { SceneGuid = "f0e339ead76f23943b9db6bcdaf20d92", DisplayName = "Construction Map" },
            new SourceMap { SceneGuid = "1354e600b46f3c3488593866c2ca43b3", DisplayName = "Mountain Village Map" },
        };

        // ── Hazard component → wrapper mapping (mirrors NetworkedPropSetupTool) ────────────
        private sealed class HazardDef
        {
            public string DisplayName;
            public Type   SingleplayerType;
            public Type   WrapperType;
        }

        private static readonly HazardDef[] HAZARD_DEFS =
        {
            new HazardDef { DisplayName = "Dead Walls",         SingleplayerType = typeof(DeadWallsBehaivour), WrapperType = typeof(NetworkedDeadWallsBehaviour)  },
            new HazardDef { DisplayName = "Breakable Platform", SingleplayerType = typeof(BreakablePlatform),  WrapperType = typeof(NetworkedBreakablePlatform)   },
            new HazardDef { DisplayName = "Point Explosion",    SingleplayerType = typeof(PointExplosion),     WrapperType = typeof(NetworkedPointExplosion)      },
            new HazardDef { DisplayName = "Mega Chicken Boss",  SingleplayerType = typeof(ChickenBrain),       WrapperType = typeof(NetworkedMegaChickenBoss)     },
            new HazardDef { DisplayName = "Trampoline",         SingleplayerType = typeof(ParabolicImpulse),   WrapperType = typeof(NetworkedTrampoline)          },
            // World systems that were written but never wired (2026-06 sync audit): without these
            // defs the networked scenes keep the singleplayer components running locally on every
            // instance, so storms/pickups/buildings desync between host and clients.
            new HazardDef { DisplayName = "Sandstorm",          SingleplayerType = typeof(SandstormController), WrapperType = typeof(NetworkedSandstormController)     },
            new HazardDef { DisplayName = "Stats Boost Pickup", SingleplayerType = typeof(StatsBoostInteractable), WrapperType = typeof(NetworkedStatsBoostInteractable) },
            new HazardDef { DisplayName = "Skin Unlock Pickup", SingleplayerType = typeof(SkinUnlockerInteractable), WrapperType = typeof(NetworkedSkinUnlockerInteractable) },
            new HazardDef { DisplayName = "Sucking Building",   SingleplayerType = typeof(SuckingBuilding),     WrapperType = typeof(NetworkedSuckingBuilding)         },
            new HazardDef { DisplayName = "Building Dead Zone", SingleplayerType = typeof(BuildingDeadZone),    WrapperType = typeof(NetworkedBuildingDeadZone)        },
            new HazardDef { DisplayName = "Golf Ball",          SingleplayerType = typeof(GolfBallBehaviour),   WrapperType = typeof(NetworkedGolfBall)                },
        };

        private struct SourceMap
        {
            public string SceneGuid;
            public string DisplayName;
        }

        // ── Per-row state ──────────────────────────────────────────────────────────────────
        private sealed class Row
        {
            public SourceMap   Source;
            public string      SourcePath;
            public string      OutputName;          // NetworkedConstructionMap
            public string      OutputPath;          // Assets/Multiplayer/Scenes/NetworkedConstructionMap.unity
            public bool        SourceFound;
            public bool        OutputExists;
            public DateTime    SourceModified;
            public DateTime    OutputModified;
            public bool        OutputIsStale;       // source modified after output
            public string      LastReport;          // last migration summary
        }

        private readonly List<Row> _rows = new List<Row>();
        private Vector2 _scroll;

        [MenuItem("Tools/Fast And Fractured/Scene Migration", priority = 12)]
        public static void ShowWindow()
        {
            var w = GetWindow<NetworkedSceneMigrationTool>("Scene Migration");
            w.minSize = new Vector2(640f, 500f);
            w.Refresh();
            w.Show();
        }

        /// <summary>
        /// Headless "Re-apply Swaps to All" — same pass the window button runs, but invocable
        /// without opening the window (menu item / CI / MCP automation). Iterates every source
        /// map that already has a networked variant and re-runs the swap + gating + repoint pass.
        /// </summary>
        [MenuItem("Tools/Fast And Fractured/Scene Migration - Reapply All (Headless)", priority = 13)]
        public static void ReapplyAllHeadless()
        {
            // Save any pending scene edits up front WITHOUT a modal dialog, then run each pass in
            // non-interactive mode so nothing blocks the main thread.
            EditorSceneManager.SaveOpenScenes();

            int scenes = 0;
            foreach (var src in SOURCE_MAPS)
            {
                string srcPath = AssetDatabase.GUIDToAssetPath(src.SceneGuid);
                if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath)) continue;

                string outName = "Networked" + Path.GetFileNameWithoutExtension(srcPath);
                string outPath = $"{OUTPUT_FOLDER}/{outName}.unity";
                if (!File.Exists(outPath)) continue;

                var row = new Row
                {
                    Source       = src,
                    SourcePath   = srcPath,
                    OutputName   = outName,
                    OutputPath   = outPath,
                    SourceFound  = true,
                    OutputExists = true,
                };
                string report = ReapplyPrefabSwaps(row, interactive: false);
                Debug.Log($"[SceneMigration] {report}");
                scenes++;
            }
            Debug.Log($"[SceneMigration] Headless re-apply complete over {scenes} networked scene(s).");
        }

        private void OnEnable() => Refresh();

        // ── Scan ───────────────────────────────────────────────────────────────────────────
        private void Refresh()
        {
            _rows.Clear();
            EnsureOutputFolder();

            foreach (var src in SOURCE_MAPS)
            {
                string srcPath  = AssetDatabase.GUIDToAssetPath(src.SceneGuid);
                bool   srcFound = !string.IsNullOrEmpty(srcPath) && File.Exists(srcPath);
                string srcName  = srcFound ? Path.GetFileNameWithoutExtension(srcPath) : src.DisplayName;
                string outName  = "Networked" + srcName;
                string outPath  = $"{OUTPUT_FOLDER}/{outName}.unity";
                bool   outExists = File.Exists(outPath);

                DateTime srcMod = srcFound  ? File.GetLastWriteTimeUtc(srcPath) : DateTime.MinValue;
                DateTime outMod = outExists ? File.GetLastWriteTimeUtc(outPath) : DateTime.MinValue;

                _rows.Add(new Row
                {
                    Source         = src,
                    SourcePath     = srcPath,
                    OutputName     = outName,
                    OutputPath     = outPath,
                    SourceFound    = srcFound,
                    OutputExists   = outExists,
                    SourceModified = srcMod,
                    OutputModified = outMod,
                    OutputIsStale  = outExists && srcMod > outMod,
                });
            }

            Repaint();
        }

        // ── GUI ────────────────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawToolbar();
            DrawInfoBox();

            EditorGUILayout.Space(4f);
            DrawBulkReapplyBar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var row in _rows)
                DrawRow(row);

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Scene Migration", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Refresh();
            }
        }

        private static void DrawInfoBox()
        {
            EditorGUILayout.HelpBox(
                "Duplicates a singleplayer map and swaps every hazard prefab instance for its " +
                "networked variant from " + PROPS_FOLDER + "/.\n\n" +
                "Outputs to " + OUTPUT_FOLDER + "/Networked<Name>.unity. The singleplayer scene under " +
                "Assets/FastAndFractured/ is never modified.\n\n" +
                "Run  Tools > Fast And Fractured > Networked Prop Setup  first to make sure all " +
                "networked variants exist.\n\n" +
                "Use \"Re-apply Swaps\" (per row) to push new wrappers into already-migrated scenes " +
                "WITHOUT rebuilding from source — manual edits are preserved.",
                MessageType.Info);
        }

        private void DrawBulkReapplyBar()
        {
            int migrated = _rows.Count(r => r.OutputExists);
            if (migrated == 0) return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "Bulk: apply newly-added wrappers (e.g. NetworkedTrampoline) to every existing " +
                    "networked map without rebuilding from source.",
                    EditorStyles.wordWrappedMiniLabel);

                var prev = GUI.color;
                GUI.color = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button($"Re-apply Swaps to All ({migrated})", GUILayout.Width(220), GUILayout.Height(28)))
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    int totalSwaps = 0;
                    foreach (var row in _rows)
                    {
                        if (!row.OutputExists) continue;
                        var report = ReapplyPrefabSwaps(row);
                        row.LastReport = report;
                        totalSwaps += ExtractFirstIntFromReport(report);
                    }
                    Debug.Log($"[SceneMigration] Bulk re-apply complete. ~{totalSwaps} prefab swaps total across {migrated} scenes.");
                    GUIUtility.ExitGUI();
                }
                GUI.color = prev;
            }
            EditorGUILayout.Space(4f);
        }

        // Cheap parser used only to aggregate counts for the bulk-action toast — extracts the
        // first integer it sees in the per-scene report text. Returns 0 if none found.
        private static int ExtractFirstIntFromReport(string report)
        {
            if (string.IsNullOrEmpty(report)) return 0;
            int n = 0;
            bool reading = false;
            foreach (var c in report)
            {
                if (c >= '0' && c <= '9') { n = n * 10 + (c - '0'); reading = true; }
                else if (reading) return n;
            }
            return n;
        }

        private void DrawRow(Row row)
        {
            Color bg = !row.SourceFound
                ? new Color(0.4f, 0.15f, 0.15f, 0.3f)
                : row.OutputExists
                    ? (row.OutputIsStale ? new Color(0.4f, 0.35f, 0.1f, 0.3f) : new Color(0.2f, 0.4f, 0.2f, 0.3f))
                    : new Color(0.25f, 0.25f, 0.25f, 0.3f);

            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, bg);

            // Header
            using (new EditorGUILayout.HorizontalScope())
            {
                string icon;
                Color  iconColor;
                if      (!row.SourceFound)   { icon = "✗"; iconColor = Color.red;    }
                else if (!row.OutputExists)  { icon = "○"; iconColor = Color.yellow; }
                else if (row.OutputIsStale)  { icon = "⚠"; iconColor = Color.yellow; }
                else                         { icon = "✓"; iconColor = Color.green;  }

                var prev = GUI.color;
                GUI.color = iconColor;
                GUILayout.Label(icon, GUILayout.Width(18));
                GUI.color = prev;

                GUILayout.Label(row.Source.DisplayName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

                if (!row.SourceFound)
                {
                    GUI.color = Color.red;
                    GUILayout.Label($"Source GUID not found: {row.Source.SceneGuid}", EditorStyles.miniLabel);
                    GUI.color = prev;
                }
                else if (!row.OutputExists)
                {
                    var p2 = GUI.color;
                    GUI.color = Color.yellow;
                    if (GUILayout.Button("Create Networked Variant", GUILayout.Width(190), GUILayout.Height(28)))
                    {
                        var report = MigrateScene(row);
                        row.LastReport = report;
                        Refresh();
                        GUIUtility.ExitGUI();
                    }
                    GUI.color = p2;
                }
                else
                {
                    // Non-destructive: opens the networked scene, runs the swap pass over any
                    // remaining singleplayer hazard instances (e.g. Trampoline → NetworkedTrampoline
                    // after a new wrapper was added), and saves. Manual edits to the scene are kept.
                    var p3 = GUI.color;
                    GUI.color = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button("Re-apply Swaps", GUILayout.Width(120), GUILayout.Height(24)))
                    {
                        var report = ReapplyPrefabSwaps(row);
                        row.LastReport = report;
                        GUIUtility.ExitGUI();
                    }
                    GUI.color = p3;

                    if (row.OutputIsStale)
                    {
                        var p2 = GUI.color;
                        GUI.color = Color.yellow;
                        if (GUILayout.Button("Re-sync from Source", GUILayout.Width(150), GUILayout.Height(24)))
                        {
                            if (EditorUtility.DisplayDialog("Re-sync " + row.OutputName,
                                $"This deletes {row.OutputPath} and rebuilds it from " +
                                $"{row.SourcePath}.\n\nAny manual edits to the networked scene will be LOST.",
                                "Yes, rebuild from source", "Cancel"))
                            {
                                AssetDatabase.DeleteAsset(row.OutputPath);
                                var report = MigrateScene(row);
                                row.LastReport = report;
                                Refresh();
                                GUIUtility.ExitGUI();
                            }
                        }
                        GUI.color = p2;
                    }

                    if (GUILayout.Button("Open", GUILayout.Width(60)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(row.OutputPath, OpenSceneMode.Single);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(row.OutputPath);
                }
            }

            // Paths
            if (row.SourceFound)
            {
                EditorGUILayout.LabelField("source",  row.SourcePath, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("output",  row.OutputPath, EditorStyles.miniLabel);
            }

            // Stale notice
            if (row.OutputIsStale)
            {
                TimeSpan delta = row.SourceModified - row.OutputModified;
                EditorGUILayout.HelpBox(
                    $"Source scene was modified {FormatDelta(delta)} after the networked variant was last generated. " +
                    "Re-sync to pull in the changes (manual edits to the networked scene will be lost).",
                    MessageType.Warning);
            }

            // Last migration report
            if (!string.IsNullOrEmpty(row.LastReport))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(row.LastReport, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static string FormatDelta(TimeSpan d)
        {
            if (d.TotalDays    >= 1) return $"{(int)d.TotalDays}d";
            if (d.TotalHours   >= 1) return $"{(int)d.TotalHours}h";
            if (d.TotalMinutes >= 1) return $"{(int)d.TotalMinutes}m";
            return $"{(int)d.TotalSeconds}s";
        }

        // ── Re-apply Swaps (non-destructive) ───────────────────────────────────────────────
        /// <summary>
        /// Opens the already-migrated networked scene and runs the hazard swap pass over it.
        /// Unlike MigrateScene this does NOT copy from the source — it preserves every manual
        /// edit (lights, decals, camera tweaks, lobby-specific changes). It only touches
        /// GameObjects whose components match a singleplayer hazard type that doesn't yet have
        /// its networked wrapper attached. Run after adding a new HazardDef (e.g. Trampoline) so
        /// the new wrapper rolls out into every existing networked map.
        ///
        /// FIRST performs a cleanup pass that strips bad NetworkObject + wrapper overrides that
        /// a previous (buggy) migration added to prefab children. These overrides cause the
        /// FishNet "ObjectId [65535] is expected to be initialized but was not" runtime error.
        /// </summary>
        private static string ReapplyPrefabSwaps(Row row) => ReapplyPrefabSwaps(row, interactive: true);

        private static string ReapplyPrefabSwaps(Row row, bool interactive)
        {
            if (!row.OutputExists)
                return "Networked scene does not exist yet — use \"Create Networked Variant\" first.";

            // In interactive (window) mode we may prompt to save; in headless mode we must NOT
            // open modal dialogs (they block Unity's main thread indefinitely, which hangs the
            // MCP bridge / CI). Headless callers are responsible for having a clean scene state.
            if (interactive)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return "Cancelled (unsaved changes).";
            }

            // Open the existing networked scene in place. NO source copy — manual edits preserved.
            var scene = EditorSceneManager.OpenScene(row.OutputPath, OpenSceneMode.Single);

            // Cleanup pass — undo previous buggy adds before doing fresh swaps.
            int strippedOverrides = StripBadOverrideWrappers(scene);

            int prefabSwaps       = 0;
            int sceneOnlyConverts = 0;
            var perDefCounts      = new Dictionary<HazardDef, int>();
            foreach (var d in HAZARD_DEFS) perDefCounts[d] = 0;

            foreach (var rootGO in scene.GetRootGameObjects())
                MigrateGameObjectTree(rootGO, ref prefabSwaps, ref sceneOnlyConverts, perDefCounts);

            int overridesRestored = RestoreSwappedHazardOverrides(scene, row.SourcePath);
            int autoInitSuppressed = SuppressLevelControllerAutoInit(scene);
            int prefabsRepointed   = RepointLevelControllerPrefabs(scene);
            int interactableFixes  = EnsureInteractableInfra(scene);

            if (prefabSwaps > 0 || sceneOnlyConverts > 0 || strippedOverrides > 0 || overridesRestored > 0 || autoInitSuppressed > 0 || prefabsRepointed > 0 || interactableFixes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                AssignSceneIds(scene);
                EditorSceneManager.SaveScene(scene, row.OutputPath);
            }

            string perDef = string.Join("\n",
                perDefCounts.Where(kv => kv.Value > 0)
                            .Select(kv => $"  • {kv.Key.DisplayName}: {kv.Value}"));
            if (string.IsNullOrEmpty(perDef)) perDef = "  • (no remaining singleplayer hazards found)";

            string report = $"Re-applied swaps to  {row.OutputName}\n" +
                            $"  Stripped bad overrides:    {strippedOverrides}\n" +
                            $"  Prefab swaps:              {prefabSwaps}\n" +
                            $"  Scene-only converts:       {sceneOnlyConverts}\n" +
                            $"  SP overrides restored:     {overridesRestored}\n" +
                            $"  LevelControllers gated:    {autoInitSuppressed}\n" +
                            $"  LC prefabs repointed:      {prefabsRepointed}\n{perDef}";

            Debug.Log("[SceneMigration] " + report.Replace("\n", "  "));
            return report;
        }

        /// <summary>
        /// Disables auto-Construct/Initialize on every LevelController in the given scene by
        /// flipping the three serialized flags to false via SerializedObject. Persisted into the
        /// scene file, so at runtime LC stays dormant until the wrapper explicitly drives it.
        ///
        /// This is the editor-time replacement for the runtime reflection-based teardown — once
        /// every networked scene has been processed by this, the AutoInitGate's destroy-and-reset
        /// path is a no-op.
        /// </summary>
        // Networked spawn prefabs the migrated scene's LevelController must reference instead of
        // the singleplayer ones. Without this, PlayerBaseFinalWithoutCar (no NetworkObject, no
        // singleton preservers) gets instantiated in multiplayer: the second player's duplicate
        // singletons resolve via DESTROY_GAMEOBJECT and the match implodes (see memory:
        // project-networked-scene-player-prefab).
        private const string NETWORKED_PLAYER_PREFAB_PATH  = "Assets/Multiplayer/Player/Prefabs/NetworkedPlayer.prefab";
        private const string NETWORKED_AI_PREFAB_PATH      = "Assets/FastAndFractured/EnemyAI/Prefabs/NetworkedEnemyAI.prefab";

        /// <summary>
        /// Points every LevelController's PlayerPrefab / AIPrefab at the networked variants.
        /// Editor-time, persisted into the scene file — same pattern as auto-init suppression.
        /// Returns how many fields were actually repointed.
        /// </summary>
        private static int RepointLevelControllerPrefabs(Scene scene)
        {
            var networkedPlayer = AssetDatabase.LoadAssetAtPath<GameObject>(NETWORKED_PLAYER_PREFAB_PATH);
            var networkedAi     = AssetDatabase.LoadAssetAtPath<GameObject>(NETWORKED_AI_PREFAB_PATH);
            if (networkedPlayer == null)
                Debug.LogError($"[SceneMigration] Missing {NETWORKED_PLAYER_PREFAB_PATH} — PlayerPrefab NOT repointed.");
            if (networkedAi == null)
                Debug.LogError($"[SceneMigration] Missing {NETWORKED_AI_PREFAB_PATH} — AIPrefab NOT repointed.");

            var playerInjector = networkedPlayer != null ? networkedPlayer.GetComponent<CarInjector>() : null;
            var aiInjector     = networkedAi     != null ? networkedAi.GetComponent<CarInjector>()     : null;
            if (networkedPlayer != null && playerInjector == null)
                Debug.LogError("[SceneMigration] NetworkedPlayer.prefab has no CarInjector on its root — cannot repoint PlayerPrefab.");
            if (networkedAi != null && aiInjector == null)
                Debug.LogError("[SceneMigration] NetworkedEnemyAI.prefab has no CarInjector on its root — cannot repoint AIPrefab.");

            int repointed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var lc in root.GetComponentsInChildren<LevelController>(true))
                {
                    var so = new SerializedObject(lc);
                    bool changed = false;

                    var pPlayer = so.FindProperty("PlayerPrefab");
                    if (pPlayer != null && playerInjector != null &&
                        !ReferenceEquals(pPlayer.objectReferenceValue, playerInjector))
                    {
                        pPlayer.objectReferenceValue = playerInjector;
                        changed = true;
                        repointed++;
                    }

                    var pAi = so.FindProperty("AIPrefab");
                    if (pAi != null && aiInjector != null &&
                        !ReferenceEquals(pAi.objectReferenceValue, aiInjector))
                    {
                        pAi.objectReferenceValue = aiInjector;
                        changed = true;
                        repointed++;
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(lc);
                    }
                }
            }
            if (repointed > 0)
                Debug.Log($"[SceneMigration] Repointed {repointed} LevelController prefab field(s) in '{scene.name}' to networked variants.");
            return repointed;
        }

        /// <summary>
        /// The prefab swaps destroy the scene objects InteractableHandler.interactablesToToggle
        /// referenced (every migrated map ended up with 25 null entries → UnassignedReferenceException
        /// in Construct at runtime). Rebuild the array from the interactables actually present, and
        /// make sure a scene-placed NetworkedInteractableHandler exists so pool state replicates.
        /// Returns how many changes were made (0 = untouched).
        /// </summary>
        private static int EnsureInteractableInfra(Scene scene)
        {
            int changes = 0;

            InteractableHandler handler = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                handler = root.GetComponentInChildren<InteractableHandler>(true);
                if (handler != null) break;
            }

            if (handler != null)
            {
                var entries = new List<GameObject>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var interactable in root.GetComponentsInChildren<GenericInteractable>(true))
                        if (!entries.Contains(interactable.gameObject))
                            entries.Add(interactable.gameObject);

                var so = new SerializedObject(handler);
                var arrayProp = so.FindProperty("interactablesToToggle");
                bool needsRewire = arrayProp == null ? false : arrayProp.arraySize != entries.Count;
                if (!needsRewire && arrayProp != null)
                {
                    for (int i = 0; i < arrayProp.arraySize; i++)
                    {
                        if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        {
                            needsRewire = true;
                            break;
                        }
                    }
                }

                if (needsRewire)
                {
                    arrayProp.arraySize = entries.Count;
                    for (int i = 0; i < entries.Count; i++)
                        arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changes++;
                    Debug.Log($"[SceneMigration] Rewired InteractableHandler.interactablesToToggle with {entries.Count} interactables in '{scene.name}'.");
                }
            }

            // NOTE: the NetworkedInteractableHandler wrapper is deliberately NOT scene-placed.
            // Scene NetworkObjects added by code end up with an unserialized SceneId ("expected
            // to be initialized but was not") — the same failure LobbyServerBootstrapper documents
            // for the lobby manager. It is runtime-spawned from NetworkedLevelControllerManager
            // instead (Resources/NetworkedInteractablesSpawnable). Remove any wrapper a previous
            // version of this tool left in the scene.
            foreach (var root in scene.GetRootGameObjects())
            {
                var legacyWrapper = root.GetComponentInChildren<NetworkedInteractableHandler>(true);
                if (legacyWrapper != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacyWrapper.gameObject);
                    changes++;
                    Debug.Log($"[SceneMigration] Removed legacy scene-placed NetworkedInteractables from '{scene.name}' (now runtime-spawned).");
                }
            }

            return changes;
        }

        /// <summary>
        /// A swap keeps only the transform, so everything the singleplayer map had set on a swapped
        /// hazard was lost - the references its instance carried as overrides, the values the level
        /// designers tuned per map, and the references other objects held to it. Measured 2026-09-30
        /// on all three maps: the storm had no sphereCenter (UnassignedReferenceException, it could
        /// never start), the level had no storm ("_sandStormController is null", the AI never saw
        /// it), the storm ran on prefab defaults (maxGrowthTime, sphereRadius, kill times), and the
        /// golf map's rising lava used the prefab's 20 s rise instead of the map's 120 s, so a car on
        /// the low spawn point died six seconds into the match.
        ///
        /// Reads the singleplayer source scene and, for every swapped instance, re-applies the
        /// instance overrides of the singleplayer one:
        ///  - a value only where the networked variant still has the singleplayer prefab's value,
        ///    so a deliberate multiplayer change always wins;
        ///  - a reference only where the networked field is empty.
        /// Then every empty reference other objects had to a swapped instance is re-pointed at the
        /// networked one. Objects are matched by hierarchy path (a swap keeps the name, the parent
        /// and the sibling order) and components by type and order. Returns how many properties
        /// were restored.
        /// </summary>
        private static int RestoreSwappedHazardOverrides(Scene output, string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return 0;
            var swappedRoots = FindSwappedHazardRoots(output);
            if (swappedRoots.Count == 0) return 0;

            Scene source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            int restored = 0;
            try
            {
                // Each networked instance and the singleplayer instance it replaced.
                var replaced = new List<KeyValuePair<Transform, Transform>>();
                foreach (var outRoot in swappedRoots)
                {
                    Transform srcRoot = FindByScenePath(source, ScenePath(outRoot.transform));
                    if (srcRoot == null) continue;
                    Transform dstRoot = outRoot.transform;

                    // Placed before the singleplayer prefab variant had its own networked
                    // variant (health pickups as NetworkedShootingDamage): place the right one.
                    var wanted = NetworkedVariantFor(srcRoot.gameObject);
                    var placed = PrefabUtility.GetCorrespondingObjectFromSource(dstRoot.gameObject);
                    if (wanted != null && placed != wanted)
                    {
                        var swapped = ReplaceInstance(dstRoot.gameObject, wanted);
                        if (swapped == null) continue;
                        Debug.Log($"[SceneMigration] Re-swapped '{ScenePath(swapped.transform)}' from {placed?.name} to {wanted.name}.");
                        dstRoot = swapped.transform;
                        restored++;
                    }
                    replaced.Add(new KeyValuePair<Transform, Transform>(srcRoot, dstRoot));
                }

                // 0. What the singleplayer map deleted from or added to its instance.
                foreach (var pair in replaced)
                    restored += MirrorSingleplayerHierarchy(pair.Key, pair.Value);

                // 1. The overrides of the replaced singleplayer instances.
                foreach (var pair in replaced)
                {
                    Transform srcRoot = pair.Key, outRoot = pair.Value;
                    Func<UnityEngine.Object, UnityEngine.Object> mapReference = target =>
                    {
                        Transform targetTransform = TransformOf(target);
                        if (targetTransform == null) return null;
                        return targetTransform.IsChildOf(srcRoot) ? FindEquivalent(target, srcRoot, outRoot) : MapToScene(target, output);
                    };

                    foreach (var srcTransform in srcRoot.GetComponentsInChildren<Transform>(true))
                    {
                        bool isRoot = srcTransform == srcRoot;
                        var dstGameObject = FindEquivalent(srcTransform.gameObject, srcRoot, outRoot) as GameObject;
                        if (dstGameObject == null) continue;
                        // The swap placed and named the root itself.
                        if (!isRoot) restored += CopyInstanceOverrides(srcTransform.gameObject, dstGameObject, mapReference);

                        foreach (var srcComponent in srcTransform.GetComponents<Component>())
                        {
                            if (srcComponent == null || (isRoot && srcComponent is Transform)) continue;
                            var dstComponent = FindEquivalent(srcComponent, srcRoot, outRoot);
                            if (dstComponent != null) restored += CopyInstanceOverrides(srcComponent, dstComponent, mapReference);
                        }
                    }
                }

                // 2. Empty references other objects had to a replaced instance.
                foreach (var root in source.GetRootGameObjects())
                {
                    foreach (var srcBehaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (srcBehaviour == null) continue;
                        bool insideReplaced = false;
                        foreach (var pair in replaced)
                            if (srcBehaviour.transform.IsChildOf(pair.Key)) { insideReplaced = true; break; }
                        if (insideReplaced) continue;

                        UnityEngine.Object outBehaviour = MapToScene(srcBehaviour, output);
                        if (outBehaviour == null) continue;
                        restored += CopyEmptySceneReferences(srcBehaviour, outBehaviour, target =>
                        {
                            Transform targetTransform = TransformOf(target);
                            if (targetTransform == null) return null;
                            foreach (var pair in replaced)
                                if (targetTransform.IsChildOf(pair.Key))
                                    return FindEquivalent(target, pair.Key, pair.Value);
                            return null;
                        });
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(source, removeScene: true);
            }

            if (restored > 0)
                Debug.Log($"[SceneMigration] Restored {restored} singleplayer override(s) lost by the prefab swaps in '{output.name}'.");
            return restored;
        }

        /// <summary>
        /// Gives a swapped instance the hierarchy the singleplayer instance has. The swap places a
        /// fresh variant, so what the map deleted from its instance came back and what it added
        /// was lost:
        ///  - deleted GameObjects / components (m_RemovedGameObjects / m_RemovedComponents):
        ///    ConstructionMap keeps one of the 63 walls of the DeadZone prefab (the kill floor
        ///    under the map) and the networked map had all 63 - red walls across the play area
        ///    that cars spawned and drove into (2026-10-16). Matched by the base prefab object
        ///    they come from, so same-named walls cannot be confused and what the networked
        ///    variant adds is never touched.
        ///  - added GameObjects (the breakable platforms swap the prefab's stage meshes for their
        ///    own): copied to the same parent and sibling position.
        /// Returns how many objects were deleted or added.
        /// </summary>
        private static int MirrorSingleplayerHierarchy(Transform srcRoot, Transform outRoot)
        {
            var srcBase = PrefabUtility.GetCorrespondingObjectFromOriginalSource(srcRoot.gameObject);
            string baseAsset = srcBase != null ? AssetDatabase.GetAssetPath(srcBase) : null;
            if (string.IsNullOrEmpty(baseAsset)) return 0;

            // Every base prefab object the singleplayer instance still has.
            var kept = new HashSet<UnityEngine.Object>();
            foreach (var t in srcRoot.GetComponentsInChildren<Transform>(true))
            {
                kept.Add(PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject));
                foreach (var c in t.GetComponents<Component>())
                    if (c != null) kept.Add(PrefabUtility.GetCorrespondingObjectFromOriginalSource(c));
            }

            bool RemovedInSingleplayer(UnityEngine.Object obj)
            {
                var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj);
                return original != null && AssetDatabase.GetAssetPath(original) == baseAsset && !kept.Contains(original);
            }

            int removed = 0;
            foreach (var t in outRoot.GetComponentsInChildren<Transform>(true).ToList())
            {
                if (t == null || t == outRoot) continue;   // already gone with its parent
                if (RemovedInSingleplayer(t.gameObject))
                {
                    Debug.Log($"[SceneMigration] Removed '{ScenePath(t)}' (deleted in the singleplayer map).");
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                    removed++;
                    continue;
                }
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null || c is Transform || !RemovedInSingleplayer(c)) continue;
                    Debug.Log($"[SceneMigration] Removed {c.GetType().Name} from '{ScenePath(t)}' (deleted in the singleplayer map).");
                    UnityEngine.Object.DestroyImmediate(c);
                    removed++;
                }
            }

            int added = 0;
            foreach (var t in srcRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == srcRoot) continue;
                var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                if (original != null && AssetDatabase.GetAssetPath(original) == baseAsset) continue;   // the prefab's own
                if (FindEquivalent(t.gameObject, srcRoot, outRoot) != null) continue;                    // already there
                if (!(FindEquivalent(t.parent.gameObject, srcRoot, outRoot) is GameObject parent)) continue;   // copied with its parent

                var copy = UnityEngine.Object.Instantiate(t.gameObject, parent.transform, false);
                copy.name = t.name;
                copy.transform.SetLocalPositionAndRotation(t.localPosition, t.localRotation);
                copy.transform.localScale = t.localScale;
                copy.transform.SetSiblingIndex(Mathf.Min(t.GetSiblingIndex(), parent.transform.childCount - 1));
                Debug.Log($"[SceneMigration] Added '{ScenePath(copy.transform)}' (added in the singleplayer map).");
                added++;
            }
            return removed + added;
        }

        /// <summary>Outermost instances of the networked variants the swap pass places.</summary>
        private static List<Transform> FindSwappedHazardRoots(Scene scene)
        {
            var roots = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && IsNetworkedVariantInstance(t.gameObject))
                        roots.Add(t);
                }
            }
            return roots;
        }

        private static bool IsNetworkedVariantInstance(GameObject prefabInstanceRoot)
        {
            string assetPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(prefabInstanceRoot));
            return !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith(PROPS_FOLDER + "/Networked", StringComparison.Ordinal);
        }

        /// <summary>
        /// Re-applies to <paramref name="destination"/> what the singleplayer map changed on
        /// <paramref name="source"/> relative to the base prefab both come from (its map variant and
        /// its scene overrides): a value only where the networked instance still has the base value,
        /// a reference only where the networked field is empty. Fields the networked scene already
        /// overrides are left alone, so running the pass again changes nothing.
        /// </summary>
        private static int CopyInstanceOverrides(UnityEngine.Object source, UnityEngine.Object destination,
            Func<UnityEngine.Object, UnityEngine.Object> mapReference)
        {
            UnityEngine.Object baseObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(source);
            if (baseObject == null || baseObject == source) return 0;

            var srcSo = new SerializedObject(source);
            var baseSo = new SerializedObject(baseObject);
            var dstSo = new SerializedObject(destination);
            int restored = 0;
            var it = srcSo.GetIterator();
            bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = it.propertyType == SerializedPropertyType.Generic;
                if (enterChildren || IsUnityInternal(it.propertyPath)) continue;

                SerializedProperty dstProp = dstSo.FindProperty(it.propertyPath);
                if (dstProp == null || dstProp.propertyType != it.propertyType || dstProp.prefabOverride) continue;

                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    UnityEngine.Object value = it.objectReferenceValue;
                    if (value == null || dstProp.objectReferenceValue != null) continue;
                    UnityEngine.Object mapped = EditorUtility.IsPersistent(value) ? value : mapReference(value);
                    if (mapped == null) continue;
                    dstProp.objectReferenceValue = mapped;
                }
                else
                {
                    if (!IsCopyableValue(it.propertyType)) continue;
                    object srcValue = it.boxedValue, dstValue = dstProp.boxedValue;
                    if (Equals(srcValue, dstValue)) continue;
                    // The networked variant changed this value itself: keep the multiplayer one.
                    // (An array element past the base's size has no base value to compare with.)
                    SerializedProperty baseProp = baseSo.FindProperty(it.propertyPath);
                    if (baseProp != null && !Equals(baseProp.boxedValue, dstValue)) continue;
                    dstProp.boxedValue = srcValue;
                }
                restored++;
                Debug.Log($"[SceneMigration] Restored {destination.GetType().Name}.{it.propertyPath} on '{ScenePath(TransformOf(destination))}'.");
            }
            if (restored > 0)
            {
                dstSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(destination);
            }
            return restored;
        }

        private static bool IsCopyableValue(SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.String:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.AnimationCurve:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.BoundsInt:
                case SerializedPropertyType.Hash128:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Copies every scene-object reference of <paramref name="source"/> that <paramref name="map"/>
        /// resolves into the same field of <paramref name="destination"/>, where that field is empty.
        /// Arrays of references the destination does not have yet are grown to the source's size.
        /// </summary>
        private static int CopyEmptySceneReferences(UnityEngine.Object source, UnityEngine.Object destination,
            Func<UnityEngine.Object, UnityEngine.Object> map)
        {
            var srcSo = new SerializedObject(source);
            var dstSo = new SerializedObject(destination);
            int restored = 0;
            var it = srcSo.GetIterator();
            bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = it.propertyType == SerializedPropertyType.Generic;
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.arrayElementType.StartsWith("PPtr<", StringComparison.Ordinal))
                {
                    var dstArray = dstSo.FindProperty(it.propertyPath);
                    if (dstArray != null && dstArray.isArray && dstArray.arraySize == 0 && it.arraySize > 0)
                        dstArray.arraySize = it.arraySize;
                    continue;
                }
                if (it.propertyType != SerializedPropertyType.ObjectReference || IsUnityInternal(it.propertyPath)) continue;
                UnityEngine.Object value = it.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value)) continue;

                var dstProp = dstSo.FindProperty(it.propertyPath);
                if (dstProp == null || dstProp.propertyType != SerializedPropertyType.ObjectReference || dstProp.objectReferenceValue != null) continue;
                UnityEngine.Object mapped = map(value);
                if (mapped == null) continue;

                dstProp.objectReferenceValue = mapped;
                restored++;
                Debug.Log($"[SceneMigration] Restored {destination.GetType().Name}.{it.propertyPath} on '{ScenePath(TransformOf(destination))}' -> '{ScenePath(TransformOf(mapped))}'.");
            }
            if (restored > 0)
            {
                dstSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(destination);
            }
            return restored;
        }

        // Unity's own bookkeeping and the hierarchy itself: never copied between instances (copying
        // Transform.m_Children linked children twice to their parent).
        private static readonly string[] UnityInternalProperties =
        {
            "m_Script", "m_GameObject", "m_PrefabInstance", "m_CorrespondingSourceObject", "m_PrefabAsset",
            "m_Children", "m_Father", "m_Component", "m_ObjectHideFlags", "m_EditorHideFlags",
        };

        private static bool IsUnityInternal(string propertyPath)
        {
            foreach (string name in UnityInternalProperties)
                if (propertyPath == name || propertyPath.StartsWith(name + ".", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static Transform TransformOf(UnityEngine.Object obj) =>
            obj is GameObject go ? go.transform : obj is Component c ? c.transform : null;

        /// <summary>"Root#0/Child#1": names, and which of the same-named siblings.</summary>
        private static string ScenePath(Transform t)
        {
            if (t == null) return null;
            var parts = new List<string>();
            for (Transform current = t; current != null; current = current.parent)
                parts.Add($"{current.name}#{SameNameIndex(current)}");
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static int SameNameIndex(Transform t)
        {
            int index = 0;
            if (t.parent != null)
            {
                for (int i = 0; i < t.parent.childCount; i++)
                {
                    Transform sibling = t.parent.GetChild(i);
                    if (sibling == t) return index;
                    if (sibling.name == t.name) index++;
                }
                return index;
            }
            foreach (var root in t.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == t) return index;
                if (root.name == t.name) index++;
            }
            return index;
        }

        private static Transform FindByScenePath(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            Transform current = null;
            for (int p = 0; p < parts.Length; p++)
            {
                int hash = parts[p].LastIndexOf('#');
                string name = parts[p].Substring(0, hash);
                int wanted = int.Parse(parts[p].Substring(hash + 1));
                int seen = 0;
                Transform next = null;
                if (current == null)
                {
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.name == name && seen++ == wanted) { next = root.transform; break; }
                }
                else
                {
                    for (int i = 0; i < current.childCount; i++)
                    {
                        Transform child = current.GetChild(i);
                        if (child.name == name && seen++ == wanted) { next = child; break; }
                    }
                }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        /// <summary>The object at the same path in <paramref name="scene"/> (same component type and order).</summary>
        private static UnityEngine.Object MapToScene(UnityEngine.Object obj, Scene scene)
        {
            Transform target = FindByScenePath(scene, ScenePath(TransformOf(obj)));
            return target == null ? null : SameObjectOn(obj, target);
        }

        /// <summary>The object at the same path below <paramref name="toRoot"/> as <paramref name="obj"/> is below <paramref name="fromRoot"/>.</summary>
        private static UnityEngine.Object FindEquivalent(UnityEngine.Object obj, Transform fromRoot, Transform toRoot)
        {
            Transform t = TransformOf(obj);
            if (t == null) return null;
            var parts = new List<string>();
            for (Transform current = t; current != fromRoot; current = current.parent)
            {
                if (current == null) return null;
                parts.Add($"{current.name}#{SameNameIndex(current)}");
            }
            Transform target = toRoot;
            for (int p = parts.Count - 1; p >= 0 && target != null; p--)
            {
                int hash = parts[p].LastIndexOf('#');
                string name = parts[p].Substring(0, hash);
                int wanted = int.Parse(parts[p].Substring(hash + 1));
                int seen = 0;
                Transform next = null;
                for (int i = 0; i < target.childCount; i++)
                {
                    Transform child = target.GetChild(i);
                    if (child.name == name && seen++ == wanted) { next = child; break; }
                }
                target = next;
            }
            return target == null ? null : SameObjectOn(obj, target);
        }

        private static UnityEngine.Object SameObjectOn(UnityEngine.Object obj, Transform target)
        {
            if (obj is GameObject) return target.gameObject;
            if (!(obj is Component component)) return null;
            var mine = component.GetComponents(component.GetType());
            int index = Array.IndexOf(mine, component);
            var theirs = target.GetComponents(component.GetType());
            return index >= 0 && index < theirs.Length ? theirs[index] : null;
        }

        private static int SuppressLevelControllerAutoInit(Scene scene)
        {
            int suppressed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var lc in root.GetComponentsInChildren<LevelController>(true))
                {
                    var so = new SerializedObject(lc);
                    var pConstruct = so.FindProperty("_autoConstructOnAwake");
                    var pStart     = so.FindProperty("_autoInitializeOnStart");
                    var pEnable    = so.FindProperty("_autoInitializeOnEnable");

                    bool changed = false;
                    if (pConstruct != null && pConstruct.boolValue) { pConstruct.boolValue = false; changed = true; }
                    if (pStart     != null && pStart.boolValue)     { pStart.boolValue     = false; changed = true; }
                    if (pEnable    != null && pEnable.boolValue)    { pEnable.boolValue    = false; changed = true; }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(lc);
                        suppressed++;
                    }
                }
            }
            if (suppressed > 0)
                Debug.Log($"[SceneMigration] Suppressed auto-init on {suppressed} LevelController(s) in '{scene.name}'.");
            return suppressed;
        }

        /// <summary>
        /// Removes NetworkObject + wrapper components that an earlier buggy migration added to
        /// CHILDREN of prefab instances as prefab overrides. These overrides never get a SceneId
        /// from FishNet's OnValidate pipeline (which only runs for the prefab asset itself, not
        /// for added-component overrides on instances), so at runtime FishNet logs the
        /// "ObjectId [65535] is expected to be initialized but was not" error.
        ///
        /// Only strips components that are confirmed prefab-override "added components". Anything
        /// the user manually added to a non-prefab scene GO or to a prefab root is left alone.
        /// </summary>
        private static int StripBadOverrideWrappers(Scene scene)
        {
            int removed = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var def in HAZARD_DEFS)
                {
                    var wrappers = root.GetComponentsInChildren(def.WrapperType, includeInactive: true);
                    foreach (var wrapper in wrappers)
                    {
                        var go = wrapper.gameObject;

                        // Skip if not part of a prefab instance.
                        if (PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.Connected) continue;
                        // Skip if this is the outermost root — that's the correct place for a
                        // wrapper added via legitimate variant swap, leave it alone.
                        if (PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                        // Only target wrappers added as overrides (not inherited from the variant prefab).
                        if (!PrefabUtility.IsAddedComponentOverride(wrapper)) continue;

                        var nob = go.GetComponent<FishNet.Object.NetworkObject>();
                        if (nob != null && PrefabUtility.IsAddedComponentOverride(nob))
                        {
                            UnityEngine.Object.DestroyImmediate(nob, allowDestroyingAssets: false);
                        }
                        UnityEngine.Object.DestroyImmediate(wrapper, allowDestroyingAssets: false);
                        removed++;
                        Debug.Log($"[SceneMigration] Stripped bad override: removed {def.WrapperType.Name} (+ NetworkObject if also override) from prefab child '{go.name}'.");
                    }
                }
            }

            return removed;
        }

        // ── Migration ──────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Duplicates the source scene into OUTPUT_FOLDER and swaps hazard prefab instances for
        /// their networked variants. Returns a human-readable migration report.
        /// </summary>
        private static string MigrateScene(Row row)
        {
            EnsureOutputFolder();

            // Make sure we don't lose unsaved work in the currently-open scene.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return "Cancelled (unsaved changes).";

            // Copy the .unity asset (and its .meta) — Unity treats this as a new scene asset.
            if (!AssetDatabase.CopyAsset(row.SourcePath, row.OutputPath))
            {
                Debug.LogError($"[SceneMigration] Failed to copy {row.SourcePath} → {row.OutputPath}");
                return "Copy failed.";
            }
            AssetDatabase.Refresh();

            // Open the duplicated scene.
            var scene = EditorSceneManager.OpenScene(row.OutputPath, OpenSceneMode.Single);

            int prefabSwaps     = 0;
            int sceneOnlyConverts = 0;
            var perDefCounts    = new Dictionary<HazardDef, int>();
            foreach (var d in HAZARD_DEFS) perDefCounts[d] = 0;

            // Walk every root, then every descendant.
            foreach (var root in scene.GetRootGameObjects())
            {
                MigrateGameObjectTree(root, ref prefabSwaps, ref sceneOnlyConverts, perDefCounts);
            }

            // The swaps keep only the transform; bring back the scene wiring from the source.
            int overridesRestored = RestoreSwappedHazardOverrides(scene, row.SourcePath);

            // Disable LC auto-init in the scene file — wrapper will drive Construct/Initialize.
            int autoInitSuppressed = SuppressLevelControllerAutoInit(scene);

            // Point LC's PlayerPrefab / AIPrefab at the networked variants.
            int prefabsRepointed = RepointLevelControllerPrefabs(scene);

            // Rewire InteractableHandler refs broken by the swaps + place the networking wrapper.
            EnsureInteractableInfra(scene);

            // Save the modified duplicate.
            EditorSceneManager.MarkSceneDirty(scene);
            AssignSceneIds(scene);
            EditorSceneManager.SaveScene(scene, row.OutputPath);

            // Auto-register: add to Build Settings and update MapCatalog.NetworkedSceneName.
            bool addedToBuild = AddToBuildSettingsIfMissing(row.OutputPath);
            bool catalogUpdated = TryWriteNetworkedSceneNameIntoCatalog(
                Path.GetFileNameWithoutExtension(row.SourcePath), row.OutputName);

            string perDef = string.Join("\n",
                perDefCounts.Where(kv => kv.Value > 0)
                            .Select(kv => $"  • {kv.Key.DisplayName}: {kv.Value}"));
            if (string.IsNullOrEmpty(perDef)) perDef = "  • (no hazard objects found)";

            string extras = "";
            if (addedToBuild)   extras += "\n  + Added to Build Settings";
            if (catalogUpdated) extras += "\n  + MapCatalog.NetworkedSceneName updated";

            string report = $"Migrated  {row.OutputName}\n" +
                            $"  Prefab swaps:           {prefabSwaps}\n" +
                            $"  Scene-only converts:    {sceneOnlyConverts}\n" +
                            $"  SP overrides restored: {overridesRestored}\n" +
                            $"  LevelControllers gated: {autoInitSuppressed}\n" +
                            $"  LC prefabs repointed:   {prefabsRepointed}\n{perDef}{extras}";

            Debug.Log("[SceneMigration] " + report.Replace("\n", "  "));
            return report;
        }

        private static bool AddToBuildSettingsIfMissing(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == scenePath)) return false;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            return true;
        }

        /// <summary>
        /// If the catalog has an entry whose SceneName matches the source scene's filename,
        /// write the networked variant's name into entry.NetworkedSceneName. No-op if the
        /// source scene isn't registered yet (user can register later via Map Registration Tool).
        /// </summary>
        private static bool TryWriteNetworkedSceneNameIntoCatalog(string sourceSceneName, string networkedSceneName)
        {
            const string CATALOG_PATH = "Assets/Multiplayer/Lobby/Resources/MapCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG_PATH);
            if (catalog == null) return false;

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            if (entries == null) return false;

            bool changed = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var elem = entries.GetArrayElementAtIndex(i);
                var sceneNameProp = elem.FindPropertyRelative("SceneName");
                if (sceneNameProp == null) continue;
                if (sceneNameProp.stringValue != sourceSceneName) continue;

                var netProp = elem.FindPropertyRelative("NetworkedSceneName");
                if (netProp == null) continue; // catalog hasn't been recompiled yet
                if (netProp.stringValue == networkedSceneName) return false;

                netProp.stringValue = networkedSceneName;
                changed = true;
                break;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        /// <summary>
        /// Recursive migration. For each GameObject we find a hazard component on, we walk UP to
        /// the outermost prefab instance root and try to swap that root with a networked variant.
        /// Walking-up matters because some singleplayer prefabs (e.g. Trampoline) put their hazard
        /// component on a CHILD GameObject — swapping the child directly would orphan its siblings
        /// and adding NetworkObject as a prefab override on a child leaves it with no SceneId,
        /// causing the FishNet "ObjectId [65535] is expected to be initialized but was not" error.
        ///
        /// Tracks already-processed prefab instance roots so we don't try to swap the same
        /// Trampoline twice when both the root and a child contain components of interest.
        /// </summary>
        private static void MigrateGameObjectTree(
            GameObject go,
            ref int prefabSwaps,
            ref int sceneOnlyConverts,
            Dictionary<HazardDef, int> perDefCounts,
            HashSet<int> swappedRootInstanceIds = null)
        {
            swappedRootInstanceIds ??= new HashSet<int>();

            // Collect children first because we may destroy/replace `go` or its ancestors.
            var children = new List<Transform>();
            for (int i = 0; i < go.transform.childCount; i++)
                children.Add(go.transform.GetChild(i));

            // Find which hazard def (if any) this GO matches.
            HazardDef matched = null;
            foreach (var def in HAZARD_DEFS)
            {
                if (go.GetComponent(def.SingleplayerType) != null && go.GetComponent(def.WrapperType) == null)
                {
                    matched = def;
                    break;
                }
            }

            if (matched != null)
            {
                // Walk up to the outermost prefab instance root. If the GO itself is a root, this
                // returns the GO. If it's a child of a prefab instance, this returns the parent
                // root prefab. If the GO is scene-only (no prefab connection), this returns null.
                GameObject prefabRoot = null;
                if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.Connected)
                    prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);

                if (prefabRoot != null)
                {
                    // Skip if we already swapped this prefab instance in a previous tree visit
                    // (e.g. encountered the root and a hazard-carrying child in the same migration).
                    if (!swappedRootInstanceIds.Add(prefabRoot.GetInstanceID()))
                        return;

                    // Already a networked variant whose wrapper sits on another object than the
                    // singleplayer component (SuckingBuildingPrefab, DeadZones*): nothing to swap.
                    // Re-swapping it replaced the instance on every Reapply - new file ids and
                    // SceneIds each run (1229 changed lines in MountainVillage, 2026-09-30).
                    if (IsNetworkedVariantInstance(prefabRoot))
                        return;

                    // The hazard sits in a prefab NESTED inside a group prefab (dev's TNT.prefab
                    // holding 25 TNTs, GolfBalls.prefab, Trampolines.prefab). Swapping the outermost
                    // root replaced the whole group with ONE networked hazard - the group "TNT" even
                    // matched NetworkedTNT by name and 24 TNTs vanished (2026-10-12). Unpack the
                    // group one level in this networked copy and swap each hazard on its own.
                    var nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (nearestRoot != null && nearestRoot != prefabRoot && HasNetworkedVariant(nearestRoot))
                    {
                        swappedRootInstanceIds.Remove(prefabRoot.GetInstanceID());
                        PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                        MigrateGameObjectTree(prefabRoot, ref prefabSwaps, ref sceneOnlyConverts, perDefCounts, swappedRootInstanceIds);
                        return;
                    }

                    bool replaced = TryReplaceWithNetworkedVariant(prefabRoot, matched, out var newGo);
                    if (replaced)
                    {
                        prefabSwaps++;
                        perDefCounts[matched] = perDefCounts[matched] + 1;
                        // Don't recurse into the replacement — the variant has the wrapper already.
                        return;
                    }

                    // Variant prefab missing — log and stop. Do NOT fall back to AddWrapperInPlace
                    // on a prefab child: that creates a NetworkObject prefab-override with no
                    // SceneId, which is exactly the "[65535] not initialized" runtime error we
                    // are trying to fix. The user should generate the missing variant via
                    // Tools > Fast And Fractured > Networked Prop Setup.
                    Debug.LogWarning($"[SceneMigration] Cannot swap '{prefabRoot.name}' (variant missing). " +
                                     $"Generate the networked variant first; skipping in-place wrapper for " +
                                     "prefab child to avoid the [65535] NetworkObject error.");
                    return;
                }

                // Scene-only path: GO is NOT part of any prefab. AddWrapperInPlace is safe here
                // because a scene-only NetworkObject gets a SceneId from the standard FishNet
                // OnValidate pipeline (we trigger it explicitly below).
                AddWrapperInPlace(go, matched);
                sceneOnlyConverts++;
                perDefCounts[matched] = perDefCounts[matched] + 1;
            }

            // Recurse into children (they may also carry hazards, or be containers for them).
            foreach (var child in children)
            {
                if (child != null) // guard against destroyed transforms
                    MigrateGameObjectTree(child.gameObject, ref prefabSwaps, ref sceneOnlyConverts, perDefCounts, swappedRootInstanceIds);
            }
        }

        /// <summary>
        /// Give every NetworkObject in the networked scene its SceneId before saving. Swapped and
        /// unpacked hazards (nested NetworkObjects such as TrampolineParabolicLogic/TriggerCheck)
        /// were saved without one, so the server never spawned them and the match never started
        /// ("ObjectId [65535] is expected to be initialized", 2026-10-12). Same as FishNet's
        /// Utility > Reserialize NetworkObjects > Scenes, which uses this internal method.
        /// </summary>
        private static void AssignSceneIds(Scene scene)
        {
            var create = typeof(NetworkObject).GetMethod("CreateSceneId",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null, new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
            if (create == null)
            {
                Debug.LogError("[SceneMigration] FishNet NetworkObject.CreateSceneId not found - run Fish-Networking > Utility > Reserialize NetworkObjects on the networked scenes.");
                return;
            }
            var args = new object[] { scene, true, 0 };
            create.Invoke(null, args);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>The prefab this instance comes from has a Networked&lt;Name&gt; variant in PROPS_FOLDER.</summary>
        private static bool HasNetworkedVariant(GameObject instanceRoot) => NetworkedVariantFor(instanceRoot) != null;

        /// <summary>
        /// The networked variant to place for an instance: the one made from the instance's own
        /// prefab variant when Networked Prop Setup generated one (NetworkedRecoverHealth
        /// Variant), else "Networked&lt;BaseName&gt;" of the base prefab. Map variants without their
        /// own networked variant (Trampolines, BigTrampoline) keep resolving to the base one.
        /// </summary>
        private static GameObject NetworkedVariantFor(GameObject instanceRoot)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            for (var asset = source; asset != null; asset = PrefabUtility.GetCorrespondingObjectFromSource(asset))
            {
                var own = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROPS_FOLDER}/Networked{asset.name}.prefab");
                if (own != null && PrefabUtility.GetCorrespondingObjectFromSource(own) == asset) return own;
            }
            var baseAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceRoot);
            if (baseAsset == null) baseAsset = source;
            return baseAsset == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>($"{PROPS_FOLDER}/Networked{baseAsset.name}.prefab");
        }

        /// <summary>
        /// `go` must be the outermost root of a prefab instance (callers walk up to it). Finds the
        /// networked variant by base prefab name → "Networked&lt;BaseName&gt;.prefab" in PROPS_FOLDER
        /// and swaps the instance, preserving world transform and sibling index.
        /// </summary>
        private static bool TryReplaceWithNetworkedVariant(GameObject go, HazardDef def, out GameObject newInstance)
        {
            newInstance = null;

            // Only operate on prefab-instance roots — replacing a non-root would orphan siblings.
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status != PrefabInstanceStatus.Connected) return false;
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) return false;

            // e.g. Trampolines.prefab (map variant) → NetworkedTrampoline (base), but
            // RecoverHealth Variant → NetworkedRecoverHealth Variant (see NetworkedVariantFor).
            var variantAsset = NetworkedVariantFor(go);
            if (variantAsset == null)
            {
                Debug.LogWarning($"[SceneMigration] No networked variant found for '{go.name}'. " +
                                 "Run Networked Prop Setup first, or this hazard will be left untouched.");
                return false;
            }
            newInstance = ReplaceInstance(go, variantAsset);
            return newInstance != null;
        }

        /// <summary>Swaps <paramref name="go"/> for an instance of <paramref name="variantAsset"/>, keeping its pose, name and sibling index.</summary>
        private static GameObject ReplaceInstance(GameObject go, GameObject variantAsset)
        {
            // Capture transform so we can restore it on the replacement.
            var t = go.transform;
            Vector3   pos    = t.position;
            Quaternion rot   = t.rotation;
            Vector3   scale  = t.localScale;
            string    goName = go.name;
            var       parent = t.parent;
            int       siblingIdx = t.GetSiblingIndex();

            // Instantiate the variant in the scene at the parent's position.
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(variantAsset, go.scene);
            if (inst == null) return null;

            Undo.RegisterCreatedObjectUndo(inst, "Replace with networked variant");
            inst.transform.SetParent(parent, worldPositionStays: true);
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.transform.localScale = scale;
            inst.transform.SetSiblingIndex(siblingIdx);
            inst.name = goName;

            // Destroy the original.
            UnityEngine.Object.DestroyImmediate(go);
            return inst;
        }

        /// <summary>
        /// Scene-only fallback: GameObject is not part of any prefab. Add NetworkObject + the
        /// matching wrapper component directly. This is identical to what NetworkedPropSetupTool
        /// does for prefab variants, but applied in-place inside the duplicated scene.
        /// </summary>
        private static void AddWrapperInPlace(GameObject go, HazardDef def)
        {
            if (go.GetComponent<NetworkObject>() == null)
                go.AddComponent<NetworkObject>();

            if (go.GetComponent(def.WrapperType) == null)
                go.AddComponent(def.WrapperType);

            Debug.Log($"[SceneMigration] Scene-only conversion: added {def.WrapperType.Name} to '{go.name}'.");
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
                AssetDatabase.CreateFolder(OUTPUT_FOLDER_PARENT, OUTPUT_SUBFOLDER);
        }
    }
}
