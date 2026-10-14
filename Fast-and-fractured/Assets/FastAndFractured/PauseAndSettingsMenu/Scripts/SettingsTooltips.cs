using System.Collections.Generic;
using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FastAndFractured
{
    /// <summary>
    /// Tooltips for the settings: hovering a row (or selecting it with the gamepad / keyboard)
    /// shows what that setting does, next to the pointer or under the row. Works for every settings
    /// menu - main menu, pause menu, tabs added at runtime - without touching them: a row is any
    /// child of "SettingsContainer/&lt;tab&gt;/Viewport/Content", and its name gives the text key.
    ///
    /// Texts: the "Settings" localization sheet, keys "Settings.Tooltip.&lt;RowName&gt;" (see KeyFor).
    /// The remapping rows reuse the controls' descriptions (Controls sheet).
    /// </summary>
    public sealed class SettingsTooltips : MonoBehaviour
    {
        private const string KEY_PREFIX = "Settings.Tooltip.";
        private const string HINT_KEY = "Settings.Tooltip.RemapHint";
        private const float HOVER_DELAY = 0.35f;
        private const float SELECT_DELAY = 0.15f;
        private const float FADE_TIME = 0.12f;
        private const float MAX_WIDTH = 460f;
        private const float FONT_SIZE = 28f;
        private const float CHECK_SETTINGS_EVERY = 0.5f;
        private static readonly Vector2 PointerOffset = new Vector2(24f, -24f);

        /// <summary>Remapping rows → the game's own description of that control.</summary>
        private static readonly Dictionary<string, string> ControlDescriptions = new Dictionary<string, string>
        {
            ["Accelerate/Forward"] = "Controls.Forward",
            ["Deaccelerate / Backwards"] = "Controls.Backward",
            ["TurnRight"] = "Controls.Right",
            ["TrunLeft"] = "Controls.Left",
            ["Shoot"] = "Controls.Shoot",
            ["ShootingMode"] = "Controls.Mode",
            ["HandBrake"] = "Controls.Brake",
            ["Dash"] = "Controls.Dash",
            ["SpecialAbility"] = "Controls.Ultimate",
            ["Mine"] = "Controls.Mine",
        };

        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private Canvas _canvas;
        private RectTransform _box;
        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private LayoutElement _labelSize;

        private Transform _row;
        private bool _rowFromSelection, _shown;
        private float _rowSince, _nextSettingsCheck;
        private bool _settingsOpen;
        private Vector2 _lastMouse;
        private GameObject _lastSelected;
        private bool _navigating;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[SettingsTooltips]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<SettingsTooltips>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextSettingsCheck)
            {
                _nextSettingsCheck = Time.unscaledTime + CHECK_SETTINGS_EVERY;
                _settingsOpen = AnySettingsOpen();
            }
            var events = EventSystem.current;
            if (!_settingsOpen || events == null) { SetRow(null, false); return; }

            // Mouse moved → pointer mode; selection changed without the mouse → navigation mode.
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : _lastMouse;
            if ((mouse - _lastMouse).sqrMagnitude > 1f) _navigating = false;
            _lastMouse = mouse;
            var selected = events.currentSelectedGameObject;
            if (selected != _lastSelected)
            {
                _lastSelected = selected;
                if (Mouse.current == null || !Mouse.current.leftButton.isPressed) _navigating = selected != null;
            }

            Transform row = null;
            bool fromSelection = false;
            if (_navigating && selected != null)
            {
                row = RowOf(selected.transform);
                fromSelection = row != null;
            }
            else if (Mouse.current != null)
            {
                row = RowUnderPointer(events, mouse);
            }
            SetRow(row, fromSelection);

            if (_row != null && !_shown && Time.unscaledTime - _rowSince >= (_rowFromSelection ? SELECT_DELAY : HOVER_DELAY))
                Show();
            if (_shown)
            {
                Place(mouse);
                if (_group.alpha < 1f) _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime / FADE_TIME);
            }
        }

        // ── Which row ───────────────────────────────────────────────────────────────────
        private static bool AnySettingsOpen()
        {
            foreach (var settings in FindObjectsByType<SettingsMenuBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (settings != null && settings.isActiveAndEnabled) return true;
            return false;
        }

        private Transform RowUnderPointer(EventSystem events, Vector2 position)
        {
            _hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = position }, _hits);
            return _hits.Count > 0 && _hits[0].gameObject != null ? RowOf(_hits[0].gameObject.transform) : null;
        }

        /// <summary>The settings row containing <paramref name="t"/>: a child of SettingsContainer/&lt;tab&gt;/Viewport/Content.</summary>
        public static Transform RowOf(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                var content = t.parent;
                if (content == null || content.name != "Content") continue;
                var viewport = content.parent;
                if (viewport == null || viewport.name != "Viewport") continue;
                var tab = viewport.parent;
                if (tab != null && tab.parent != null && tab.parent.name == "SettingsContainer") return t;
            }
            return null;
        }

        private void SetRow(Transform row, bool fromSelection)
        {
            if (row == _row) return;
            _row = row;
            _rowFromSelection = fromSelection;
            _rowSince = Time.unscaledTime;
            Hide();
        }

        // ── Text ────────────────────────────────────────────────────────────────────────
        /// <summary>The tooltip of a row, in the current language; null when it has none.</summary>
        public static string TextFor(string rowName)
        {
            if (string.IsNullOrEmpty(rowName)) return null;
            if (ControlDescriptions.TryGetValue(rowName, out var controlKey))
            {
                string action = Text(controlKey);
                if (action == null) return null;
                string hint = Text(HINT_KEY);
                return hint == null ? action : $"{action}\n<size=85%><color=#FFFFFFA0>{hint}</color></size>";
            }
            return Text(KeyFor(rowName));
        }

        /// <summary>"Connection panel (F3)" → "Settings.Tooltip.ConnectionPanelF3": words in PascalCase, letters and digits only.</summary>
        public static string KeyFor(string rowName)
        {
            var key = new System.Text.StringBuilder(KEY_PREFIX);
            bool wordStart = true;
            foreach (char c in rowName)
            {
                if (!char.IsLetterOrDigit(c)) { wordStart = true; continue; }
                key.Append(wordStart ? char.ToUpperInvariant(c) : c);
                wordStart = false;
            }
            return key.ToString();
        }

        private static string Text(string key)
        {
            try
            {
                LocalizationManager.Read();
                return LocalizationManager.HasKey(key) ? LocalizationManager.Localize(key) : null;
            }
            catch { return null; }   // localization not ready: no tooltip rather than an error
        }

        // ── Box ─────────────────────────────────────────────────────────────────────────
        private void Show()
        {
            string text = TextFor(_row.name);
            var canvas = _row.GetComponentInParent<Canvas>();
            if (string.IsNullOrEmpty(text) || canvas == null) return;
            EnsureBox(canvas.rootCanvas, _row.GetComponentInChildren<TMP_Text>(true));
            _label.text = text;
            _labelSize.preferredWidth = Mathf.Min(MAX_WIDTH, _label.GetPreferredValues(text).x + 1f);
            _box.SetAsLastSibling();
            _box.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_box);
            _group.alpha = 0f;
            _shown = true;
        }

        private void Hide()
        {
            _shown = false;
            if (_box != null) _box.gameObject.SetActive(false);
        }

        private void EnsureBox(Canvas canvas, TMP_Text styleFrom)
        {
            if (_box != null && _canvas == canvas) return;
            if (_box != null) Destroy(_box.gameObject);
            _canvas = canvas;

            var go = new GameObject("SettingsTooltip", typeof(RectTransform));
            _box = (RectTransform)go.transform;
            _box.SetParent(canvas.transform, false);
            _box.pivot = new Vector2(0f, 1f);
            _box.anchorMin = _box.anchorMax = new Vector2(0.5f, 0.5f);
            var background = go.AddComponent<Image>();
            background.color = new Color(0.09f, 0.07f, 0.06f, 0.94f);
            background.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.78f, 0.35f, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _group = go.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(_box, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            if (styleFrom != null && styleFrom.font != null) _label.font = styleFrom.font;
            _label.fontSize = FONT_SIZE;
            _label.color = Color.white;
            _label.enableWordWrapping = true;
            _label.raycastTarget = false;
            _labelSize = labelGo.AddComponent<LayoutElement>();
            go.SetActive(false);
        }

        /// <summary>Next to the pointer, or under the selected row; kept inside the canvas.</summary>
        private void Place(Vector2 mouse)
        {
            var canvasRect = (RectTransform)_canvas.transform;
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 local;
            if (_rowFromSelection)
            {
                // Under the row, from its left edge: beside it the box would cover the control.
                var corners = new Vector3[4];
                ((RectTransform)_row).GetWorldCorners(corners);   // 0 = bottom left
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, camera, out local);
                local += new Vector2(24f, -4f);
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse, camera, out local);
                local += PointerOffset;
            }

            Rect bounds = canvasRect.rect;
            Vector2 size = _box.rect.size;
            const float margin = 10f;
            if (local.x + size.x > bounds.xMax - margin) local.x = Mathf.Max(bounds.xMin + margin, local.x - size.x - 2f * PointerOffset.x);
            if (local.y - size.y < bounds.yMin + margin) local.y = bounds.yMin + margin + size.y;
            if (local.y > bounds.yMax - margin) local.y = bounds.yMax - margin;
            _box.anchoredPosition = local;
        }
    }
}
