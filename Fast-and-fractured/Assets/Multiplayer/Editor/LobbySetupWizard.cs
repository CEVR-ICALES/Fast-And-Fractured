using System.Collections.Generic;
using System.IO;
using Enums;
using FastAndFractured;
using FastAndFractured.Multiplayer;
using FastAndFractured.Multiplayer.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// One-click lobby setup wizard.
    /// Open via: Tools > Fast And Fractured > Lobby Setup Wizard
    ///
    /// What it does:
    ///   1. Creates MapCatalog.asset (Resources/MapCatalog) with 3 maps pre-filled.
    ///   2. In the currently open scene, creates the 6 new MenuScreen GameObjects
    ///      (MULTIPLAYER_HUB, MULTIPLAYER_HOST_SETUP, MULTIPLAYER_BROWSER,
    ///       MULTIPLAYER_DIRECT_JOIN, LOBBY_ROOM, LOBBY_MAP_VOTE) under the same
    ///       parent as the existing MenuScreen objects.
    ///   3. Attaches the matching controller MonoBehaviour to each new screen.
    ///   4. Saves the scene.
    ///
    /// REQUIRES: A main menu scene with at least one MenuScreen component must be open.
    /// </summary>
    public class LobbySetupWizard : EditorWindow
    {
        private const string CATALOG_RESOURCE_PATH = "Assets/Multiplayer/Lobby/Resources/MapCatalog.asset";
        private const string RESOURCES_DIR          = "Assets/Multiplayer/Lobby/Resources";

        private static readonly (ScreensType type, string label, System.Type controller)[] NEW_SCREENS =
        {
            (ScreensType.MULTIPLAYER_HUB,         "Multiplayer Hub",     typeof(MultiplayerHubController)),
            (ScreensType.MULTIPLAYER_HOST_SETUP,   "Host Setup",          typeof(MultiplayerHostSetupController)),
            (ScreensType.MULTIPLAYER_BROWSER,      "Lobby Browser",       typeof(LobbyBrowserController)),
            (ScreensType.MULTIPLAYER_DIRECT_JOIN,  "Direct Join",         typeof(MultiplayerDirectJoinController)),
            (ScreensType.LOBBY_ROOM,               "Lobby Room",          null),   // multiple controllers
            (ScreensType.LOBBY_MAP_VOTE,           "Lobby Map Vote",      null),
        };

        // Wizard state
        private Vector2 _scroll;
        private string  _log = string.Empty;

        [MenuItem("Tools/Fast And Fractured/Lobby Setup Wizard", priority = 10)]
        public static void ShowWindow()
        {
            var w = GetWindow<LobbySetupWizard>("Lobby Setup Wizard");
            w.minSize = new Vector2(480f, 500f);
            w.Show();
        }

        // Foldout state for the advanced individual-action buttons.
        private bool _showAdvanced;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Lobby Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open the main menu scene, then click the big button below. The full setup is " +
                "idempotent — running it again only fixes drift, it won't duplicate anything.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            DrawStatusSection();

            EditorGUILayout.Space(10f);

            // ── The single button that does everything ─────────────────────────
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("SETUP EVERYTHING  (idempotent)", GUILayout.Height(56f)))
                RunFullSetup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox(
                "Runs in order:\n" +
                "  1. Create/update MapCatalog (Resources/MapCatalog.asset).\n" +
                "  2. Generate LobbyBrowserEntry + PlayerSlotEntry prefabs.\n" +
                "  3. DELETE old lobby screens and recreate them with all buttons wired.\n" +
                "  4. Add the MULTIPLAYER button to MAIN_MENU if missing.\n" +
                "  5. Create MultiplayerHubCamera and wire to every multiplayer screen.\n" +
                "  6. Wire singleplayer screen cameras (CharSlectionCamera → CHARACTER_SELECTION, etc.).\n" +
                "  7. Set up dynamic LobbyManager spawn — generates the prefab, adds bootstrapper, removes stale scene-placed instances.\n" +
                "  8. Save the scene.",
                MessageType.None);

            EditorGUILayout.Space(8f);

            // Diagnostic — always visible (no foldout) because it's the first thing you want when
            // setup looks fine but the runtime UI is broken.
            GUI.backgroundColor = new Color(0.95f, 0.85f, 0.3f);
            if (GUILayout.Button("DIAGNOSE Lobby Wiring (inspect current scene)", GUILayout.Height(28f)))
                DiagnoseLobbyWiring();
            GUI.backgroundColor = Color.white;

            // Quick visual sanity check — turns ON every multiplayer screen at edit time so you
            // can SEE whether they actually render. Click HOST_SETUP/HUB/etc. in the hierarchy
            // afterwards to verify children.
            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.95f);
            if (GUILayout.Button("FORCE SHOW all multiplayer screens (edit-time visual check)", GUILayout.Height(24f)))
                ForceShowAllMultiplayerScreens();
            GUI.backgroundColor = Color.white;

            // FishNet reserialize — fixes the "NetworkObject ObjectId [65535] not initialized"
            // errors that flood the console for TNT/MegaChicken/etc. when their NetworkBehaviours
            // list got out of sync (typical after script changes that affect NetworkBehaviour).
            GUI.backgroundColor = new Color(0.95f, 0.5f, 0.3f);
            if (GUILayout.Button("Reserialize NetworkObjects (Prefabs + Open Scenes)", GUILayout.Height(24f)))
                ReserializeNetworkObjects();
            GUI.backgroundColor = Color.white;

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced (run individual steps)", true);
            if (_showAdvanced)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button("Create / Update MapCatalog", GUILayout.Height(24f)))
                        CreateOrUpdateMapCatalog();
                    if (GUILayout.Button("Generate Lobby Browser Entry Prefab", GUILayout.Height(24f)))
                    {
                        LobbyBrowserEntryPrefabGenerator.Generate();
                        Log("LobbyBrowserEntry prefab generated.");
                    }
                    if (GUILayout.Button("Generate Player Slot Entry Prefab", GUILayout.Height(24f)))
                    {
                        PlayerSlotEntryPrefabGenerator.Generate();
                        Log("PlayerSlotEntry prefab generated.");
                    }
                    if (GUILayout.Button("Drop NetworkingCommons prefab into scene", GUILayout.Height(24f)))
                    {
                        EnsureNetworkingCommonsInScene();
                        EditorSceneManager.MarkAllScenesDirty();
                        EditorSceneManager.SaveOpenScenes();
                    }
                    if (GUILayout.Button("Refresh FishNet SceneIds (fix client-can't-see-lobby)", GUILayout.Height(24f)))
                    {
                        RefreshFishNetSceneIds();
                        EditorSceneManager.MarkAllScenesDirty();
                        EditorSceneManager.SaveOpenScenes();
                    }
                    if (GUILayout.Button("DELETE & Recreate Lobby Screens", GUILayout.Height(24f)))
                        DeleteAndRecreateLobbyScreens();
                    if (GUILayout.Button("Fix Missing CanvasGroup", GUILayout.Height(24f)))
                        RepairCanvasGroups();
                    if (GUILayout.Button("Add MULTIPLAYER Button to Main Menu", GUILayout.Height(24f)))
                        AddMultiplayerButtonToMainMenu();
                    if (GUILayout.Button("Setup Multiplayer Hub Camera (all screens)", GUILayout.Height(24f)))
                        CreateMultiplayerHubCamera();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // Canonical pose for MultiplayerHubCamera. All multiplayer screens share this single camera
        // so transitioning between HUB → HOST_SETUP → BROWSER etc. doesn't move the camera.
        private static readonly Vector3    HUB_CAMERA_POSITION = new Vector3(62.5f, 4.1f, 62.3f);
        private static readonly Quaternion HUB_CAMERA_ROTATION = Quaternion.Euler(0f, 91.27f, 0f);

        // Every screen that should share the hub camera.
        private static readonly ScreensType[] MULTIPLAYER_SCREENS =
        {
            ScreensType.MULTIPLAYER_HUB,
            ScreensType.MULTIPLAYER_HOST_SETUP,
            ScreensType.MULTIPLAYER_BROWSER,
            ScreensType.MULTIPLAYER_DIRECT_JOIN,
            ScreensType.LOBBY_ROOM,
            ScreensType.LOBBY_MAP_VOTE,
        };

        private void CreateMultiplayerHubCamera()
        {
            // 1. Find every multiplayer MenuScreen present in the scene.
            var screensByType = new Dictionary<ScreensType, MenuScreen>();
            foreach (var ms in FindObjectsOfType<MenuScreen>(true))
            {
                if (System.Array.IndexOf(MULTIPLAYER_SCREENS, ms.screenType) < 0) continue;
                screensByType[ms.screenType] = ms;
            }

            if (!screensByType.ContainsKey(ScreensType.MULTIPLAYER_HUB))
            {
                Log("ERROR: MULTIPLAYER_HUB screen not found. Run 'Delete & Recreate Lobby Screens' first.");
                return;
            }

            // 2. Locate an existing MultiplayerHubCamera, otherwise create one (cloning InitialCamera
            //    so we inherit the Cinemachine setup the rest of the menu uses).
            GameObject camGO = FindMultiplayerHubCamera(screensByType.Values);
            bool created = false;
            if (camGO == null)
            {
                var initCam = GameObject.Find("InitialCamera")
                              ?? GameObject.Find("GameModeScreenCamera")
                              ?? GameObject.Find("CharSlectionCamera");
                if (initCam != null)
                {
                    camGO = Object.Instantiate(initCam);
                    camGO.name = "MultiplayerHubCamera";
                    Log($"Cloned '{initCam.name}' as MultiplayerHubCamera.");
                }
                else
                {
                    camGO = new GameObject("MultiplayerHubCamera");
                    Log("WARNING: Could not find InitialCamera to clone. Created empty GO — add CinemachineVirtualCamera manually.");
                }
                created = true;
            }
            else
            {
                Log($"Reusing existing camera '{camGO.name}'.");
            }

            // 3. Snap to the canonical pose every time, regardless of whether it was reused or created.
            //    This is the source of truth — the user's reported coordinates.
            Undo.RecordObject(camGO.transform, "Set MultiplayerHubCamera pose");
            camGO.transform.SetParent(null, worldPositionStays: false);
            camGO.transform.position   = HUB_CAMERA_POSITION;
            camGO.transform.rotation   = HUB_CAMERA_ROTATION;
            camGO.transform.localScale = Vector3.one;

            // 4. Bump Cinemachine priority above InitialCamera so it wins the blend when active.
            var allBehaviours = camGO.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in allBehaviours)
            {
                var so_cam = new SerializedObject(mb);
                var legacyPrio = so_cam.FindProperty("m_LegacyPriority");
                if (legacyPrio != null)
                {
                    if (legacyPrio.intValue < 15) legacyPrio.intValue = 15;
                    so_cam.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mb);
                    break;
                }
            }

            // Starts inactive — MainMenuManager activates it when its screen becomes current.
            camGO.SetActive(false);

            // 5. Wire it onto every multiplayer screen we found.
            int wired = 0, alreadyWired = 0, redirected = 0;
            foreach (var st in MULTIPLAYER_SCREENS)
            {
                if (!screensByType.TryGetValue(st, out var screen))
                {
                    Log($"  skip {st} — screen not in scene");
                    continue;
                }

                var so = new SerializedObject(screen);
                var prop = so.FindProperty("screenCamera");
                if (prop == null)
                {
                    Log($"  ERROR  {st}: 'screenCamera' property missing on MenuScreen script.");
                    continue;
                }

                var previous = prop.objectReferenceValue as GameObject;
                if (previous == camGO) { alreadyWired++; continue; }

                if (previous != null && previous != camGO)
                {
                    // The screen was pointing at a different camera. Disable it so it doesn't
                    // interfere — Cinemachine blending would otherwise compete with the hub cam.
                    previous.SetActive(false);
                    redirected++;
                    Log($"  redirect {st}: was '{previous.name}', now MultiplayerHubCamera");
                }

                prop.objectReferenceValue = camGO;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(screen);
                wired++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Log($"Done. Camera {(created ? "created" : "reused")} at {HUB_CAMERA_POSITION} rotY={HUB_CAMERA_ROTATION.eulerAngles.y:F2}.  " +
                $"Wired to {wired} screen(s)  |  already wired: {alreadyWired}  |  redirected from other cam: {redirected}");
        }

        /// <summary>
        /// Returns the canonical MultiplayerHubCamera:
        ///   - first GO named exactly "MultiplayerHubCamera" anywhere in the scene
        ///   - or any non-null camera currently wired into one of the multiplayer screens
        /// </summary>
        private static GameObject FindMultiplayerHubCamera(IEnumerable<MenuScreen> multiplayerScreens)
        {
            // Direct name lookup first — handles inactive objects.
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null) continue;
                if (t.hideFlags != HideFlags.None) continue;
                if (!t.gameObject.scene.IsValid()) continue;          // skip prefab assets
                if (t.name == "MultiplayerHubCamera") return t.gameObject;
            }

            // Fallback: take whatever any multiplayer screen is already pointing at.
            foreach (var ms in multiplayerScreens)
            {
                if (ms != null && ms.screenCamera != null) return ms.screenCamera;
            }

            return null;
        }

        private void DeleteAndRecreateLobbyScreens()
        {
            var lobbyTypes = new HashSet<ScreensType>();
            foreach (var (t, _, _) in NEW_SCREENS) lobbyTypes.Add(t);

            int deleted = 0;
            foreach (var ms in FindObjectsOfType<MenuScreen>(true))
            {
                if (!lobbyTypes.Contains(ms.screenType)) continue;
                Log($"  DELETE  {ms.gameObject.name}");
                DestroyImmediate(ms.gameObject);
                deleted++;
            }

            Log($"Deleted {deleted} lobby screen(s). Recreating...");
            CreateMenuScreens();
            RepairCanvasGroups();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Log("Done. Scene saved.");
        }

        private void AddMultiplayerButtonToMainMenu()
        {
            // Find the MAIN_MENU screen
            MenuScreen mainMenuScreen = null;
            foreach (var ms in FindObjectsOfType<MenuScreen>(true))
            {
                if (ms.screenType == ScreensType.MAIN_MENU)
                {
                    mainMenuScreen = ms;
                    break;
                }
            }

            if (mainMenuScreen == null)
            {
                Log("ERROR: No MenuScreen with type MAIN_MENU found in scene.");
                return;
            }

            // Ensure all lobby screens exist in this scene first
            CreateMenuScreens();

            // Check if a Multiplayer button already exists
            foreach (var mtb in mainMenuScreen.GetComponentsInChildren<MenuTransitionButton>(true))
            {
                if (mtb.screenToOpen == ScreensType.MULTIPLAYER_HUB)
                {
                    Log("Multiplayer button already exists in MAIN_MENU — skipped.");
                    return;
                }
            }

            // Find all existing MenuTransitionButtons to place the new button relative to them
            var existingButtons = mainMenuScreen.GetComponentsInChildren<MenuTransitionButton>(true);

            // Build the new button GO
            var btnGO = new GameObject("MultiplayerButton");
            btnGO.transform.SetParent(mainMenuScreen.transform, false);

            var rt = btnGO.AddComponent<RectTransform>();

            if (existingButtons.Length > 0)
            {
                // Place as a sibling: copy anchors from first button, then shift it to top (first in order)
                var firstBtnRT = existingButtons[0].GetComponent<RectTransform>();
                rt.anchorMin  = firstBtnRT.anchorMin;
                rt.anchorMax  = firstBtnRT.anchorMax;
                rt.offsetMin  = firstBtnRT.offsetMin;
                rt.offsetMax  = firstBtnRT.offsetMax;
                rt.pivot      = firstBtnRT.pivot;

                // Move the new button to be BEFORE the first existing button in the hierarchy
                btnGO.transform.SetSiblingIndex(existingButtons[0].transform.GetSiblingIndex());
            }
            else
            {
                // Fallback: center-left position
                rt.anchorMin = new Vector2(0.1f, 0.6f);
                rt.anchorMax = new Vector2(0.4f, 0.7f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }

            // Image + Button
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.1f, 0.45f, 0.85f, 1f);
            btnGO.AddComponent<Button>();

            // Label
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "MULTIPLAYER";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize  = 24f;
            tmp.color     = Color.white;

            // MenuTransitionButton wired to MULTIPLAYER_HUB
            var mtbComp = btnGO.AddComponent<MenuTransitionButton>();
            var so = new SerializedObject(mtbComp);
            so.FindProperty("screenToOpen").enumValueIndex = (int)ScreensType.MULTIPLAYER_HUB;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Wire the Button.onClick → MenuTransitionButton.OpenScreen via UnityEvent
            var button = btnGO.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick,
                mtbComp.OpenScreen);

            EditorUtility.SetDirty(mainMenuScreen.gameObject);
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Log($"Multiplayer button added to '{mainMenuScreen.gameObject.name}' → MULTIPLAYER_HUB. Scene saved.");
        }

        private void RepairCanvasGroups()
        {
            int fixed_ = 0;
            foreach (var ms in FindObjectsOfType<MenuScreen>(true))
            {
                var so = new SerializedObject(ms);
                var prop = so.FindProperty("canvasGroup");
                if (prop == null || prop.objectReferenceValue != null) continue;

                var cg = ms.GetComponent<CanvasGroup>();
                if (cg == null) cg = ms.gameObject.AddComponent<CanvasGroup>();

                prop.objectReferenceValue = cg;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ms);
                fixed_++;
                Log($"  FIXED canvasGroup on '{ms.gameObject.name}'");
            }

            if (fixed_ == 0)
                Log("No missing canvasGroup references found.");
            else
            {
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
                Log($"Repaired {fixed_} screen(s). Scene saved.");
            }
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool catalogOk = File.Exists(CATALOG_RESOURCE_PATH);
                DrawStatusRow("MapCatalog.asset", catalogOk);

                var existingScreens = FindAllMenuScreenTypes();
                foreach (var (type, label, _) in NEW_SCREENS)
                {
                    bool exists = existingScreens.Contains(type);
                    DrawStatusRow($"MenuScreen: {label} ({type})", exists);
                }

                bool hasMainMenuMgr = FindObjectOfType<MainMenuManager>() != null;
                DrawStatusRow("MainMenuManager in scene", hasMainMenuMgr);
            }
        }

        private static void DrawStatusRow(string label, bool ok)
        {
            var prev = GUI.color;
            GUI.color = ok ? Color.green : Color.red;
            EditorGUILayout.LabelField($"  {(ok ? "✓" : "✗")}  {label}");
            GUI.color = prev;
        }

        // ─────────────────────────────────────────────
        //  FULL SETUP
        // ─────────────────────────────────────────────
        /// <summary>
        /// Activates every multiplayer screen GameObject at edit time, sets their CanvasGroup
        /// alpha to 1, and reports the parent Canvas state. Lets the user EYEBALL whether the
        /// screens are rendering at all and where the buttons end up.
        /// </summary>
        private void ForceShowAllMultiplayerScreens()
        {
            _log = string.Empty;
            Log("=== Force-show multiplayer screens ===");

            var lobbyTypes = new HashSet<ScreensType>();
            foreach (var (t, _, _) in NEW_SCREENS) lobbyTypes.Add(t);

            Canvas parentCanvas = null;
            int shown = 0;
            foreach (var ms in FindObjectsOfType<MenuScreen>(true))
            {
                if (!lobbyTypes.Contains(ms.screenType)) continue;

                ms.gameObject.SetActive(true);
                var cg = ms.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }

                EditorUtility.SetDirty(ms.gameObject);
                shown++;

                if (parentCanvas == null) parentCanvas = ms.GetComponentInParent<Canvas>();
                Log($"  Forced visible: {ms.gameObject.name} ({ms.screenType})  children={ms.transform.childCount}");
            }

            Log($"\nTotal forced visible: {shown} screens.");

            if (parentCanvas != null)
            {
                Log($"\n── Parent Canvas state ──");
                Log($"  GameObject: '{parentCanvas.gameObject.name}'");
                Log($"  renderMode: {parentCanvas.renderMode}");
                Log($"  worldCamera: {(parentCanvas.worldCamera != null ? parentCanvas.worldCamera.name : "null")}");
                Log($"  sortingOrder: {parentCanvas.sortingOrder}");
                Log($"  enabled: {parentCanvas.enabled}");
                Log($"  gameObject.activeInHierarchy: {parentCanvas.gameObject.activeInHierarchy}");

                var scaler = parentCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    Log($"  CanvasScaler.uiScaleMode: {scaler.uiScaleMode}");
                    Log($"  CanvasScaler.referenceResolution: {scaler.referenceResolution}");
                }
            }
            else
            {
                Log("\nWARNING: could not find a parent Canvas. Are the screens parented under something other than a Canvas?");
            }

            Log("\n=== Done. Open Scene/Game view and look. ===");

            // Select the first screen so the user can inspect its hierarchy.
            foreach (var ms in FindObjectsOfType<MenuScreen>(true))
            {
                if (ms.screenType == ScreensType.MULTIPLAYER_HUB)
                {
                    Selection.activeGameObject = ms.gameObject;
                    break;
                }
            }

            SceneView.RepaintAll();
        }

        // ─────────────────────────────────────────────
        //  DIAGNOSIS
        // ─────────────────────────────────────────────
        /// <summary>
        /// Walks the multiplayer MenuScreens currently in the scene and reports the wiring state
        /// of every [SerializeField] on every controller it finds. Designed for the common case
        /// where "setup looks fine but nothing works at runtime" — the answer is almost always
        /// a null SerializeField the user can't see in the Inspector unless they expand it.
        /// </summary>
        private void DiagnoseLobbyWiring()
        {
            _log = string.Empty;
            Log("=== Lobby Wiring Diagnosis ===");
            Log($"Active scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");

            // 0. Network infrastructure — dynamic-spawn approach.
            Log("\n── Network infrastructure ──");
            var nm = Object.FindAnyObjectByType<FishNet.Managing.NetworkManager>();
            if (nm == null)
            {
                Log("  • NetworkManager in scene: MISSING ✗");
                Log("       ↳ Add a NetworkManager (e.g. via the NetworkingCommons prefab or your own setup).");
            }
            else
            {
                Log($"  • NetworkManager in scene: OK ('{nm.gameObject.name}')");

                var transport = nm.TransportManager?.Transport;
                Log($"  • Transport: {(transport != null ? transport.GetType().Name : "MISSING ✗ — server cannot start")}");

                // Bootstrapper drives dynamic spawn — its presence is the contract.
                var boot = nm.GetComponent<LobbyServerBootstrapper>();
                Log($"  • LobbyServerBootstrapper: {(boot != null ? "OK" : "MISSING ✗ — run SETUP EVERYTHING")}");

                // Spawnable prefab in Resources/.
                var prefabPath = LobbyManagerSpawnablePrefabGenerator.PREFAB_PATH;
                var spawnable  = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Log($"  • {prefabPath}: {(spawnable != null ? "OK" : "MISSING ✗ — run SETUP EVERYTHING")}");

                // Warn if there's STILL a scene-placed lobby manager (stale state).
                int sceneInstances = 0;
                foreach (var lm in Object.FindObjectsOfType<NetworkedLobbyManager>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabAsset(lm.gameObject)) sceneInstances++;
                }
                if (sceneInstances > 0)
                    Log($"  ⚠ {sceneInstances} stale scene-placed NetworkedLobbyManager(s) found — re-run SETUP EVERYTHING to remove them.");
            }

            // 1. Prefab assets that LobbyReadyUI / LobbyBrowserController reference.
            string slotPrefabPath  = "Assets/Multiplayer/Lobby/Prefabs/PlayerSlotEntry.prefab";
            string entryPrefabPath = "Assets/Multiplayer/Lobby/Prefabs/LobbyBrowserEntry.prefab";
            bool slotExists  = AssetDatabase.LoadAssetAtPath<GameObject>(slotPrefabPath)  != null;
            bool entryExists = AssetDatabase.LoadAssetAtPath<GameObject>(entryPrefabPath) != null;
            Log($"PlayerSlotEntry.prefab    : {(slotExists  ? "OK" : "MISSING — re-run SETUP")}");
            Log($"LobbyBrowserEntry.prefab  : {(entryExists ? "OK" : "MISSING — re-run SETUP")}");

            // 2. List every MenuScreen in the scene (not just multiplayer ones) so we know
            //    exactly what survived the wizard's create/delete loops.
            Log("\n── All MenuScreens in scene ──");
            var allScreens = FindObjectsOfType<MenuScreen>(true);
            if (allScreens.Length == 0)
            {
                Log("  (none — open the main menu scene first)");
            }
            else
            {
                foreach (var ms in allScreens)
                    Log($"  • {ms.gameObject.name}   screenType={ms.screenType}   active={ms.gameObject.activeInHierarchy}");
            }

            // 3. Verify each NEW_SCREENS entry actually exists in the scene.
            Log("\n── Required multiplayer screens ──");
            var lobbyTypes = new HashSet<ScreensType>();
            foreach (var (t, _, _) in NEW_SCREENS) lobbyTypes.Add(t);
            var byType = new Dictionary<ScreensType, MenuScreen>();
            foreach (var ms in allScreens) byType[ms.screenType] = ms;
            foreach (var (t, label, _) in NEW_SCREENS)
            {
                bool present = byType.ContainsKey(t);
                Log($"  • {t}   {(present ? "OK" : "MISSING ✗")}");
            }

            // 4. Walk every multiplayer MenuScreen for deep inspection.
            int found = 0;
            foreach (var ms in allScreens)
            {
                if (!lobbyTypes.Contains(ms.screenType)) continue;
                found++;

                Log($"\n── {ms.screenType}  (GameObject '{ms.gameObject.name}') ──");

                // MenuScreen's own back button + canvas group.
                LogField(ms, "canvasGroup");
                LogField(ms, "backButton");
                LogField(ms, "screenCamera");

                // Per-screen controllers.
                foreach (var comp in ms.GetComponents<MonoBehaviour>())
                {
                    if (comp == null) continue;
                    if (comp is MenuScreen) continue;
                    LogAllSerializeFields(comp);
                }

                // Also inspect nested AiSlotsUI / LobbyReadyUI / MapVoteUI components that
                // live deeper in the hierarchy.
                foreach (var nested in ms.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (nested == null) continue;
                    if (nested.gameObject == ms.gameObject) continue;
                    if (nested is MenuScreen) continue;
                    if (!IsInMultiplayerNamespace(nested)) continue;
                    LogAllSerializeFields(nested);
                }
            }

            if (found == 0)
                Log("\nNo multiplayer MenuScreens found in scene. Run SETUP EVERYTHING.");
            Log("\n=== End diagnosis ===");

            // Dump the full diagnosis to a temp file so the user can read the whole thing —
            // Unity's TextArea truncates the visible portion of long logs.
            try
            {
                string dumpPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "lobby-diagnose.txt");
                System.IO.File.WriteAllText(dumpPath, _log);
                Log($"\nFull log written to: {dumpPath}");
                Debug.Log($"[LobbySetupWizard] Full diagnose log: {dumpPath}");
                EditorUtility.RevealInFinder(dumpPath);
            }
            catch (System.Exception ex)
            {
                Log($"\nCould not write log dump: {ex.Message}");
            }
        }

        private static bool IsInMultiplayerNamespace(Component c) =>
            c.GetType().Namespace?.StartsWith("FastAndFractured.Multiplayer") == true;

        private void LogField(Component comp, string fieldName)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(fieldName);
            if (p == null) { Log($"      • {comp.GetType().Name}.{fieldName}: (field not found)"); return; }
            string status = p.objectReferenceValue == null ? "NULL ✗" : "OK — " + p.objectReferenceValue.name;
            Log($"      • {comp.GetType().Name}.{fieldName}: {status}");
        }

        private void LogAllSerializeFields(Component comp)
        {
            var type = comp.GetType();
            Log($"    [{type.Name}] on '{comp.gameObject.name}'");

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                                                         System.Reflection.BindingFlags.NonPublic |
                                                         System.Reflection.BindingFlags.Public;

            var so = new SerializedObject(comp);
            int nullCount = 0, totalRefs = 0;
            foreach (var f in type.GetFields(flags))
            {
                bool isSerialized = f.IsPublic ||
                                    System.Attribute.IsDefined(f, typeof(SerializeField));
                if (!isSerialized) continue;

                // Only ref types (UnityEngine.Object subclasses) — value types like float/Color
                // can't be "wired" so reporting them is noise.
                if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;

                totalRefs++;
                var p = so.FindProperty(f.Name);
                bool isNull = p == null || p.objectReferenceValue == null;
                if (isNull) nullCount++;

                string status = isNull ? "NULL ✗" : "OK — " + p.objectReferenceValue.name;
                Log($"      • {f.Name} ({f.FieldType.Name}): {status}");
            }
            if (totalRefs == 0) Log("      (no UnityEngine.Object refs to check)");
            else if (nullCount > 0) Log($"      ⚠ {nullCount}/{totalRefs} ref(s) NULL on this component");
        }

        /// <summary>
        /// Wires up the dynamic-spawn approach for NetworkedLobbyManager:
        ///   1. Generates the LobbyManagerSpawnable prefab in a Resources folder
        ///   2. Removes any scene-placed NetworkedLobbyManager (to avoid double spawn)
        ///   3. Adds LobbyServerBootstrapper to the NetworkManager GameObject (idempotent)
        ///   4. Triggers FishNet's default prefab rebuild so the new prefab is registered
        /// This bypasses FishNet's scene-NetworkObject SceneId system entirely, which was the
        /// reason the client couldn't see the lobby room.
        /// </summary>
        private void SetupDynamicLobbyManagerSpawn()
        {
            // 1. Ensure the prefab exists in Resources.
            var prefab = LobbyManagerSpawnablePrefabGenerator.Generate();
            Log(prefab != null
                ? $"  ✓ LobbyManagerSpawnable prefab at {LobbyManagerSpawnablePrefabGenerator.PREFAB_PATH}"
                : "  ✗ Failed to generate LobbyManagerSpawnable prefab.");

            // 2. Remove any scene-placed NetworkedLobbyManager that older runs may have added.
            //    Otherwise we'd have two instances at runtime: scene-placed (broken, no SceneId)
            //    + dynamically-spawned (working). Bad state.
            int removed = 0;
            foreach (var existing in Object.FindObjectsOfType<NetworkedLobbyManager>(true))
            {
                // Only delete if it's NOT inside a prefab asset (i.e. it's a real scene instance).
                if (PrefabUtility.IsPartOfPrefabAsset(existing.gameObject)) continue;
                Log($"  Removing scene-placed NetworkedLobbyManager on '{existing.gameObject.name}'");
                Object.DestroyImmediate(existing.gameObject);
                removed++;
            }
            if (removed > 0) Log($"  Removed {removed} stale scene-placed lobby manager(s).");

            // 3. Add LobbyServerBootstrapper to the NetworkManager GameObject so it spawns the
            //    prefab automatically when the server starts.
            var nm = Object.FindAnyObjectByType<FishNet.Managing.NetworkManager>();
            if (nm == null)
            {
                Log("  ⚠ No NetworkManager in scene. Add one before this works at runtime. " +
                    "(LobbyServerBootstrapper needs a NetworkManager to subscribe to.)");
            }
            else
            {
                if (nm.GetComponent<LobbyServerBootstrapper>() == null)
                {
                    nm.gameObject.AddComponent<LobbyServerBootstrapper>();
                    EditorUtility.SetDirty(nm.gameObject);
                    Log($"  ✓ Added LobbyServerBootstrapper to '{nm.gameObject.name}'.");
                }
                else
                {
                    Log($"  LobbyServerBootstrapper already on '{nm.gameObject.name}'.");
                }
            }

            // 4. Rebuild FishNet's default prefab list so the new Resources prefab is registered.
            //    FishNet.Editing.Generator.GenerateFull is the underlying API used by the
            //    "Tools > Fish-Networking > Refresh Default Prefabs" menu item. Call it directly.
            AssetDatabase.SaveAssets(); // ensure prefab is on disk before scanning
            AssetDatabase.Refresh();

            bool generated = false;
            try
            {
                // Find FishNet.Editing.PrefabCollectionGenerator.Generator.GenerateFull
                System.Type generatorType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    generatorType = asm.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator");
                    if (generatorType == null) generatorType = asm.GetType("FishNet.Editing.Generator");
                    if (generatorType != null) break;
                }

                if (generatorType != null)
                {
                    var generateFull = generatorType.GetMethod("GenerateFull",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (generateFull != null)
                    {
                        // Signature: GenerateFull(settings=null, forced=true, initializeAdded=true)
                        var paramz = generateFull.GetParameters();
                        var args = new object[paramz.Length];
                        for (int i = 0; i < paramz.Length; i++)
                        {
                            args[i] = paramz[i].HasDefaultValue ? paramz[i].DefaultValue : null;
                        }
                        if (paramz.Length >= 2) args[1] = true; // forced
                        generateFull.Invoke(null, args);
                        generated = true;
                        Log("  ✓ Generator.GenerateFull(forced=true) — DefaultPrefabObjects rebuilt.");
                    }
                    else
                    {
                        Log($"  ✗ Generator type found ('{generatorType.FullName}') but no GenerateFull method.");
                    }
                }
                else
                {
                    Log("  ✗ Could not find FishNet's Generator type via reflection.");
                }
            }
            catch (System.Exception ex)
            {
                Log($"  Default prefab refresh threw: {ex.Message}");
                Debug.LogException(ex);
            }

            if (!generated)
                Log("  ⚠ Run 'Tools > Fish-Networking > Utility > Refresh Default Prefabs' MANUALLY before testing.");

            // 5. Verify: the prefab should now be in DefaultPrefabObjects.
            var defaultPrefabObjects = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/DefaultPrefabObjects.asset");
            if (defaultPrefabObjects != null)
            {
                var so = new SerializedObject(defaultPrefabObjects);
                var prefabsArr = so.FindProperty("_prefabs");
                if (prefabsArr != null && prefabsArr.isArray)
                {
                    bool found = false;
                    for (int i = 0; i < prefabsArr.arraySize; i++)
                    {
                        var elem = prefabsArr.GetArrayElementAtIndex(i);
                        var refObj = elem.objectReferenceValue;
                        if (refObj == null) continue;
                        string path = AssetDatabase.GetAssetPath(refObj);
                        if (path == LobbyManagerSpawnablePrefabGenerator.PREFAB_PATH ||
                            (refObj is GameObject go && go.name == "LobbyManagerSpawnable"))
                        {
                            found = true;
                            Log($"  ✓ Prefab registered in DefaultPrefabObjects at index {i}.");
                            break;
                        }
                    }
                    if (!found)
                        Log("  ⚠ Prefab NOT found in DefaultPrefabObjects after refresh — spawn will fail. " +
                            "Run 'Tools > Fish-Networking > Utility > Refresh Default Prefabs' manually.");
                }
            }
        }

        /// <summary>
        /// Mirrors Tools > Fish-Networking > Utility > Reserialize NetworkObjects with both
        /// "Reserialize Prefabs" and "Reserialize Scenes (Open Scenes)" turned on. Fixes the
        /// "NetworkObject ObjectId [65535] is expected to be initialized but was not" errors
        /// that fire from FishNet.NetworkObject.Awake when the NetworkBehaviours list on a
        /// prefab or scene object got out of sync (common after editing a NetworkBehaviour).
        ///
        /// All the FishNet methods we need are internal so this goes through reflection.
        /// </summary>
        private void ReserializeNetworkObjects()
        {
            _log = string.Empty;
            Log("=== Reserialize NetworkObjects (Prefabs + Open Scenes) ===");

            if (UnityEditor.EditorApplication.isPlaying)
            {
                Log("  ✗ Cannot reserialize while in Play Mode. Exit play mode first.");
                return;
            }

            // ── Methods via reflection ─────────────────────────────────────────
            var nobType         = typeof(FishNet.Object.NetworkObject);
            var miReserialize   = nobType.GetMethod("ReserializeEditorSetValues",
                                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var miRemoveDup     = nobType.GetMethod("RemoveDuplicateNetworkObjects",
                                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var miCreateSceneId = nobType.GetMethod("CreateSceneId",
                                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            System.Type generatorType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                generatorType = asm.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator");
                if (generatorType == null) generatorType = asm.GetType("FishNet.Editing.Generator");
                if (generatorType != null) break;
            }
            var miGetNobs = generatorType?.GetMethod("GetNetworkObjects",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                                binder: null,
                                types:  new[] { System.Type.GetType("FishNet.Editing.PrefabCollectionGenerator.PrefabGeneratorConfigurations, FishNet.Runtime")
                                                ?? typeof(object) },
                                modifiers: null);

            if (miReserialize == null || miRemoveDup == null)
            {
                Log("  ✗ FishNet ReserializeEditorSetValues / RemoveDuplicateNetworkObjects not found via reflection. " +
                    "FishNet may have renamed them — fall back to Tools > Fish-Networking > Utility > Reserialize NetworkObjects.");
                return;
            }

            // ── PHASE 1: prefabs ───────────────────────────────────────────────
            int prefabsChecked = 0;
            int duplicatesRemoved = 0;

            System.Collections.IEnumerable nobs = null;
            if (miGetNobs != null)
            {
                try { nobs = miGetNobs.Invoke(null, new object[] { null }) as System.Collections.IEnumerable; }
                catch (System.Exception ex) { Log($"  ⚠ Generator.GetNetworkObjects threw: {ex.Message}. Falling back to AssetDatabase scan."); }
            }
            if (nobs == null)
            {
                // Fallback: scan AssetDatabase for prefabs containing a NetworkObject.
                var found = new System.Collections.Generic.List<FishNet.Object.NetworkObject>();
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab  = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var nob = prefab.GetComponent<FishNet.Object.NetworkObject>();
                    if (nob != null) found.Add(nob);
                }
                nobs = found;
            }

            foreach (var obj in nobs)
            {
                var nob = obj as FishNet.Object.NetworkObject;
                if (nob == null) continue;
                prefabsChecked++;
                try
                {
                    var dupResult = miRemoveDup.Invoke(nob, null);
                    if (dupResult is int n) duplicatesRemoved += n;
                    miReserialize.Invoke(nob, new object[] { /*setWasActiveDuringEdit*/ true, /*setSceneId*/ false });
                    EditorUtility.SetDirty(nob);
                }
                catch (System.Exception ex)
                {
                    Log($"  ⚠ Failed to reserialize prefab '{nob.name}': {ex.Message}");
                }
            }
            AssetDatabase.SaveAssets();
            Log($"  ✓ Prefabs: reserialized {prefabsChecked}, removed {duplicatesRemoved} duplicate NetworkObject components.");

            // ── PHASE 2: open scenes ───────────────────────────────────────────
            int sceneNobsChecked = 0;
            int sceneIdsChanged  = 0;
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                if (miCreateSceneId != null)
                {
                    try
                    {
                        var args = new object[] { scene, /*force*/ true, /*out changed*/ 0 };
                        var result = miCreateSceneId.Invoke(null, args) as System.Collections.IEnumerable;
                        if (args[2] is int chg) sceneIdsChanged += chg;
                        if (result != null)
                        {
                            foreach (var obj in result)
                            {
                                var nob = obj as FishNet.Object.NetworkObject;
                                if (nob == null) continue;
                                sceneNobsChecked++;
                                try
                                {
                                    miRemoveDup.Invoke(nob, null);
                                    miReserialize.Invoke(nob, new object[] { true, false });
                                }
                                catch (System.Exception ex)
                                {
                                    Log($"  ⚠ Failed to reserialize scene NOB '{nob.name}': {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex) { Log($"  ⚠ CreateSceneId failed for scene '{scene.name}': {ex.Message}"); }
                }

                EditorSceneManager.MarkSceneDirty(scene);
            }
            EditorSceneManager.SaveOpenScenes();
            Log($"  ✓ Open scenes: reserialized {sceneNobsChecked} NetworkObjects, regenerated {sceneIdsChanged} SceneIds.");
            Log("Done. If you still see [65535] errors at runtime, also run on closed map scenes via the FishNet menu " +
                "(Tools > Fish-Networking > Utility > Reserialize NetworkObjects > Reserialize Scenes > All/Build).");
        }

        /// <summary>
        /// Calls FishNet's PrefabCollectionGenerator.Generator.GenerateFull(forced=true) via
        /// reflection to rebuild DefaultPrefabObjects. Required after creating a new networked
        /// prefab so the FishNet spawn machinery can find it. Shared between the LobbyManager
        /// spawn setup and the LevelController manager spawn setup.
        /// </summary>
        private void RebuildDefaultPrefabObjectsViaGenerator()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            try
            {
                System.Type generatorType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    generatorType = asm.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator");
                    if (generatorType == null) generatorType = asm.GetType("FishNet.Editing.Generator");
                    if (generatorType != null) break;
                }
                if (generatorType == null)
                {
                    Log("  ⚠ Could not find FishNet's Generator type — DefaultPrefabObjects NOT refreshed. " +
                        "Run 'Tools > Fish-Networking > Utility > Refresh Default Prefabs' manually.");
                    return;
                }
                var generateFull = generatorType.GetMethod("GenerateFull",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (generateFull == null)
                {
                    Log($"  ⚠ Generator type '{generatorType.FullName}' has no GenerateFull method.");
                    return;
                }
                var paramz = generateFull.GetParameters();
                var args = new object[paramz.Length];
                for (int i = 0; i < paramz.Length; i++)
                    args[i] = paramz[i].HasDefaultValue ? paramz[i].DefaultValue : null;
                if (paramz.Length >= 2) args[1] = true; // forced
                generateFull.Invoke(null, args);
                Log("  ✓ DefaultPrefabObjects rebuilt (Generator.GenerateFull forced).");
            }
            catch (System.Exception ex)
            {
                Log($"  ⚠ DefaultPrefabObjects refresh threw: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Reads the internal SceneId field of a NetworkObject via reflection. Returns 0 if the
        /// field is not found.
        /// </summary>
        private static ulong ReadSceneIdViaReflection(FishNet.Object.NetworkObject nob)
        {
            if (nob == null) return 0;
            var f = typeof(FishNet.Object.NetworkObject).GetField("SceneId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) return 0;
            var value = f.GetValue(nob);
            return value is ulong u ? u : 0;
        }

        /// <summary>
        /// Forces every NetworkObject in the active scene to have a valid SceneId, via reflection
        /// onto FishNet's internal static <c>NetworkObject.CreateSceneId(scene, force, out changed)</c>.
        /// FishNet normally assigns SceneIds via OnValidate when a NetworkObject is dragged into a
        /// scene from the Inspector. When the LobbyManager is added by code (our wizard) the SceneId
        /// can stay unset until something else triggers it. Without a SceneId, the host and client
        /// cannot match the scene-placed lobby manager during the spawn handshake.
        /// </summary>
        private void RefreshFishNetSceneIds()
        {
            try
            {
                var noType = typeof(FishNet.Object.NetworkObject);
                var method = noType.GetMethod("CreateSceneId",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(UnityEngine.SceneManagement.Scene), typeof(bool), typeof(int).MakeByRefType() },
                    modifiers: null);

                if (method == null)
                {
                    Log("  RefreshFishNetSceneIds: could not find internal CreateSceneId via reflection. " +
                        "Run 'Tools > Fish-Networking > Refresh All NetworkObjects' manually if it exists.");
                    return;
                }

                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                object[] args = { scene, true /* force */, 0 };
                method.Invoke(null, args);
                int changed = (int)args[2];
                Log($"  Refreshed SceneIds — {changed} NetworkObject(s) updated.");
            }
            catch (System.Exception ex)
            {
                Log($"  RefreshFishNetSceneIds threw: {ex.GetType().Name}: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Wires the CharSlectionCamera Cinemachine vcam to the CHARACTER_SELECTION MenuScreen's
        /// screenCamera field if it's null. Same for MAP_SELECTION → MapSlectionCamera.
        /// Required because MainMenuManager.TransitionBetweenScreens uses MenuScreen.ActivateCamera
        /// for camera switching, and an unwired screenCamera leaves Cinemachine on whatever vcam
        /// has highest priority (usually InitialCamera) — which is why "Select Character" was
        /// redirecting to the main menu camera instead of the character selection one.
        /// </summary>
        private void WireSingleplayerScreenCameras()
        {
            // (ScreenType, candidate camera name)
            var pairs = new[]
            {
                (ScreensType.CHARACTER_SELECTION, new[] { "CharSlectionCamera", "CharSelectionCamera", "CharacterSelectionCamera" }),
                (ScreensType.MAP_SELECTION,       new[] { "MapSlectionCamera",  "MapSelectionCamera"  }),
                (ScreensType.GAMEMODE_SELECTION,  new[] { "GameModeScreenCamera", "GameModeCamera"    }),
            };

            foreach (var (screenType, candidates) in pairs)
            {
                MenuScreen screen = null;
                foreach (var ms in FindObjectsOfType<MenuScreen>(true))
                    if (ms.screenType == screenType) { screen = ms; break; }

                if (screen == null) { Log($"  {screenType}: screen not found, skipping."); continue; }
                if (screen.screenCamera != null)
                {
                    Log($"  {screenType}: screenCamera already wired to '{screen.screenCamera.name}', skipping.");
                    continue;
                }

                GameObject cam = null;
                foreach (var name in candidates)
                {
                    cam = GameObject.Find(name);
                    if (cam != null) break;
                }
                if (cam == null) { Log($"  {screenType}: no candidate camera found ({string.Join(", ", candidates)})."); continue; }

                var so = new SerializedObject(screen);
                var prop = so.FindProperty("screenCamera");
                if (prop == null) continue;
                prop.objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(screen);
                Log($"  {screenType}: wired screenCamera = '{cam.name}'");
            }
        }

        private void RunFullSetup()
        {
            _log = string.Empty;
            Log("=== Lobby Setup Wizard: Full Setup ===");

            // 1. Catalog
            CreateOrUpdateMapCatalog();

            // 2. Entry prefabs (browser row + lobby player slot)
            var entryPrefab = LobbyBrowserEntryPrefabGenerator.Generate();
            Log(entryPrefab != null ? "LobbyBrowserEntry prefab generated." : "LobbyBrowserEntry prefab FAILED.");
            var slotPrefab = PlayerSlotEntryPrefabGenerator.Generate();
            Log(slotPrefab  != null ? "PlayerSlotEntry prefab generated."   : "PlayerSlotEntry prefab FAILED.");

            // 3. NetworkingCommons is NOT instantiated here — per project decision it loads only
            //    in the game scene. The wizard does NOT modify the user's main-menu NetworkManager.
            //    LobbyManager presence is verified by DIAGNOSE; user wires it themselves.

            // 4. DELETE & recreate lobby screens so the wiring is always fresh after script changes
            //    (this is the only way to guarantee no stale references after a refactor).
            DeleteAndRecreateLobbyScreens();

            // 5. Main menu entry point
            AddMultiplayerButtonToMainMenu();

            // 6. Shared camera across all multiplayer screens
            CreateMultiplayerHubCamera();

            // 7. Wire singleplayer screen cameras so SELECT CHARACTER / map flow works.
            Log("Wiring singleplayer screen cameras…");
            WireSingleplayerScreenCameras();

            // 8. Generate the dynamic-spawn prefab for NetworkedLobbyManager and wire its server
            //    bootstrapper. This replaces the broken scene-placed NetworkObject approach.
            Log("Setting up dynamic LobbyManager spawn…");
            SetupDynamicLobbyManagerSpawn();

            // 9. Generate the dynamic-spawn prefab for the multiplayer LevelController manager.
            //    MultiplayerLevelControllerAutoInitGate loads it via Resources.Load when a map
            //    scene finishes loading and the local peer is the server.
            Log("Generating MultiplayerLevelControllerSpawnable prefab…");
            var lcSpawn = MultiplayerLevelControllerSpawnablePrefabGenerator.Generate();
            Log(lcSpawn != null
                    ? "MultiplayerLevelControllerSpawnable prefab generated."
                    : "MultiplayerLevelControllerSpawnable prefab FAILED.");

            // Refresh DefaultPrefabObjects so the new spawnable is registered. Without this,
            // FishNet rejects the dynamic spawn on the server (the manager prefab isn't in its
            // prefab table for replication).
            RebuildDefaultPrefabObjectsViaGenerator();

            // 10. Persist
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Log("Scene saved. Done!");
        }

        /// <summary>
        /// Ensures the scene has a working NetworkManager + Transport + NetworkedLobbyManager.
        /// Order:
        ///   1. Instantiate the NetworkingCommons prefab if no NetworkManager is in scene.
        ///   2. Attach a Tugboat Transport if TransportManager.Transport is null.
        ///   3. Add a child GameObject 'LobbyManager' with NetworkObject + NetworkedLobbyManager
        ///      if no NetworkedLobbyManager exists under the NetworkManager.
        /// Each step is independent and idempotent — re-running fixes drift without duplicating.
        /// </summary>
        private void EnsureNetworkingCommonsInScene()
        {
            // ── Step 1: NetworkManager prefab in scene ─────────────────────────────────
            var nm = Object.FindAnyObjectByType<FishNet.Managing.NetworkManager>();
            if (nm == null)
            {
                string ncPath = AssetDatabase.GUIDToAssetPath(GUID_NETWORKING_COMMONS);
                var ncAsset = string.IsNullOrEmpty(ncPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(ncPath);

                if (ncAsset == null)
                {
                    Log($"ERROR: Could not find NetworkingCommons prefab (GUID {GUID_NETWORKING_COMMONS}).");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ncAsset);
                if (instance == null)
                {
                    Log("ERROR: PrefabUtility.InstantiatePrefab returned null for NetworkingCommons.");
                    return;
                }
                instance.transform.SetParent(null);
                instance.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(instance, "Add NetworkingCommons to scene");

                nm = instance.GetComponentInChildren<FishNet.Managing.NetworkManager>(true);
                Log($"Dropped NetworkingCommons into scene (NetworkManager={(nm != null ? "OK" : "MISSING ✗")}).");
            }
            else
            {
                Log($"NetworkManager already in scene ('{nm.gameObject.name}').");
            }

            if (nm == null) return;

            // ── Step 2: Transport ─────────────────────────────────────────────────────
            var tm = nm.TransportManager;
            if (tm != null && tm.Transport == null)
            {
                // Prefer a transport already attached to the NM GameObject; otherwise add Tugboat.
                var existingTransport = nm.GetComponent<FishNet.Transporting.Transport>();
                if (existingTransport == null)
                {
                    existingTransport = nm.gameObject.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();
                    Log($"Added Tugboat transport to '{nm.gameObject.name}'.");
                }

                var soTm = new SerializedObject(tm);
                var pTransport = soTm.FindProperty("Transport");
                if (pTransport != null)
                {
                    pTransport.objectReferenceValue = existingTransport;
                    soTm.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(tm);
                    Log("Assigned Transport to TransportManager.");
                }
            }
            else if (tm != null)
            {
                Log($"Transport already configured: {tm.Transport.GetType().Name}.");
            }

            // ── Step 3: NetworkedLobbyManager as child of NM ───────────────────────────
            var existingLobby = nm.GetComponentInChildren<NetworkedLobbyManager>(true);
            if (existingLobby == null)
            {
                var lobbyGO = new GameObject("LobbyManager");
                Undo.RegisterCreatedObjectUndo(lobbyGO, "Add LobbyManager child");
                lobbyGO.transform.SetParent(nm.transform, false);

                // NetworkedLobbyManager requires a NetworkObject on the same GO to spawn.
                lobbyGO.AddComponent<FishNet.Object.NetworkObject>();
                lobbyGO.AddComponent<NetworkedLobbyManager>();

                Log($"Created child '{lobbyGO.name}' with NetworkObject + NetworkedLobbyManager.");
            }
            else
            {
                Log($"NetworkedLobbyManager already in scene on '{existingLobby.gameObject.name}'.");
            }
        }

        // ─────────────────────────────────────────────
        //  MAP CATALOG
        // ─────────────────────────────────────────────
        // Scene GUIDs — stable regardless of file renames or folder moves
        private const string GUID_GOLF_MAP             = "1c251044aeaa846428dbf86821c160d3";
        private const string GUID_CONSTRUCTION_MAP     = "f0e339ead76f23943b9db6bcdaf20d92";
        private const string GUID_MOUNTAIN_VILLAGE_MAP = "1354e600b46f3c3488593866c2ca43b3";
        private const string GUID_NETWORKING_COMMONS   = "8096a6240cc47914d94ecd79c8f0e955";

        private void CreateOrUpdateMapCatalog()
        {
            if (!Directory.Exists(RESOURCES_DIR))
            {
                Directory.CreateDirectory(RESOURCES_DIR);
                AssetDatabase.Refresh();
            }

            MapCatalog catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CATALOG_RESOURCE_PATH);
            bool isNew = catalog == null;

            if (isNew)
            {
                catalog = ScriptableObject.CreateInstance<MapCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG_RESOURCE_PATH);
                Log($"Created MapCatalog at {CATALOG_RESOURCE_PATH}");
            }
            else
            {
                Log("MapCatalog already exists — updating entries.");
            }

            var so          = new SerializedObject(catalog);
            var entriesProp = so.FindProperty("_entries");
            if (entriesProp == null)
            {
                Log("ERROR: Could not find '_entries' on MapCatalog.");
                return;
            }

            // Resolve scene names from GUIDs so renaming files doesn't break the catalog
            string golfPath     = AssetDatabase.GUIDToAssetPath(GUID_GOLF_MAP);
            string constPath    = AssetDatabase.GUIDToAssetPath(GUID_CONSTRUCTION_MAP);
            string villagePath  = AssetDatabase.GUIDToAssetPath(GUID_MOUNTAIN_VILLAGE_MAP);

            string golfScene    = SceneNameFromPath(golfPath,    "GolfMap");
            string constScene   = SceneNameFromPath(constPath,   "ConstructionMap");
            string villageScene = SceneNameFromPath(villagePath, "MountainVillageMap");

            entriesProp.arraySize = 3;
            SetEntry(entriesProp.GetArrayElementAtIndex(0), 0, "Golf Map",            golfScene,    "GOLF2");
            SetEntry(entriesProp.GetArrayElementAtIndex(1), 1, "Construction Map",    constScene,   "OBRAS_RENDER");
            SetEntry(entriesProp.GetArrayElementAtIndex(2), 2, "Mountain Village Map", villageScene, "PUEBLO_SCREENSHOT");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            // Warn if any scene wasn't found
            if (string.IsNullOrEmpty(golfPath))    Log("WARNING: GolfMap not found (GUID may be stale). Update GUID_GOLF_MAP.");
            if (string.IsNullOrEmpty(constPath))   Log("WARNING: ConstructionMap not found. Update GUID_CONSTRUCTION_MAP.");
            if (string.IsNullOrEmpty(villagePath)) Log("WARNING: MountainVillageMap not found. Update GUID_MOUNTAIN_VILLAGE_MAP.");

            Log($"MapCatalog entries written: '{golfScene}'(0), '{constScene}'(1), '{villageScene}'(2)");
        }

        private static string SceneNameFromPath(string path, string fallback) =>
            string.IsNullOrEmpty(path)
                ? fallback
                : System.IO.Path.GetFileNameWithoutExtension(path);

        private static void SetEntry(SerializedProperty entryProp, int id, string displayKey, string sceneName, string spriteBaseName)
        {
            entryProp.FindPropertyRelative("Id").intValue                    = id;
            entryProp.FindPropertyRelative("DisplayNameKey").stringValue     = displayKey;
            entryProp.FindPropertyRelative("SceneName").stringValue          = sceneName;

            // Locate thumbnail by name inside the map-selection sprites folder
            string[] guids = AssetDatabase.FindAssets(
                $"{spriteBaseName} t:Sprite",
                new[] { "Assets/FastAndFractured/MapSelection/Sprites" });
            if (guids.Length > 0)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
                entryProp.FindPropertyRelative("Thumbnail").objectReferenceValue = sprite;
            }
        }

        // ─────────────────────────────────────────────
        //  MENU SCREENS
        // ─────────────────────────────────────────────
        private const string MULTIPLAYER_CANVAS_NAME = "MultiplayerUICanvas";

        /// <summary>
        /// Finds or creates the dedicated multiplayer Screen Space Overlay Canvas. The existing
        /// menu uses per-zone World Space canvases at scattered positions; reusing one of those
        /// requires placing the screens at exactly the right world coordinates for the
        /// MultiplayerHubCamera to see them, which is fragile. An Overlay canvas renders on top
        /// of everything regardless of camera, so the screens are always visible when active.
        /// </summary>
        private static Canvas EnsureMultiplayerCanvas()
        {
            // Look for existing multiplayer canvas in scene
            foreach (var c in Object.FindObjectsOfType<Canvas>(true))
            {
                if (c.gameObject.name == MULTIPLAYER_CANVAS_NAME) return c;
            }

            // Create new Screen Space Overlay canvas
            var canvasGO = new GameObject(MULTIPLAYER_CANVAS_NAME);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above any existing canvas

            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode          = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight   = 0.5f;

            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            return canvas;
        }

        private void CreateMenuScreens()
        {
            // 1. Find an existing MenuScreen in the scene (for the template reference).
            var allScreens = FindObjectsOfType<MenuScreen>(true);
            if (allScreens.Length == 0)
            {
                Log("ERROR: No MenuScreen found in the current scene. Open the main menu scene first.");
                return;
            }

            // 2. Get / create the dedicated multiplayer Overlay Canvas. New multiplayer screens
            //    go under THIS, not under the existing per-zone world-space canvases.
            var mpCanvas = EnsureMultiplayerCanvas();
            Transform screenParent = mpCanvas.transform;

            Log($"Creating new screens under: '{screenParent.name}' (Screen Space Overlay)");

            var existingTypes = FindAllMenuScreenTypes();

            foreach (var (screenType, label, controllerType) in NEW_SCREENS)
            {
                if (existingTypes.Contains(screenType))
                {
                    Log($"  SKIP  {label} — already exists.");
                    continue;
                }

                GameObject go = null;
                try
                {
                    go = CreateScreenGO(label, screenType, screenParent, allScreens[0]);
                    AttachControllers(go, screenType, controllerType);
                    Log($"  CREATED  {label} ({screenType})");
                }
                catch (System.Exception ex)
                {
                    // Don't abort the whole foreach if one screen fails — log the failure and
                    // continue so the user gets a partial setup they can debug from.
                    Log($"  FAILED   {label} ({screenType}) — {ex.GetType().Name}: {ex.Message}");
                    Debug.LogException(ex);
                    // Leave the partial GO around so the diagnostic can spot it. If we destroy
                    // it the user would not see that *some* state was created before the error.
                }
            }

            EditorSceneManager.MarkAllScenesDirty();
        }

        private static GameObject CreateScreenGO(string label, ScreensType type, Transform parent, MenuScreen template)
        {
            // Create root GO
            var go = new GameObject($"Screen_{label.Replace(" ", "")}");
            go.transform.SetParent(parent, false);

            // RectTransform — full screen stretch like all other screens
            var rt = go.AddComponent<RectTransform>();
            CopyFullScreenAnchor(rt, template.GetComponent<RectTransform>());

            // CanvasGroup (required for fade transitions)
            var canvasGroup = go.AddComponent<CanvasGroup>();

            // Background Image — FULLY OPAQUE dark blue so it's impossible to miss when active.
            // Earlier attempts used alpha 0.8 which combined with CanvasGroup fade left the
            // screen apparently invisible for hours. Make it loud.
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.12f, 0.20f, 1f);
            bgImg.raycastTarget = true;

            // MenuScreen component
            var menuScreen = go.AddComponent<MenuScreen>();

            // Defensive: forces canvasGroup.alpha to 1 a few frames after activation if the
            // MainMenuManager fadeIn never reaches it. Fixes a hard-to-repro "camera switches
            // to the new screen but UI stays invisible" bug.
            go.AddComponent<FastAndFractured.Multiplayer.EnsureMenuScreenVisible>();

            // Wire all serialized fields via one SerializedObject instance
            var so_ms = new SerializedObject(menuScreen);

            // canvasGroup — the field MenuScreen.SetInteractable/SetAlpha require
            var canvasGroupProp = so_ms.FindProperty("canvasGroup");
            if (canvasGroupProp != null) canvasGroupProp.objectReferenceValue = canvasGroup;

            // screenType
            var screenTypeProp = so_ms.FindProperty("screenType");
            if (screenTypeProp != null)
                screenTypeProp.enumValueIndex = (int)type;

            so_ms.ApplyModifiedPropertiesWithoutUndo();

            // Title label
            var titleGO    = new GameObject("Title");
            titleGO.transform.SetParent(go.transform, false);
            var titleRT    = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin  = new Vector2(0f, 0.85f);
            titleRT.anchorMax  = new Vector2(1f, 1f);
            titleRT.offsetMin  = titleRT.offsetMax = Vector2.zero;
            var title      = titleGO.AddComponent<TextMeshProUGUI>();
            title.text     = label.ToUpper();
            title.fontSize = 48f;
            title.alignment = TextAlignmentOptions.Center;

            // Back button (bottom-left). 22% × 12% — large enough to read on all aspect ratios.
            var backGO = CreateButton(go.transform, "BackButton", "BACK",
                new Vector2(0.02f, 0.02f), new Vector2(0.20f, 0.12f), fontSize: 26f);

            // Wire backButton and defaultInteractable into the same so_ms (already Updated above)
            var backBtnProp    = so_ms.FindProperty("backButton");
            var defaultBtnProp = so_ms.FindProperty("defaultInteractable");
            var backBtn        = backGO.GetComponent<Button>();
            if (backBtnProp    != null) backBtnProp.objectReferenceValue    = backBtn;
            if (defaultBtnProp != null) defaultBtnProp.objectReferenceValue = backBtn;
            so_ms.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(menuScreen);
            return go;
        }

        private static void AttachControllers(GameObject go, ScreensType type, System.Type controllerType)
        {
            // Anchor reference — all layouts use the same coordinate system:
            //   x: 0 = left,    1 = right
            //   y: 0 = bottom,  1 = top
            // CreateScreenGO already created a BackButton at (0,0)-(0.2,0.1) and a Title at (0,0.85)-(1,1).
            switch (type)
            {
                case ScreensType.MULTIPLAYER_HUB:
                {
                    var hub = go.AddComponent<MultiplayerHubController>();

                    // 3 main actions stacked vertically in the middle of the screen — taller for readability.
                    CreateButton(go.transform, "HostButton",       "HOST A GAME",
                        new Vector2(0.30f, 0.62f), new Vector2(0.70f, 0.74f), fontSize: 30f);
                    CreateButton(go.transform, "BrowseButton",     "BROWSE ROOMS",
                        new Vector2(0.30f, 0.46f), new Vector2(0.70f, 0.58f), fontSize: 30f);
                    CreateButton(go.transform, "DirectJoinButton", "DIRECT JOIN (IP)",
                        new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.42f), fontSize: 30f);

                    WireSerializedFields(hub, new Dictionary<string, Component>
                    {
                        ["_hostButton"]       = FindButton(go, "HostButton"),
                        ["_browseButton"]     = FindButton(go, "BrowseButton"),
                        ["_directJoinButton"] = FindButton(go, "DirectJoinButton"),
                        ["_backButton"]       = FindButton(go, "BackButton"),
                    });
                    break;
                }

                case ScreensType.MULTIPLAYER_HOST_SETUP:
                {
                    var host = go.AddComponent<MultiplayerHostSetupController>();

                    // Form column on the left half.
                    AddLabel(go.transform,      "RoomNameLabel", "ROOM NAME",
                        new Vector2(0.10f, 0.72f), new Vector2(0.45f, 0.80f), fontSize: 24f);
                    AddInputField(go.transform, "RoomNameInput", "Room name...",
                        new Vector2(0.10f, 0.62f), new Vector2(0.70f, 0.72f));

                    AddLabel(go.transform,      "PortLabel", "PORT",
                        new Vector2(0.10f, 0.52f), new Vector2(0.30f, 0.60f), fontSize: 24f);
                    AddInputField(go.transform, "PortInput", "7777",
                        new Vector2(0.10f, 0.43f), new Vector2(0.30f, 0.52f));

                    // AI slot count is configured from the lobby ready screen (AiSlotsUI),
                    // not from this host-setup screen — single source of truth avoids the host
                    // having to set it in two places.

                    // Big primary action: bottom-right.
                    CreateButton(go.transform, "StartButton", "START HOSTING",
                        new Vector2(0.50f, 0.05f), new Vector2(0.95f, 0.20f), fontSize: 30f);

                    // Status line above the start button — for errors / "connecting…" messages.
                    AddLabel(go.transform, "StatusLabel", string.Empty,
                        new Vector2(0.10f, 0.20f), new Vector2(0.95f, 0.27f), fontSize: 20f);

                    WireSerializedFields(host, new Dictionary<string, Component>
                    {
                        ["_roomNameInput"] = FindInput (go, "RoomNameInput"),
                        ["_portInput"]     = FindInput (go, "PortInput"),
                        ["_startButton"]   = FindButton(go, "StartButton"),
                        ["_backButton"]    = FindButton(go, "BackButton"),
                        ["_statusLabel"]   = FindLabel (go, "StatusLabel"),
                    });
                    break;
                }

                case ScreensType.MULTIPLAYER_BROWSER:
                {
                    var browser = go.AddComponent<LobbyBrowserController>();

                    AddLabel(go.transform, "StatusLabel", "Searching...",
                        new Vector2(0.10f, 0.76f), new Vector2(0.90f, 0.84f), fontSize: 22f);

                    var listContainer = CreatePanel(go.transform, "EntryContainer",
                        new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.75f));
                    var vl = listContainer.AddComponent<VerticalLayoutGroup>();
                    vl.childForceExpandWidth  = true;
                    vl.childForceExpandHeight = false;
                    vl.spacing = 4f;
                    listContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // Refresh: bottom-right, sibling to back.
                    CreateButton(go.transform, "RefreshButton", "REFRESH",
                        new Vector2(0.65f, 0.03f), new Vector2(0.95f, 0.14f), fontSize: 26f);

                    WireSerializedFields(browser, new Dictionary<string, Component>
                    {
                        ["_entryContainer"] = listContainer.transform,
                        ["_refreshButton"]  = FindButton(go, "RefreshButton"),
                        ["_backButton"]     = FindButton(go, "BackButton"),
                        ["_statusLabel"]    = FindLabel (go, "StatusLabel"),
                    });

                    // Also try to wire the entry prefab if it exists in the project.
                    // (LobbyBrowserEntryPrefabGenerator saves to /Prefabs/ subfolder.)
                    var entryPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Multiplayer/Lobby/Prefabs/LobbyBrowserEntry.prefab");
                    if (entryPrefabAsset != null)
                    {
                        var entryComp = entryPrefabAsset.GetComponent<LobbyBrowserEntry>();
                        if (entryComp != null)
                        {
                            var so = new SerializedObject(browser);
                            var p  = so.FindProperty("_entryPrefab");
                            if (p != null) { p.objectReferenceValue = entryComp; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(browser); }
                        }
                    }
                    break;
                }

                case ScreensType.MULTIPLAYER_DIRECT_JOIN:
                {
                    var direct = go.AddComponent<MultiplayerDirectJoinController>();

                    AddLabel(go.transform,      "IpLabel", "IP",
                        new Vector2(0.10f, 0.66f), new Vector2(0.20f, 0.76f), fontSize: 24f);
                    AddInputField(go.transform, "IpInput", "192.168.x.x",
                        new Vector2(0.22f, 0.66f), new Vector2(0.90f, 0.76f));

                    AddLabel(go.transform,      "PortLabel", "PORT",
                        new Vector2(0.10f, 0.52f), new Vector2(0.20f, 0.62f), fontSize: 24f);
                    AddInputField(go.transform, "PortInput", "7777",
                        new Vector2(0.22f, 0.52f), new Vector2(0.50f, 0.62f));

                    AddLabel(go.transform, "StatusLabel", string.Empty,
                        new Vector2(0.10f, 0.36f), new Vector2(0.90f, 0.46f), fontSize: 20f);

                    CreateButton(go.transform, "JoinButton", "CONNECT",
                        new Vector2(0.50f, 0.05f), new Vector2(0.95f, 0.20f), fontSize: 30f);

                    WireSerializedFields(direct, new Dictionary<string, Component>
                    {
                        ["_ipInput"]     = FindInput (go, "IpInput"),
                        ["_portInput"]   = FindInput (go, "PortInput"),
                        ["_joinButton"]  = FindButton(go, "JoinButton"),
                        ["_backButton"]  = FindButton(go, "BackButton"),
                        ["_statusLabel"] = FindLabel (go, "StatusLabel"),
                    });
                    break;
                }

                case ScreensType.LOBBY_ROOM:
                {
                    // Layout:
                    //   ┌──────────────────────────────────────────────────────┐
                    //   │ Title (top)                          AI:N  [−] [+]   │
                    //   │ "Room Name" header                                   │
                    //   │ ──────────────────────────────────────────────────── │
                    //   │ PlayerSlot 0:  icon  name  [READY]   map-thumb [SEL] │
                    //   │ PlayerSlot 1:  icon  name  [READY]   map-thumb       │
                    //   │ ...                                                  │
                    //   │ ──────────────────────────────────────────────────── │
                    //   │ Back    READY    FORCE START                         │
                    //   └──────────────────────────────────────────────────────┘
                    var lobbyUI = go.AddComponent<LobbyReadyUI>();

                    // ── Room name label (below the title, above the player list) ───────
                    AddLabel(go.transform, "RoomNameLabel", "Room",
                        new Vector2(0.04f, 0.76f), new Vector2(0.55f, 0.84f), fontSize: 28f);

                    // ── AI slots strip (top-right) ─────────────────────────────────────
                    var aiPanel = CreatePanel(go.transform, "AiSlotsPanel",
                        new Vector2(0.55f, 0.76f), new Vector2(0.98f, 0.84f));
                    aiPanel.AddComponent<AiSlotsUI>();
                    AddLabel(aiPanel.transform, "AiCountLabel", "AI: 0  |  Total: 1/8",
                        new Vector2(0.04f, 0f), new Vector2(0.70f, 1f), fontSize: 22f);
                    CreateButton(aiPanel.transform, "DecButton", "−",
                        new Vector2(0.71f, 0.10f), new Vector2(0.84f, 0.90f), fontSize: 30f);
                    CreateButton(aiPanel.transform, "IncButton", "+",
                        new Vector2(0.85f, 0.10f), new Vector2(0.98f, 0.90f), fontSize: 30f);

                    // ── Player list (full width) ────────────────────────────────────────
                    var listPanel = CreatePanel(go.transform, "PlayerListPanel",
                        new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.75f));

                    var playerContainer = CreatePanel(listPanel.transform, "PlayerListContainer",
                        new Vector2(0f, 0f), new Vector2(1f, 1f));
                    // A grid: up to 8 slots (humans + AI) must fit the panel (LobbyReadyUI also
                    // converts an older vertical list at runtime).
                    var grid = playerContainer.AddComponent<GridLayoutGroup>();
                    grid.cellSize       = new Vector2(400f, 120f);
                    grid.spacing        = new Vector2(12f, 12f);
                    grid.padding        = new RectOffset(8, 8, 8, 8);
                    grid.childAlignment = TextAnchor.UpperCenter;
                    playerContainer.AddComponent<ContentSizeFitter>().verticalFit =
                        ContentSizeFitter.FitMode.PreferredSize;

                    // ── Bottom action row (Back is bottom-left, created by CreateScreenGO) ──
                    CreateButton(go.transform, "ReadyButton",      "READY",
                        new Vector2(0.22f, 0.02f), new Vector2(0.45f, 0.14f), fontSize: 28f);
                    CreateButton(go.transform, "ForceStartButton", "FORCE START",
                        new Vector2(0.48f, 0.02f), new Vector2(0.78f, 0.14f), fontSize: 26f);

                    // ── Countdown overlay (hidden until OnCountdownStarted fires) ──────
                    var countdownPanel = CreatePanel(go.transform, "CountdownPanel",
                        new Vector2(0.30f, 0.40f), new Vector2(0.70f, 0.60f));
                    countdownPanel.SetActive(false);
                    var countdownLbl = new GameObject("CountdownText");
                    countdownLbl.transform.SetParent(countdownPanel.transform, false);
                    var clRT = countdownLbl.AddComponent<RectTransform>();
                    clRT.anchorMin = Vector2.zero; clRT.anchorMax = Vector2.one;
                    clRT.offsetMin = clRT.offsetMax = Vector2.zero;
                    var clTmp = countdownLbl.AddComponent<TMPro.TextMeshProUGUI>();
                    clTmp.text      = "...";
                    clTmp.alignment = TMPro.TextAlignmentOptions.Center;
                    clTmp.fontSize  = 40f;
                    clTmp.color     = Color.white;

                    // Ensure the PlayerSlotEntry prefab exists, then wire LobbyReadyUI.
                    var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Multiplayer/Lobby/Prefabs/PlayerSlotEntry.prefab");
                    if (slotPrefab == null)
                    {
                        var generated = PlayerSlotEntryPrefabGenerator.Generate();
                        if (generated != null) slotPrefab = generated.gameObject;
                    }

                    WireSerializedFields(lobbyUI, new Dictionary<string, Component>
                    {
                        ["_readyButton"]         = FindButton(go,  "ReadyButton"),
                        ["_readyButtonText"]     = FindButton(go,  "ReadyButton")?.GetComponentInChildren<TMP_Text>(),
                        ["_readyButtonImage"]    = FindButton(go,  "ReadyButton")?.GetComponent<Image>(),
                        ["_forceStartButton"]    = FindButton(go,  "ForceStartButton"),
                        ["_backButton"]          = FindButton(go,  "BackButton"),
                        ["_playerListContainer"] = playerContainer.transform,
                        ["_countdownText"]       = countdownLbl.GetComponent<TMP_Text>(),
                        ["_roomNameLabel"]       = FindLabel(go,  "RoomNameLabel"),
                    });

                    // GameObject-ref fields (not Component): _countdownPanel and _playerEntryPrefab.
                    var soLobby = new SerializedObject(lobbyUI);
                    var pCountdown = soLobby.FindProperty("_countdownPanel");
                    if (pCountdown != null) pCountdown.objectReferenceValue = countdownPanel;
                    var pEntry = soLobby.FindProperty("_playerEntryPrefab");
                    if (pEntry != null && slotPrefab != null) pEntry.objectReferenceValue = slotPrefab;
                    soLobby.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(lobbyUI);

                    // ── Map vote slot-machine animation overlay ──────────────────────
                    var animUI = BuildMapVoteAnimationOverlay(go.transform);

                    // Wire the overlay reference into LobbyReadyUI so the click subscription
                    // doesn't depend on a runtime GetComponentInChildren search.
                    var soWire = new SerializedObject(lobbyUI);
                    var pAnim  = soWire.FindProperty("_mapVoteAnimation");
                    if (pAnim != null && animUI != null) pAnim.objectReferenceValue = animUI;
                    soWire.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(lobbyUI);

                    // Wire AiSlotsUI (lives inside aiPanel).
                    var aiUI = aiPanel.GetComponent<AiSlotsUI>();
                    if (aiUI != null)
                    {
                        WireSerializedFields(aiUI, new Dictionary<string, Component>
                        {
                            ["_countLabel"] = aiPanel.transform.Find("AiCountLabel")?.GetComponent<TMP_Text>(),
                            ["_decButton"]  = aiPanel.transform.Find("DecButton")?.GetComponent<Button>(),
                            ["_incButton"]  = aiPanel.transform.Find("IncButton")?.GetComponent<Button>(),
                        });
                    }
                    break;
                }

                case ScreensType.LOBBY_MAP_VOTE:
                    // Reserved for future standalone vote screen
                    AddLabel(go.transform, "Info", "Map Vote Screen (reserved)",
                        new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f), fontSize: 24f);
                    break;
            }
        }

        /// <summary>
        /// Builds the slot-machine overlay inside the LOBBY_ROOM screen:
        /// full-screen dimmer + big map thumbnail + map name + WINNER label + canvas group for
        /// DOTween fading. Hidden by default — activated by NetworkedLobbyManager's RPC when the
        /// server resolves the map vote.
        /// </summary>
        private static MapVoteAnimationUI BuildMapVoteAnimationOverlay(Transform screenRoot)
        {
            var overlayGO = new GameObject("MapVoteAnimationOverlay");
            overlayGO.transform.SetParent(screenRoot, false);
            var rt = overlayGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var dimmer = overlayGO.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.85f);
            dimmer.raycastTarget = true;

            var cg = overlayGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            overlayGO.SetActive(false);

            // Big map thumbnail center.
            var thumbGO = new GameObject("MapImage");
            thumbGO.transform.SetParent(overlayGO.transform, false);
            var thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0.20f, 0.30f);
            thumbRT.anchorMax = new Vector2(0.80f, 0.78f);
            thumbRT.offsetMin = thumbRT.offsetMax = Vector2.zero;
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.preserveAspect = true;

            // Map name label.
            var nameGO = new GameObject("MapNameLabel");
            nameGO.transform.SetParent(overlayGO.transform, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.10f, 0.18f);
            nameRT.anchorMax = new Vector2(0.90f, 0.28f);
            nameRT.offsetMin = nameRT.offsetMax = Vector2.zero;
            var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
            nameTmp.text      = "MAP";
            nameTmp.fontSize  = 44f;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color     = Color.white;

            // WINNER label, hidden until reveal.
            var winGO = new GameObject("WinnerLabel");
            winGO.transform.SetParent(overlayGO.transform, false);
            var winRT = winGO.AddComponent<RectTransform>();
            winRT.anchorMin = new Vector2(0.20f, 0.80f);
            winRT.anchorMax = new Vector2(0.80f, 0.95f);
            winRT.offsetMin = winRT.offsetMax = Vector2.zero;
            var winTmp = winGO.AddComponent<TextMeshProUGUI>();
            winTmp.text      = "★  WINNER  ★";
            winTmp.fontSize  = 64f;
            winTmp.fontStyle = FontStyles.Bold;
            winTmp.alignment = TextAlignmentOptions.Center;
            winTmp.color     = new Color(1f, 0.85f, 0.3f);
            winGO.SetActive(false);

            // Component + wiring.
            var anim = overlayGO.AddComponent<MapVoteAnimationUI>();
            var so = new SerializedObject(anim);
            so.FindProperty("_root").objectReferenceValue          = overlayGO;
            so.FindProperty("_canvasGroup").objectReferenceValue   = cg;
            so.FindProperty("_mapImage").objectReferenceValue      = thumbImg;
            so.FindProperty("_mapNameLabel").objectReferenceValue  = nameTmp;
            so.FindProperty("_winnerLabel").objectReferenceValue   = winTmp;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anim);
            return anim;
        }

        // ── Wiring helpers ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Assigns every (fieldName → component) pair on a MonoBehaviour via SerializedObject.
        /// Skips entries whose component is null (e.g. when an optional UI element wasn't created)
        /// and logs a warning when a field name doesn't exist on the component (likely a refactor drift).
        /// </summary>
        private static void WireSerializedFields(Component target, Dictionary<string, Component> assignments)
        {
            if (target == null || assignments == null) return;
            var so = new SerializedObject(target);
            int wired = 0, missingProps = 0;
            foreach (var kv in assignments)
            {
                if (kv.Value == null) continue;
                var p = so.FindProperty(kv.Key);
                if (p == null)
                {
                    Debug.LogWarning($"[LobbySetupWizard] {target.GetType().Name} has no field '{kv.Key}' — refactor drift?");
                    missingProps++;
                    continue;
                }
                p.objectReferenceValue = kv.Value;
                wired++;
            }
            if (wired > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
            Debug.Log($"[LobbySetupWizard] Wired {wired}/{assignments.Count} fields on {target.GetType().Name}" +
                      (missingProps > 0 ? $" ({missingProps} field name(s) not found)" : ""));
        }

        private static Button         FindButton(GameObject root, string name) => root.transform.Find(name)?.GetComponent<Button>();
        private static TMP_InputField FindInput (GameObject root, string name) => root.transform.Find(name)?.GetComponent<TMP_InputField>();
        private static TMP_Text       FindLabel (GameObject root, string name) => root.transform.Find(name)?.GetComponent<TMP_Text>();

        // ─────────────────────────────────────────────
        //  UI HELPERS
        // ─────────────────────────────────────────────
        private static void CopyFullScreenAnchor(RectTransform target, RectTransform source)
        {
            if (source != null)
            {
                target.anchorMin  = source.anchorMin;
                target.anchorMax  = source.anchorMax;
                target.offsetMin  = source.offsetMin;
                target.offsetMax  = source.offsetMax;
                target.pivot      = source.pivot;
            }
            else
            {
                target.anchorMin = Vector2.zero;
                target.anchorMax = Vector2.one;
                target.offsetMin = target.offsetMax = Vector2.zero;
            }
        }

        private static GameObject CreateButton(Transform parent, string goName, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize = 26f)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.9f, 1f);

            go.AddComponent<Button>();

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;

            return go;
        }

        private static GameObject CreatePanel(Transform parent, string goName, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go  = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.4f);
            return go;
        }

        private static void AddInputField(Transform parent, string goName, string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            // TextArea with RectMask2D — must exist BEFORE TMP_InputField is added
            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRT = textArea.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(8f, 2f);
            textAreaRT.offsetMax = new Vector2(-8f, -2f);
            textArea.AddComponent<RectMask2D>();

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textArea.transform, false);
            var phRT = placeholderGO.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = phRT.offsetMax = Vector2.zero;
            var phTmp = placeholderGO.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.color = new Color(1f, 1f, 1f, 0.4f);
            phTmp.fontSize = 18f;
            phTmp.enableWordWrapping = false;
            phTmp.overflowMode = TextOverflowModes.Ellipsis;

            var inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(textArea.transform, false);
            var inputTextRT = inputTextGO.AddComponent<RectTransform>();
            inputTextRT.anchorMin = Vector2.zero; inputTextRT.anchorMax = Vector2.one;
            inputTextRT.offsetMin = inputTextRT.offsetMax = Vector2.zero;
            var inputTmp = inputTextGO.AddComponent<TextMeshProUGUI>();
            inputTmp.fontSize = 18f;
            inputTmp.color = Color.white;
            inputTmp.enableWordWrapping = false;
            inputTmp.overflowMode = TextOverflowModes.Overflow;

            // TMP_InputField added AFTER children so component can find them
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport   = textAreaRT;   // required — prevents the OnDrag NullRef
            field.textComponent  = inputTmp;
            field.placeholder    = phTmp;
        }

        private static void AddLabel(Transform parent, string goName, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize = 22f)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
        }


        // ─────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────
        private static HashSet<ScreensType> FindAllMenuScreenTypes()
        {
            var set = new HashSet<ScreensType>();
            foreach (var s in FindObjectsOfType<MenuScreen>(true))
                set.Add(s.screenType);
            return set;
        }

        private void Log(string msg)
        {
            _log += msg + "\n";
            Debug.Log($"[LobbySetupWizard] {msg}");
            Repaint();
        }
    }
}
