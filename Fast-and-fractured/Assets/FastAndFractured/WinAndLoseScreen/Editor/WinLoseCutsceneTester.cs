using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace FastAndFractured
{
    /// <summary>
    /// Plays any character's win/lose cutscene, for any skin, through the real main menu flow (GameEndData + Play mode)
    /// without finishing a match, and opens a cutscene for editing where it plays in the menu.
    /// </summary>
    public class WinLoseCutsceneTester : EditorWindow
    {
        private const string MAIN_MENU_SCENE = "Assets/FastAndFractured/!Environment/Scenes/!MainMenuPostRelease/MainMenuFinal_PostRelease.unity";
        private const string GAME_END_DATA = "Assets/FastAndFractured/WinAndLoseScreen/ScriptableObject/GameEndData.asset";
        private const string GAME_END_DATA_DEFAULT = "Assets/FastAndFractured/WinAndLoseScreen/ScriptableObject/GameEndDataDefault.asset";
        private const string CHARACTERS_FOLDER = "Assets/FastAndFractured/Characters";
        private const string TIMELINE_WINDOW_MENU = "Window/Sequencing/Timeline";
        private const string PENDING_CODE_KEY = "WinLoseCutsceneTester.PendingCode";
        private const string PENDING_WIN_KEY = "WinLoseCutsceneTester.PendingWin";
        private const string LAUNCHED_KEY = "WinLoseCutsceneTester.Launched";
        private const string LAST_CODE_KEY = "WinLoseCutsceneTester.LastCode";
        private const string LAST_WIN_KEY = "WinLoseCutsceneTester.LastWin";
        private const string EDITING_KEY = "WinLoseCutsceneTester.EditingInstance";
        private const float BUTTON_WIDTH = 60f;
        private const float EDIT_BUTTON_WIDTH = 80f;
        private const float NAME_WIDTH = 110f;

        private List<CharacterData> _characters = new List<CharacterData>();
        private Dictionary<string, int> _skinCounts = new Dictionary<string, int>();
        private Vector2 _scroll;

        [MenuItem("Tools/CharacterRelated/Win Lose Cutscene Tester", false, 60)]
        public static void ShowWindow() => GetWindow<WinLoseCutsceneTester>("Win/Lose Cutscenes");

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static GameObject EditingInstance
        {
            get => EditorUtility.InstanceIDToObject(SessionState.GetInt(EDITING_KEY, 0)) as GameObject;
            set => SessionState.SetInt(EDITING_KEY, value != null ? value.GetInstanceID() : 0);
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            _characters = LoadCharacters();
            _skinCounts.Clear();
            var skins = Resources.Load<ListOfCharactersSkins>(LevelConstants.LIST_OF_CHARACTERS_SKINS_PATH);
            if (skins == null) return;
            for (int i = 0; i < skins.listOfCharacters.Count && i < skins.listOfCharactersSkinCount.Count; i++)
                _skinCounts[skins.listOfCharacters[i]] = skins.listOfCharactersSkinCount[i];
        }

        private static List<CharacterData> LoadCharacters()
        {
            return AssetDatabase.FindAssets("t:CharacterData", new[] { CHARACTERS_FOLDER })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(data => data != null)
                .OrderBy(data => data.CharacterName)
                .ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Win / Lose: opens the main menu and enters Play mode showing that cutscene, exactly as after a match " +
                                    "(press any key in the Game view to test the skip).\n" +
                                    "Edit: places the cutscene where it plays in the menu and opens the Timeline window; the Game view shows its camera.",
                                    MessageType.Info);

            DrawEditingPanel();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh")) Refresh();
                string last = SessionState.GetString(LAST_CODE_KEY, string.Empty);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(last)))
                {
                    bool lastWin = SessionState.GetBool(LAST_WIN_KEY, true);
                    if (GUILayout.Button(string.IsNullOrEmpty(last) ? "Replay last" : $"Replay {last} {(lastWin ? "win" : "lose")}"))
                        Launch(last, lastWin);
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (CharacterData data in _characters)
            {
                EditorGUILayout.Space();
                string baseCode = CodeFor(data, 0);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(data.CharacterName, EditorStyles.boldLabel, GUILayout.Width(NAME_WIDTH));
                    DrawEditButtons(data.GetFinalAnimation(baseCode, true), "Edit win");
                    DrawEditButtons(data.GetFinalAnimation(baseCode, false), "Edit lose");
                }
                _skinCounts.TryGetValue(data.CharacterName, out int skinCount);
                for (int skin = 0; skin <= skinCount; skin++)
                {
                    string code = CodeFor(data, skin);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(code, GUILayout.Width(NAME_WIDTH));
                        DrawLaunchButton(data, code, true);
                        DrawLaunchButton(data, code, false);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static string CodeFor(CharacterData data, int skin) =>
            data.CharacterName + LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + skin;

        private void DrawLaunchButton(CharacterData data, string code, bool isWin)
        {
            GameObject cutscene = data.GetFinalAnimation(code, isWin);
            string tooltip = cutscene != null ? AssetDatabase.GetAssetPath(cutscene) : "No cutscene assigned";
            using (new EditorGUI.DisabledScope(cutscene == null))
            {
                if (GUILayout.Button(new GUIContent(isWin ? "Win" : "Lose", tooltip), GUILayout.Width(BUTTON_WIDTH)))
                    Launch(code, isWin);
            }
        }

        private void DrawEditButtons(GameObject cutscene, string label)
        {
            using (new EditorGUI.DisabledScope(cutscene == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                string path = cutscene != null ? AssetDatabase.GetAssetPath(cutscene) : "No cutscene assigned";
                if (GUILayout.Button(new GUIContent(label, "Edit in the menu scene: " + path), GUILayout.Width(EDIT_BUTTON_WIDTH)))
                    EditInMenu(cutscene);
                if (GUILayout.Button(new GUIContent("Prefab", "Open in Prefab Mode: " + path), GUILayout.Width(BUTTON_WIDTH)))
                    OpenPrefab(cutscene);
            }
        }

        private void DrawEditingPanel()
        {
            GameObject instance = EditingInstance;
            if (instance == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Editing " + instance.name, EditorStyles.boldLabel);
                bool hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
                EditorGUILayout.LabelField(hasOverrides ? "Has unapplied changes (clip edits save to the .playable directly)." : "No object changes yet.");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select timeline")) SelectDirector(instance);
                    if (GUILayout.Button("Apply to prefab")) StopEditing(true);
                    if (GUILayout.Button("Discard")) StopEditing(false);
                }
            }
        }

        private static void EditInMenu(GameObject cutscene)
        {
            if (!StopEditingIfUserAgrees()) return;
            if (!OpenMainMenu()) return;

            WinLoseScreenBehaviour screen = Object.FindObjectsByType<WinLoseScreenBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (screen == null || screen.spawnPoint == null)
            {
                Debug.LogError("[WinLoseCutsceneTester] The main menu has no WinLoseScreenBehaviour spawn point.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(cutscene, screen.gameObject.scene);
            instance.transform.SetPositionAndRotation(screen.CutscenePosition, screen.spawnPoint.transform.rotation);
            // Never saved with the menu scene; changes go back to the prefab with Apply.
            instance.hideFlags = HideFlags.DontSaveInEditor;
            EditingInstance = instance;

            EditorApplication.ExecuteMenuItem(TIMELINE_WINDOW_MENU);
            SelectDirector(instance);
            FocusWindowIfItsOpen<WinLoseCutsceneTester>();
        }

        private static void OpenPrefab(GameObject cutscene)
        {
            if (!StopEditingIfUserAgrees()) return;
            PrefabStage stage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(cutscene));
            if (stage == null) return;
            EditorApplication.ExecuteMenuItem(TIMELINE_WINDOW_MENU);
            SelectDirector(stage.prefabContentsRoot);
        }

        private static void SelectDirector(GameObject root)
        {
            PlayableDirector director = root.GetComponentInChildren<PlayableDirector>(true);
            Selection.activeGameObject = director != null ? director.gameObject : root;
            EditorGUIUtility.PingObject(Selection.activeGameObject);
        }

        private static bool StopEditingIfUserAgrees()
        {
            GameObject instance = EditingInstance;
            if (instance == null) return true;
            if (!PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false))
            {
                StopEditing(false);
                return true;
            }
            int choice = EditorUtility.DisplayDialogComplex("Cutscene being edited",
                $"'{instance.name}' has unapplied changes.", "Apply", "Cancel", "Discard");
            if (choice == 1) return false;
            StopEditing(choice == 0);
            return true;
        }

        private static void StopEditing(bool apply)
        {
            GameObject instance = EditingInstance;
            EditingInstance = null;
            if (instance == null) return;

            // The root position at the spawn point is a default override, which Apply leaves out.
            if (apply) PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);
            Object.DestroyImmediate(instance);
        }

        private static bool OpenMainMenu()
        {
            if (EditorSceneManager.GetActiveScene().path == MAIN_MENU_SCENE) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(MAIN_MENU_SCENE, OpenSceneMode.Single);
            return true;
        }

        private static void Launch(string code, bool isWin)
        {
            if (!StopEditingIfUserAgrees()) return;
            SessionState.SetString(LAST_CODE_KEY, code);
            SessionState.SetBool(LAST_WIN_KEY, isWin);
            SessionState.SetString(PENDING_CODE_KEY, code);
            SessionState.SetBool(PENDING_WIN_KEY, isWin);

            // Restart through edit mode so the menu runs its normal start-up with the new data.
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else StartPending();
        }

        private static void StartPending()
        {
            string code = SessionState.GetString(PENDING_CODE_KEY, string.Empty);
            if (string.IsNullOrEmpty(code)) return;
            bool isWin = SessionState.GetBool(PENDING_WIN_KEY, true);
            SessionState.EraseString(PENDING_CODE_KEY);

            if (!OpenMainMenu()) return;

            LevelUtilities.ParseCharacterNameCode(code, out string characterName, out int skin);
            CharacterData data = LoadCharacters().FirstOrDefault(d => d.CharacterName == characterName);
            var gameEndData = AssetDatabase.LoadAssetAtPath<GameEndData>(GAME_END_DATA);
            if (data == null || gameEndData == null)
            {
                Debug.LogError($"[WinLoseCutsceneTester] No CharacterData for '{characterName}' or no GameEndData at {GAME_END_DATA}.");
                return;
            }

            gameEndData.isWin = isWin;
            gameEndData.finalAnimation = data.GetFinalAnimation(code, isWin);
            gameEndData.finalAnimationSkin = skin;
            gameEndData.totalDamageDealt = "0";
            gameEndData.totalDamageTaken = "0";
            gameEndData.totalDistanceTraveled = "0" + LevelConstants.METERS_TEXT;
            gameEndData.killCount = "0";

            SessionState.SetBool(LAUNCHED_KEY, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // An edited instance would be lost with the scene reload of Play mode.
            if (change == PlayModeStateChange.ExitingEditMode && EditingInstance != null && !StopEditingIfUserAgrees())
                EditorApplication.isPlaying = false;

            if (change != PlayModeStateChange.EnteredEditMode) return;

            // A cutscene stopped half-way leaves GameEndData set, which would replay it on the next normal Play.
            if (SessionState.GetBool(LAUNCHED_KEY, false))
            {
                SessionState.SetBool(LAUNCHED_KEY, false);
                RestoreGameEndData();
            }
            StartPending();
        }

        private static void RestoreGameEndData()
        {
            var gameEndData = AssetDatabase.LoadAssetAtPath<GameEndData>(GAME_END_DATA);
            var defaults = AssetDatabase.LoadAssetAtPath<GameEndData>(GAME_END_DATA_DEFAULT);
            if (gameEndData == null || defaults == null) return;
            gameEndData.isWin = defaults.isWin;
            gameEndData.totalDamageDealt = defaults.totalDamageDealt;
            gameEndData.totalDamageTaken = defaults.totalDamageTaken;
            gameEndData.totalDistanceTraveled = defaults.totalDistanceTraveled;
            gameEndData.finalAnimation = defaults.finalAnimation;
            gameEndData.finalAnimationSkin = defaults.finalAnimationSkin;
            gameEndData.sceneBuildIndex = defaults.sceneBuildIndex;
            gameEndData.killCount = defaults.killCount;
        }
    }
}
