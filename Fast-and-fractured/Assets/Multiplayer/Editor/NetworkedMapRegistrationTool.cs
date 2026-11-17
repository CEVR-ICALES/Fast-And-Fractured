using System.Collections.Generic;
using System.Linq;
using FastAndFractured.Multiplayer.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>Registers project scenes in MapCatalog; the default maps are listed by GUID in DEFAULT_MAPS.</summary>
    public class NetworkedMapRegistrationTool : EditorWindow
    {
        private const string CATALOG_RESOURCE_PATH = "Assets/Multiplayer/Lobby/Resources/MapCatalog.asset";
        private const string RESOURCES_DIR         = "Assets/Multiplayer/Lobby/Resources";

        private const float SPACING = 4f;
        private const float SMALL_SPACING = 2f;
        private const float BUTTON_HEIGHT = 24f;
        private const float LARGE_BUTTON_HEIGHT = 26f;
        private const float SMALL_BUTTON_WIDTH = 70f;
        private const float ROW_BUTTON_WIDTH = 80f;
        private const float ICON_WIDTH = 18f;
        private static readonly Vector2 MIN_WINDOW_SIZE = new Vector2(580f, 560f);
        private static readonly Color ROW_REGISTERED_COLOR = new Color(0.2f, 0.4f, 0.2f, 0.25f);
        private static readonly Color ROW_DEFAULT_COLOR = new Color(0.3f, 0.3f, 0.1f, 0.25f);
        private static readonly Color ROW_OTHER_COLOR = new Color(0.15f, 0.15f, 0.15f, 0.25f);

        // Default maps by scene GUID (stable across renames).
        private static readonly DefaultMapDef[] DEFAULT_MAPS =
        {
            new DefaultMapDef
            {
                SceneGuid   = "1c251044aeaa846428dbf86821c160d3",   // GolfMap.unity
                DisplayName = "Menu.Golf",
                CatalogId   = 0,
                ThumbnailSearchName = "GOLF2",
            },
            new DefaultMapDef
            {
                SceneGuid   = "f0e339ead76f23943b9db6bcdaf20d92",   // ConstructionMap.unity
                DisplayName = "Menu.Obras",
                CatalogId   = 1,
                ThumbnailSearchName = "OBRAS_RENDER",
            },
            new DefaultMapDef
            {
                SceneGuid   = "1354e600b46f3c3488593866c2ca43b3",   // MountainVillageMap.unity
                DisplayName = "Menu.Pueblo",
                CatalogId   = 2,
                ThumbnailSearchName = "PUEBLO_SCREENSHOT",
            },
            // New maps go here, with the next CatalogId.
        };

        private const string NETWORKING_COMMONS_GUID = "8096a6240cc47914d94ecd79c8f0e955";

        private struct DefaultMapDef
        {
            public string SceneGuid;
            public string DisplayName;
            public int    CatalogId;
            public string ThumbnailSearchName;   // partial name searched in Assets/FastAndFractured/MapSelection/Sprites
        }

        private MapCatalog       _catalog;
        private GameObject       _networkingCommons;
        private Vector2          _scroll;
        private List<SceneInfo>  _scenes     = new List<SceneInfo>();
        private bool             _showFilters = true;

        private bool _showRegistered   = true;
        private bool _showUnregistered = true;
        private bool _showMenuScenes   = false;

        private static readonly HashSet<string> IGNORED_KEYWORDS = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            "MainMenu", "Menu", "Lobby", "Bootstrap", "Manager",
            "MultiplayerRoom", "Test", "Loading", "Splash",
        };

        private struct SceneInfo
        {
            public string Path;
            public string Name;
            public string Guid;
            public bool   InBuildSettings;
            public int    BuildIndex;
            public bool   InCatalog;
            public int    CatalogId;
            public bool   IsDefault;      // present in DEFAULT_MAPS
            public ValidationStatus Status;
            public string StatusDetail;

            public bool   IsExpanded;
            public int    PendingId;
            public string PendingDisplayName;
            public Sprite PendingThumbnail;
        }

        private enum ValidationStatus { Unknown, OK, Warning, Error }

        [MenuItem("Tools/Fast And Fractured/Map Registration Tool", priority = 13)]
        public static void ShowWindow()
        {
            var w = GetWindow<NetworkedMapRegistrationTool>("Map Registration");
            w.minSize = MIN_WINDOW_SIZE;
            w.Refresh();
            w.Show();
        }

        private void OnEnable() => Refresh();

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(SPACING);
            DrawMultiplayerWorkflowInfo();
            EditorGUILayout.Space(SPACING);
            DrawNetworkingCommonsSection();
            EditorGUILayout.Space(SPACING);
            DrawDefaultsSection();
            EditorGUILayout.Space(SPACING);
            DrawCatalogStatus();
            EditorGUILayout.Space(SPACING);
            DrawFilters();
            EditorGUILayout.Space(SPACING);
            DrawSceneList();
        }

        private void DrawMultiplayerWorkflowInfo()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("How to make a map multiplayer-ready", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "No scene modification is needed.  Existing singleplayer scenes work directly in multiplayer.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    "FishNet SceneManager loads the map scene.  NetworkingCommons (NetworkManager, etc.) " +
                    "persists via DontDestroyOnLoad from the lobby — it does NOT live inside the map scene.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(SMALL_SPACING);
                EditorGUILayout.LabelField("  1.  Register the map in MapCatalog (use the section below).", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  2.  Add it to Build Settings (button inside each row, or the bulk button below).", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  3.  That's it — no prefab swaps, no scene duplication.", EditorStyles.miniLabel);
                EditorGUILayout.Space(SMALL_SPACING);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add All Registered Maps to Build Settings", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        int added = 0;
                        foreach (var s in _scenes)
                        {
                            if (s.InCatalog && !s.InBuildSettings)
                            {
                                AddToBuildSettings(s.Path);
                                added++;
                            }
                        }
                        if (added > 0) Refresh();
                        else EditorUtility.DisplayDialog("Build Settings", "All registered maps are already in Build Settings.", "OK");
                    }
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Map Registration Tool", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(SMALL_BUTTON_WIDTH)))
                    Refresh();
            }
        }

        private void DrawNetworkingCommonsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Networking Infrastructure", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("NetworkingCommons prefab",
                        _networkingCommons, typeof(GameObject), false);
                    EditorGUI.EndDisabledGroup();

                    if (_networkingCommons == null)
                    {
                        var prev = GUI.color;
                        GUI.color = Color.red;
                        GUILayout.Label("NOT FOUND", EditorStyles.boldLabel, GUILayout.Width(90));
                        GUI.color = prev;
                    }
                    else
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                            Selection.activeObject = _networkingCommons;
                    }
                }

                EditorGUILayout.LabelField(
                    "This prefab must be present in every game scene for multiplayer to function.",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawDefaultsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Default Maps (defined by GUID)", EditorStyles.boldLabel);

                bool allRegistered = true;
                foreach (var def in DEFAULT_MAPS)
                {
                    string path = AssetDatabase.GUIDToAssetPath(def.SceneGuid);
                    bool found      = !string.IsNullOrEmpty(path);
                    bool registered = _catalog != null &&
                                      _catalog.Entries.Any(e =>
                                          e.SceneName == System.IO.Path.GetFileNameWithoutExtension(path));

                    if (!registered) allRegistered = false;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var prevColor = GUI.color;
                        GUI.color = registered ? Color.green : (found ? Color.yellow : Color.red);
                        GUILayout.Label(registered ? "✓" : (found ? "○" : "✗"), GUILayout.Width(ICON_WIDTH));
                        GUI.color = prevColor;

                        GUILayout.Label($"[{def.CatalogId}] {def.DisplayName}", GUILayout.ExpandWidth(true));

                        if (!found)
                        {
                            GUILayout.Label($"GUID not found: {def.SceneGuid}", EditorStyles.miniLabel);
                        }
                        else if (!registered)
                        {
                            GUILayout.Label(System.IO.Path.GetFileNameWithoutExtension(path),
                                EditorStyles.miniLabel, GUILayout.Width(160));
                            if (GUILayout.Button("Register", GUILayout.Width(SMALL_BUTTON_WIDTH)))
                            {
                                RegisterFromDefault(def, path);
                                Refresh();
                                GUIUtility.ExitGUI();
                            }
                        }
                        else
                        {
                            GUILayout.Label("In catalog", EditorStyles.miniLabel, GUILayout.Width(SMALL_BUTTON_WIDTH));
                        }
                    }
                }

                EditorGUILayout.Space(SPACING);
                using (new EditorGUI.DisabledScope(allRegistered || _catalog == null && false))
                {
                    if (GUILayout.Button("Register All Missing Defaults", GUILayout.Height(LARGE_BUTTON_HEIGHT)))
                    {
                        foreach (var def in DEFAULT_MAPS)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(def.SceneGuid);
                            if (string.IsNullOrEmpty(path)) continue;
                            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
                            bool already = _catalog != null && _catalog.Entries.Any(e => e.SceneName == sceneName);
                            if (!already) RegisterFromDefault(def, path);
                        }
                        Refresh();
                    }
                }

                EditorGUILayout.LabelField(
                    "Add new maps to the DEFAULT_MAPS array in NetworkedMapRegistrationTool.cs.",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawCatalogStatus()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_catalog == null)
                {
                    EditorGUILayout.HelpBox(
                        "MapCatalog.asset not found. Run Lobby Setup Wizard first, or click Create below.",
                        MessageType.Warning);
                    if (GUILayout.Button("Create MapCatalog.asset", GUILayout.Height(LARGE_BUTTON_HEIGHT)))
                    {
                        CreateCatalog();
                        Refresh();
                    }
                }
                else
                {
                    int reg = _scenes.Count(s => s.InCatalog);
                    EditorGUILayout.LabelField(
                        $"MapCatalog: {_catalog.Entries.Count} entries  |  Scenes found: {_scenes.Count}  |  Registered: {reg}",
                        EditorStyles.miniLabel);

                    if (GUILayout.Button("Select MapCatalog.asset", GUILayout.Height(22)))
                        Selection.activeObject = _catalog;
                }
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showRegistered   = GUILayout.Toggle(_showRegistered,   "Registered",             GUILayout.Width(100));
                _showUnregistered = GUILayout.Toggle(_showUnregistered, "Unregistered",            GUILayout.Width(110));
                _showMenuScenes   = GUILayout.Toggle(_showMenuScenes,   "Show Menu / Test scenes", GUILayout.Width(170));
            }
        }

        private void DrawSceneList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var info in _scenes)
            {
                bool isMenu = IGNORED_KEYWORDS.Any(k =>
                    info.Name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (isMenu && !_showMenuScenes)   continue;
                if (info.InCatalog  && !_showRegistered)   continue;
                if (!info.InCatalog && !_showUnregistered) continue;

                DrawSceneRow(info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneRow(SceneInfo info)
        {
            int idx = _scenes.FindIndex(s => s.Path == info.Path);

            Color bg = info.InCatalog
                ? ROW_REGISTERED_COLOR
                : info.IsDefault
                    ? ROW_DEFAULT_COLOR
                    : ROW_OTHER_COLOR;

            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, bg);

            using (new EditorGUILayout.HorizontalScope())
            {
                string icon = info.Status switch
                {
                    ValidationStatus.OK      => "✓",
                    ValidationStatus.Warning => "⚠",
                    ValidationStatus.Error   => "✗",
                    _                        => "?",
                };
                Color iconColor = info.Status switch
                {
                    ValidationStatus.OK      => Color.green,
                    ValidationStatus.Warning => Color.yellow,
                    ValidationStatus.Error   => Color.red,
                    _                        => Color.gray,
                };
                var prev = GUI.color;
                GUI.color = iconColor;
                GUILayout.Label(icon, GUILayout.Width(ICON_WIDTH));
                GUI.color = prev;

                GUILayout.Label(info.Name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

                if (info.IsDefault && !info.InCatalog)
                {
                    GUI.color = Color.yellow;
                    GUILayout.Label("[DEFAULT]", EditorStyles.miniLabel, GUILayout.Width(64));
                    GUI.color = prev;
                }

                if (info.InBuildSettings)
                    GUILayout.Label($"[Build #{info.BuildIndex}]", EditorStyles.miniLabel, GUILayout.Width(78));
                else
                {
                    GUI.color = Color.yellow;
                    GUILayout.Label("[Not in Build]", EditorStyles.miniLabel, GUILayout.Width(88));
                    GUI.color = prev;
                }

                if (info.InCatalog)
                {
                    GUILayout.Label($"[ID {info.CatalogId}]", EditorStyles.miniLabel, GUILayout.Width(56));
                    if (!info.InBuildSettings)
                    {
                        var prev2 = GUI.color;
                        GUI.color = Color.yellow;
                        if (GUILayout.Button("Add to Build", GUILayout.Width(ROW_BUTTON_WIDTH)))
                        {
                            AddToBuildSettings(info.Path);
                            Refresh();
                            GUIUtility.ExitGUI();
                        }
                        GUI.color = prev2;
                    }
                    if (GUILayout.Button("Unregister", GUILayout.Width(ROW_BUTTON_WIDTH)))
                    {
                        UnregisterScene(info.Name);
                        Refresh();
                        GUIUtility.ExitGUI();
                    }
                }
                else
                {
                    if (GUILayout.Button("▼ Register", GUILayout.Width(ROW_BUTTON_WIDTH)))
                    {
                        var updated = _scenes[idx];
                        updated.IsExpanded = !updated.IsExpanded;
                        if (updated.IsExpanded)
                        {
                            updated.PendingDisplayName = info.Name;
                            updated.PendingId          = NextFreeId();
                        }
                        _scenes[idx] = updated;
                    }
                }
            }

            if (!string.IsNullOrEmpty(info.StatusDetail))
                EditorGUILayout.LabelField($"  {info.StatusDetail}", EditorStyles.miniLabel);

            if (!info.InCatalog && idx >= 0 && _scenes[idx].IsExpanded)
            {
                EditorGUILayout.Space(SPACING);
                EditorGUI.indentLevel++;
                var s = _scenes[idx];

                bool idConflict = _catalog != null && _catalog.Entries.Any(e => e.Id == s.PendingId);

                s.PendingId          = EditorGUILayout.IntField("Catalog ID (stable)", s.PendingId);
                if (idConflict)
                    EditorGUILayout.HelpBox($"ID {s.PendingId} is already used.", MessageType.Error);

                s.PendingDisplayName = EditorGUILayout.TextField("Display Name", s.PendingDisplayName);
                s.PendingThumbnail   = (Sprite)EditorGUILayout.ObjectField(
                    "Thumbnail", s.PendingThumbnail, typeof(Sprite), false);

                EditorGUILayout.Space(SPACING);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!info.InBuildSettings && GUILayout.Button("Add to Build Settings", GUILayout.Height(BUTTON_HEIGHT)))
                    {
                        AddToBuildSettings(info.Path);
                        Refresh();
                    }

                    bool canRegister = !idConflict && !string.IsNullOrWhiteSpace(s.PendingDisplayName);
                    using (new EditorGUI.DisabledScope(!canRegister))
                    {
                        if (GUILayout.Button("Confirm Registration", GUILayout.Height(BUTTON_HEIGHT)))
                        {
                            RegisterScene(info.Name, s.PendingId, s.PendingDisplayName, s.PendingThumbnail);
                            Refresh();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                EditorGUI.indentLevel--;
                _scenes[idx] = s;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(SMALL_SPACING);
        }

        private void Refresh()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG_RESOURCE_PATH);

            string ncPath = AssetDatabase.GUIDToAssetPath(NETWORKING_COMMONS_GUID);
            _networkingCommons = string.IsNullOrEmpty(ncPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(ncPath);

            var defaultGuids = new HashSet<string>(DEFAULT_MAPS.Select(d => d.SceneGuid));

            var prev          = _scenes.Where(s => s.IsExpanded).Select(s => s.Path).ToHashSet();
            _scenes.Clear();

            var buildScenes = EditorBuildSettings.scenes;
            var guids       = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                int  buildIndex = -1;
                bool inBuild    = false;
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    if (buildScenes[i].path == path) { inBuild = true; buildIndex = i; break; }
                }

                bool inCatalog = false;
                int  catalogId = -1;
                if (_catalog != null)
                {
                    foreach (var e in _catalog.Entries)
                    {
                        if (e.SceneName == name) { inCatalog = true; catalogId = e.Id; break; }
                    }
                }

                var (status, detail) = QuickValidate(path);

                _scenes.Add(new SceneInfo
                {
                    Path             = path,
                    Name             = name,
                    Guid             = guid,
                    InBuildSettings  = inBuild,
                    BuildIndex       = buildIndex,
                    InCatalog        = inCatalog,
                    CatalogId        = catalogId,
                    IsDefault        = defaultGuids.Contains(guid),
                    Status           = status,
                    StatusDetail     = detail,
                    IsExpanded       = prev.Contains(path),
                    PendingId        = NextFreeId(),
                    PendingDisplayName = name,
                });
            }

            // FindAssets can miss scenes: add any default map it did not return.
            var foundPaths = new HashSet<string>(_scenes.Select(s => s.Path), System.StringComparer.OrdinalIgnoreCase);
            foreach (var def in DEFAULT_MAPS)
            {
                string defPath = AssetDatabase.GUIDToAssetPath(def.SceneGuid);
                if (string.IsNullOrEmpty(defPath) || foundPaths.Contains(defPath)) continue;

                string defName = System.IO.Path.GetFileNameWithoutExtension(defPath);

                int  defBuildIndex = -1;
                bool defInBuild    = false;
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    if (buildScenes[i].path == defPath) { defInBuild = true; defBuildIndex = i; break; }
                }

                bool defInCatalog = false;
                int  defCatalogId = -1;
                if (_catalog != null)
                {
                    foreach (var e in _catalog.Entries)
                    {
                        if (e.SceneName == defName) { defInCatalog = true; defCatalogId = e.Id; break; }
                    }
                }

                var (defStatus, defDetail) = QuickValidate(defPath);

                _scenes.Add(new SceneInfo
                {
                    Path              = defPath,
                    Name              = defName,
                    Guid              = def.SceneGuid,
                    InBuildSettings   = defInBuild,
                    BuildIndex        = defBuildIndex,
                    InCatalog         = defInCatalog,
                    CatalogId         = defCatalogId,
                    IsDefault         = true,
                    Status            = defStatus,
                    StatusDetail      = defDetail,
                    IsExpanded        = prev.Contains(defPath),
                    PendingId         = NextFreeId(),
                    PendingDisplayName = def.DisplayName,
                });
            }

            _scenes.Sort((a, b) =>
            {
                if (a.IsDefault != b.IsDefault) return a.IsDefault ? -1 : 1;
                if (a.InCatalog != b.InCatalog) return a.InCatalog ? -1 : 1;
                return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            });

            Repaint();
        }

        // Scene YAML references MonoBehaviours by script GUID, not class name.
        private const string GUID_LEVEL_CONTROLLER_SCRIPT  = "a0ca2c67c9478114d8d628ab87d87e56";

        private static (ValidationStatus, string) QuickValidate(string path)
        {
            if (SceneManager.GetActiveScene().path == path)
                return ValidateActiveScene();

            // Unloaded scenes: search the raw YAML for the script GUID.
            try
            {
                string text = System.IO.File.ReadAllText(path);
                bool hasLC = text.Contains(GUID_LEVEL_CONTROLLER_SCRIPT);
                bool hasSP = text.Contains("SpawnPoint");
                bool hasNM = text.Contains("NetworkManager");

                if (!hasLC && !hasSP)
                    return (ValidationStatus.Warning, "No LevelController or SpawnPoints detected.");
                if (!hasLC)
                    return (ValidationStatus.Warning, "No LevelController detected.");
                if (!hasSP)
                    return (ValidationStatus.Warning, $"LevelController ✓  No SpawnPoints detected.{(hasNM ? "  NetworkManager ✓" : "")}");

                return (ValidationStatus.OK,
                    $"LevelController ✓  SpawnPoints ✓{(hasNM ? "  NetworkManager ✓" : "")}");
            }
            catch
            {
                return (ValidationStatus.Unknown, "Could not read scene file.");
            }
        }

        private static (ValidationStatus, string) ValidateActiveScene()
        {
            var lc = Object.FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
            int sp = GameObject.FindGameObjectsWithTag("SpawnPoint")?.Length ?? 0;
            if (sp == 0)
            {
                sp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                           .Count(t => t.name.Contains("SpawnPoint"));
            }

            if (lc == null)
                return (ValidationStatus.Warning, $"No LevelController.  SpawnPoints: {sp}");
            if (sp == 0)
                return (ValidationStatus.Warning, "LevelController ✓  No SpawnPoints found!");

            return (ValidationStatus.OK, $"LevelController ✓  SpawnPoints: {sp}");
        }

        private void RegisterFromDefault(DefaultMapDef def, string scenePath)
        {
            if (_catalog == null) { CreateCatalog(); _catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG_RESOURCE_PATH); }

            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (!EditorBuildSettings.scenes.Any(s => s.path == scenePath))
                AddToBuildSettings(scenePath);

            Sprite thumbnail = FindSprite(def.ThumbnailSearchName);

            RegisterScene(sceneName, def.CatalogId, def.DisplayName, thumbnail);
        }

        private void RegisterScene(string sceneName, int id, string displayName, Sprite thumbnail)
        {
            if (_catalog == null) CreateCatalog();
            _catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG_RESOURCE_PATH);

            var so      = new SerializedObject(_catalog);
            var entries = so.FindProperty("_entries");

            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i)
                           .FindPropertyRelative("SceneName").stringValue == sceneName) return;
            }

            int newIdx = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIdx);
            var elem = entries.GetArrayElementAtIndex(newIdx);
            elem.FindPropertyRelative("Id").intValue                        = id;
            elem.FindPropertyRelative("DisplayNameKey").stringValue         = displayName;
            elem.FindPropertyRelative("SceneName").stringValue              = sceneName;
            elem.FindPropertyRelative("Thumbnail").objectReferenceValue     = thumbnail;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapRegistration] Registered '{sceneName}' as ID {id} ('{displayName}').");
        }

        private void UnregisterScene(string sceneName)
        {
            if (_catalog == null) return;

            var so      = new SerializedObject(_catalog);
            var entries = so.FindProperty("_entries");

            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i)
                           .FindPropertyRelative("SceneName").stringValue == sceneName)
                {
                    entries.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[MapRegistration] Unregistered '{sceneName}'.");
                    return;
                }
            }
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == scenePath)) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[MapRegistration] Added '{scenePath}' to Build Settings (index {list.Count - 1}).");
        }

        private static Sprite FindSprite(string searchName)
        {
            if (string.IsNullOrEmpty(searchName)) return null;
            string[] guids = AssetDatabase.FindAssets(
                $"{searchName} t:Sprite",
                new[] { "Assets/FastAndFractured/MapSelection/Sprites" });
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void CreateCatalog()
        {
            if (!AssetDatabase.IsValidFolder(RESOURCES_DIR))
            {
                string parent = System.IO.Path.GetDirectoryName(RESOURCES_DIR);
                AssetDatabase.CreateFolder(parent, "Resources");
            }
            var asset = ScriptableObject.CreateInstance<MapCatalog>();
            AssetDatabase.CreateAsset(asset, CATALOG_RESOURCE_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapRegistration] Created {CATALOG_RESOURCE_PATH}");
        }

        private int NextFreeId()
        {
            if (_catalog == null || _catalog.Entries.Count == 0) return 0;
            int maxDefault = DEFAULT_MAPS.Length > 0 ? DEFAULT_MAPS.Max(d => d.CatalogId) : -1;
            int maxCatalog = _catalog.Entries.Max(e => e.Id);
            return Mathf.Max(maxDefault, maxCatalog) + 1;
        }
    }
}
