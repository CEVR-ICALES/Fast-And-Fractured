using System.Collections.Generic;
using System.Linq;
using Enums;
using FastAndFractured.Progression;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.EditorTools
{
    /// <summary>
    /// Restructures the main menu scene into Play > Singleplayer / Multiplayer screens and adds the progression and
    /// accessories screens. Every change is undoable, and Revert undoes them in a saved scene.
    /// </summary>
    public static class MainMenuPlayModesTool
    {
        private const string MENU = "Tools/Fast And Fractured/Main Menu/";
        private const string PLAY_MODE_NAME = "PlayModeScreen";
        private const string PROGRESSION_NAME = "Screen_Progression";
        private const string ACCESSORIES_NAME = "Screen_Accessories";
        private const string PROGRESS_BUTTON = "ProgressButton";
        private const string ACCESSORIES_BUTTON = "Accessories_Button";
        private const string COMING_SOON = "ComingSoon_Button";
        private const float BUTTON_GAP_RATIO = 0.02f;
        private const float MIN_AUTO_FONT_SIZE = 10f;
        private const float MIN_AUTO_FONT_RATIO = 0.4f;
        private const float DEFAULT_ROW_SPACING = 60f;
        private static readonly Vector2 DEFAULT_ROW_STEP = new Vector2(0f, -0.15f);
        private const string GAMEMODE_CAMERA = "GameModeScreenCamera";

        [MenuItem(MENU + "Apply Singleplayer / Multiplayer menu")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            var main = FindScreen(scene, ScreensType.MAIN_MENU);
            var modes = FindScreen(scene, ScreensType.GAMEMODE_SELECTION);
            var hub = FindScreen(scene, ScreensType.MULTIPLAYER_HUB);
            if (main == null || modes == null || hub == null)
            {
                EditorUtility.DisplayDialog("Main menu", "Open the main menu scene first (MAIN_MENU, GAMEMODE_SELECTION and MULTIPLAYER_HUB screens are needed).", "OK");
                return;
            }
            if (FindScreen(scene, ScreensType.PLAY_MODE_SELECTION) != null)
            {
                EditorUtility.DisplayDialog("Main menu", "Already applied. Use Revert first to apply it again.", "OK");
                return;
            }
            Undo.SetCurrentGroupName("Singleplayer / Multiplayer menu");
            int group = Undo.GetCurrentGroup();

            // 1. The main screen: no MULTIPLAYER button, Play goes to the new screen.
            var multiplayerButton = FindChild(main.transform, "MultiplayerButton");
            if (multiplayerButton != null)
            {
                Undo.RecordObject(multiplayerButton.gameObject, "Hide Multiplayer");
                multiplayerButton.gameObject.SetActive(false);
            }
            var play = main.GetComponentsInChildren<MenuTransitionButton>(true).FirstOrDefault(b => b.screenToOpen == ScreensType.GAMEMODE_SELECTION);
            if (play != null) SetTarget(play, ScreensType.PLAY_MODE_SELECTION);

            // 2. The new Singleplayer / Multiplayer screen: a copy of the mode screen.
            var playModes = Duplicate(modes.gameObject, PLAY_MODE_NAME);
            var playScreen = playModes.GetComponent<MenuScreen>();
            playScreen.screenType = ScreensType.PLAY_MODE_SELECTION;

            // Both screens bring their camera: they can be reached without the main screen's Play button.
            var modesCamera = FindCamera(scene, GAMEMODE_CAMERA);
            if (modesCamera != null)
            {
                Undo.RecordObject(modes, "Screen camera");
                modes.screenCamera = modesCamera;
                playScreen.screenCamera = modesCamera;
            }
            var single = FindChild(playModes.transform, "Tutorial_Button");
            var multi = FindChild(playModes.transform, "Game_Button");
            if (single != null)
            {
                single.name = "Singleplayer_Button";
                SetLabel(single, "Singleplayer");
                Silence(single.GetComponent<Button>());
                var open = single.gameObject.AddComponent<MenuTransitionButton>();
                open.screenToOpen = ScreensType.GAMEMODE_SELECTION;
                UnityEventTools.AddPersistentListener(single.GetComponent<Button>().onClick, open.OpenScreen);
                playScreen.defaultInteractable = single.GetComponent<Button>();
            }
            if (multi != null)
            {
                multi.name = "Multiplayer_Button";
                SetLabel(multi, "Multiplayer");
                var button = multi.GetComponent<Button>();
                Silence(button);
                var open = multi.GetComponent<MenuTransitionButton>() ?? multi.gameObject.AddComponent<MenuTransitionButton>();
                open.screenToOpen = ScreensType.MULTIPLAYER_HUB;
                UnityEventTools.AddPersistentListener(button.onClick, open.OpenScreen);
            }

            // 3. The old mode screen becomes Singleplayer, with room for four modes.
            var back = FindChild(modes.transform, "BackToMain_Btn");
            if (back != null)
            {
                var transition = back.GetComponent<MenuTransitionButton>();
                if (transition != null) SetTarget(transition, ScreensType.PLAY_MODE_SELECTION);
                SetCameraCalls(back.GetComponent<Button>(), false);   // the new screen uses this camera too
            }
            var layoutRoot = FindChild(modes.transform, "GameMode_HLayout");
            var showdown = FindChild(modes.transform, "Game_Button");
            if (layoutRoot != null && showdown != null)
            {
                var soon = Object.Instantiate(showdown.gameObject, layoutRoot);
                Undo.RegisterCreatedObjectUndo(soon, "Coming soon");
                soon.name = $"{COMING_SOON}_1";
                SetLabel(soon.transform, "Coming soon");
                var soonButton = soon.GetComponent<Button>();
                Silence(soonButton);
                soonButton.interactable = false;
                Object.DestroyImmediate(soon.GetComponent<MenuTransitionButton>());

                var accessories = Object.Instantiate(showdown.gameObject, layoutRoot);
                Undo.RegisterCreatedObjectUndo(accessories, "Accessories button");
                accessories.name = ACCESSORIES_BUTTON;
                SetLabel(accessories.transform, "Accessories");
                Silence(accessories.GetComponent<Button>());
                Object.DestroyImmediate(accessories.GetComponent<MenuTransitionButton>());
                accessories.AddComponent<AccessoriesEntryButton>().returnTo = ScreensType.GAMEMODE_SELECTION;

                MakeGrid(layoutRoot);
                foreach (var label in layoutRoot.GetComponentsInChildren<TMP_Text>(true)) AutoSize(label);
            }

            // 4. Progress and Accessories from the multiplayer hub, side by side in one more row.
            var browse = FindChild(hub.transform, "BrowseButton");
            var direct = FindChild(hub.transform, "DirectJoinButton");
            if (direct != null)
            {
                var directRect = (RectTransform)direct;
                Vector2 step = browse != null ? directRect.anchorMin - ((RectTransform)browse).anchorMin : DEFAULT_ROW_STEP;
                Vector2 min = directRect.anchorMin + step, max = directRect.anchorMax + step;
                float gap = (max.x - min.x) * BUTTON_GAP_RATIO, middle = (min.x + max.x) / 2f;

                var progress = Object.Instantiate(direct.gameObject, hub.transform);
                Undo.RegisterCreatedObjectUndo(progress, "Progress button");
                progress.name = PROGRESS_BUTTON;
                SetLabel(progress.transform, "PROGRESS");
                var rect = (RectTransform)progress.transform;
                rect.anchorMin = min;
                rect.anchorMax = new Vector2(middle - gap, max.y);
                var open = progress.AddComponent<MenuTransitionButton>();
                open.screenToOpen = ScreensType.PROGRESSION;
                UnityEventTools.AddPersistentListener(progress.GetComponent<Button>().onClick, open.OpenScreen);

                var accessories = Object.Instantiate(direct.gameObject, hub.transform);
                Undo.RegisterCreatedObjectUndo(accessories, "Accessories button");
                accessories.name = ACCESSORIES_BUTTON;
                SetLabel(accessories.transform, "ACCESSORIES");
                rect = (RectTransform)accessories.transform;
                rect.anchorMin = new Vector2(middle + gap, min.y);
                rect.anchorMax = max;
                accessories.AddComponent<AccessoriesEntryButton>().returnTo = ScreensType.MULTIPLAYER_HUB;
            }

            // 5. The progression and accessories screens, in the hub's look.
            CreateScreen(hub, PROGRESSION_NAME, ScreensType.PROGRESSION, typeof(ProgressionScreen));
            CreateScreen(hub, ACCESSORIES_NAME, ScreensType.ACCESSORIES, typeof(AccessoriesScreen));

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            FastAndFractured.Multiplayer.Editor.LocalizeSceneTextsTool.Localize(scene);   // the labels it wrote
            Debug.Log("[MainMenuPlayModesTool] Applied. Save the scene to keep it.");
        }

        [MenuItem(MENU + "Revert Singleplayer / Multiplayer menu")]
        public static void Revert()
        {
            var scene = SceneManager.GetActiveScene();
            var main = FindScreen(scene, ScreensType.MAIN_MENU);
            var modes = FindScreen(scene, ScreensType.GAMEMODE_SELECTION);
            var hub = FindScreen(scene, ScreensType.MULTIPLAYER_HUB);
            if (main == null || modes == null)
            {
                EditorUtility.DisplayDialog("Main menu", "Open the main menu scene first.", "OK");
                return;
            }
            Undo.SetCurrentGroupName("Revert Singleplayer / Multiplayer menu");
            int group = Undo.GetCurrentGroup();

            var multiplayerButton = FindChild(main.transform, "MultiplayerButton");
            if (multiplayerButton != null)
            {
                Undo.RecordObject(multiplayerButton.gameObject, "Show Multiplayer");
                multiplayerButton.gameObject.SetActive(true);
            }
            var play = main.GetComponentsInChildren<MenuTransitionButton>(true).FirstOrDefault(b => b.screenToOpen == ScreensType.PLAY_MODE_SELECTION);
            if (play != null) SetTarget(play, ScreensType.GAMEMODE_SELECTION);

            if (modes.screenCamera != null && modes.screenCamera.name == GAMEMODE_CAMERA)
            {
                Undo.RecordObject(modes, "Screen camera");
                modes.screenCamera = null;
            }
            var back = FindChild(modes.transform, "BackToMain_Btn");
            if (back != null)
            {
                var transition = back.GetComponent<MenuTransitionButton>();
                if (transition != null) SetTarget(transition, ScreensType.MAIN_MENU);
                SetCameraCalls(back.GetComponent<Button>(), true);
            }
            var layoutRoot = FindChild(modes.transform, "GameMode_HLayout");
            if (layoutRoot != null)
            {
                foreach (Transform child in layoutRoot.Cast<Transform>().ToList())
                    if (child.name.StartsWith(COMING_SOON)) Undo.DestroyObjectImmediate(child.gameObject);
                MakeRow(layoutRoot);
            }

            foreach (var type in new[] { ScreensType.PLAY_MODE_SELECTION, ScreensType.PROGRESSION, ScreensType.ACCESSORIES })
            {
                var screen = FindScreen(scene, type);
                if (screen != null) Undo.DestroyObjectImmediate(screen.gameObject);
            }
            foreach (var root in new[] { hub != null ? hub.transform : null, modes.transform })
                foreach (var name in new[] { PROGRESS_BUTTON, ACCESSORIES_BUTTON })
                {
                    var added = root != null ? FindChild(root, name) : null;
                    if (added != null) Undo.DestroyObjectImmediate(added.gameObject);
                }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("[MainMenuPlayModesTool] Reverted. Save the scene to keep it.");
        }

        private static MenuScreen FindScreen(Scene scene, ScreensType type)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var screen in root.GetComponentsInChildren<MenuScreen>(true))
                    if (screen.screenType == type) return screen;
            return null;
        }

        private static GameObject FindCamera(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Trim() == name) return t;
            return null;
        }

        private static GameObject Duplicate(GameObject source, string name)
        {
            var copy = Object.Instantiate(source, source.transform.parent);
            Undo.RegisterCreatedObjectUndo(copy, name);
            copy.name = name;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            return copy;
        }

        private static void CreateScreen(MenuScreen template, string name, ScreensType type, System.Type content)
        {
            var copy = Duplicate(template.gameObject, name);
            foreach (Transform child in copy.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
            foreach (var behaviour in copy.GetComponents<MonoBehaviour>())
                if (behaviour is not MenuScreen && behaviour is not Graphic &&behaviour.GetType().Name != "EnsureMenuScreenVisible")
                    Object.DestroyImmediate(behaviour);
            var screen = copy.GetComponent<MenuScreen>();
            screen.screenType = type;
            screen.backButton = null;
            screen.defaultInteractable = null;
            copy.AddComponent(content);
        }

        private static void SetTarget(MenuTransitionButton button, ScreensType target)
        {
            Undo.RecordObject(button, "Menu target");
            button.screenToOpen = target;
            EditorUtility.SetDirty(button);
        }

        private static void SetLabel(Transform root, string text)
        {
            var label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            foreach (var behaviour in label.GetComponents<MonoBehaviour>())
                if (behaviour.GetType().Name == "LocalizedText") Object.DestroyImmediate(behaviour);
            Undo.RecordObject(label, "Label");
            label.text = text;
        }

        private static void AutoSize(TMP_Text label)
        {
            Undo.RecordObject(label, "Auto size");
            if (!label.enableAutoSizing)
            {
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = Mathf.Max(MIN_AUTO_FONT_SIZE, label.fontSize * MIN_AUTO_FONT_RATIO);
                label.enableAutoSizing = true;
            }
        }

        /// <summary>Turns off every persistent listener (the copies keep the original's calls).</summary>
        private static void Silence(Button button)
        {
            if (button == null) return;
            Undo.RecordObject(button, "Listeners");
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }

        private static void SetCameraCalls(Button button, bool on)
        {
            if (button == null) return;
            Undo.RecordObject(button, "Camera calls");
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == nameof(GameObject.SetActive))
                    button.onClick.SetPersistentListenerState(i, on ? UnityEventCallState.RuntimeOnly : UnityEventCallState.Off);
            EditorUtility.SetDirty(button);
        }

        private static void MakeGrid(Transform layoutRoot)
        {
            var row = layoutRoot.GetComponent<HorizontalLayoutGroup>();
            var rect = (RectTransform)layoutRoot;
            float spacingX = row != null ? row.spacing : DEFAULT_ROW_SPACING;
            if (row != null) Undo.DestroyObjectImmediate(row);
            var grid = Undo.AddComponent<GridLayoutGroup>(layoutRoot.gameObject);
            const float spacingY = 20f;
            grid.spacing = new Vector2(spacingX, spacingY);
            grid.cellSize = new Vector2((rect.rect.width - spacingX) / 2f, (rect.rect.height - spacingY) / 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        private static void MakeRow(Transform layoutRoot)
        {
            var grid = layoutRoot.GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            float spacing = grid.spacing.x;
            Undo.DestroyObjectImmediate(grid);
            var row = Undo.AddComponent<HorizontalLayoutGroup>(layoutRoot.gameObject);
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
        }
    }
}
