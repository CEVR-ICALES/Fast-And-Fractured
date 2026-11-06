using Assets.SimpleLocalization.Scripts;
using System.Collections.Generic;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Chase camera of a player watching a match from the room (no car, no rig). TAB / click / A: next car;
    /// right mouse or stick: look around; Esc / B: back to the room.
    /// </summary>
    public sealed class MatchSpectatorCamera : MonoBehaviour
    {
        private const float DISTANCE = 12f;
        private const float HEIGHT = 5f;
        private const float LOOK_HEIGHT = 1.2f;
        private const float FOLLOW_SMOOTH = 0.18f;
        private const float ORBIT_SPEED = 120f;     // degrees per second at full stick
        private const float MOUSE_ORBIT = 0.25f;    // degrees per pixel
        private const float MIN_HEADING_SPEED_SQR = 4f;
        private const float MIN_HEADING_SQR = 0.01f;
        private const float TARGET_REFRESH_SECONDS = 0.5f;
        private const float CAMERA_DEPTH = 50f;
        private const float NEAR_CLIP = 0.3f;
        private const float FAR_CLIP = 3000f;
        private const int SORTING_ORDER = 30;
        private const float MATCH_WIDTH_OR_HEIGHT = 0.5f;
        private const float FONT_SIZE = 28f;
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);
        private static readonly Color BAR_COLOR = new Color(0f, 0f, 0f, 0.55f);

        private static MatchSpectatorCamera _instance;

        /// <summary>This client is watching a match, not playing it.</summary>
        public static bool IsSpectating => _instance != null;

        private readonly List<GameObject> _cast = new List<GameObject>();
        private readonly List<Transform> _alive = new List<Transform>();
        private Transform _target;
        private Camera _camera;
        private Vector3 _velocity;
        private float _orbitYaw;
        private TMP_Text _label;
        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public static void Begin(List<GameObject> cast)
        {
            if (_instance != null) return;
            var go = new GameObject("[MatchSpectatorCamera]");   // in the map scene: unloads with it
            _instance = go.AddComponent<MatchSpectatorCamera>();
            _instance._cast.AddRange(cast);
            _instance.Build();
            _instance.NextTarget();
        }

        public static void End()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            // The only camera and listener this client has in the map.
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            _camera = gameObject.AddComponent<Camera>();
            _camera.depth = CAMERA_DEPTH;
            _camera.nearClipPlane = NEAR_CLIP;
            _camera.farClipPlane = FAR_CLIP;
            gameObject.AddComponent<AudioListener>();
            gameObject.tag = "MainCamera";

            var canvasGo = new GameObject("SpectatorUI", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            scaler.matchWidthOrHeight = MATCH_WIDTH_OR_HEIGHT;

            var bar = new GameObject("Bar", typeof(RectTransform));
            var barRect = (RectTransform)bar.transform;
            barRect.SetParent(canvasGo.transform, false);
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 40f);
            barRect.sizeDelta = new Vector2(1100f, 64f);
            var background = bar.AddComponent<Image>();
            background.color = BAR_COLOR;
            background.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(barRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = FONT_SIZE;
            _label.alignment = TextAlignmentOptions.Center;
            _label.richText = true;
            _label.raycastTarget = false;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var mouse = Mouse.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame)
            {
                MultiplayerRoomReturn.LeaveMatchToRoom();
                enabled = false;
                return;
            }
            if (kb != null && kb.tabKey.wasPressedThisFrame || mouse != null && mouse.leftButton.wasPressedThisFrame ||
                pad != null && pad.buttonSouth.wasPressedThisFrame)
                NextTarget();

            if (mouse != null && mouse.rightButton.isPressed) _orbitYaw += mouse.delta.ReadValue().x * MOUSE_ORBIT;
            if (pad != null) _orbitYaw += pad.rightStick.ReadValue().x * ORBIT_SPEED * Time.unscaledDeltaTime;

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + TARGET_REFRESH_SECONDS;
                RefreshAlive();
                if (_target == null || !_alive.Contains(_target)) NextTarget();
                UpdateLabel();
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            var body = _target.GetComponentInParent<Rigidbody>();
            Vector3 heading = body != null && body.linearVelocity.sqrMagnitude > MIN_HEADING_SPEED_SQR ? body.linearVelocity : _target.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < MIN_HEADING_SQR) heading = Vector3.forward;
            heading = Quaternion.Euler(0f, _orbitYaw, 0f) * heading.normalized;

            // The interpolated model, not the car root, which only moves on physics steps.
            Vector3 point = NetworkedSpectatorController.SmoothedPointOf(_target).position;
            Vector3 wanted = point - heading * DISTANCE + Vector3.up * HEIGHT;
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref _velocity, FOLLOW_SMOOTH, Mathf.Infinity, Time.unscaledDeltaTime);
            transform.rotation = Quaternion.LookRotation(point + Vector3.up * LOOK_HEIGHT - transform.position);
        }

        private void RefreshAlive()
        {
            _alive.Clear();
            foreach (var character in _cast)
            {
                if (character == null) continue;
                var stats = character.GetComponent<StatsController>();
                if (stats == null) stats = character.GetComponentInChildren<StatsController>(true);
                if (stats == null || !stats.gameObject.activeInHierarchy || stats.Endurance <= 0f) continue;
                _alive.Add(stats.transform);
            }
        }

        private void NextTarget()
        {
            RefreshAlive();
            if (_alive.Count == 0) { _target = null; UpdateLabel(); return; }
            int index = _target != null ? _alive.IndexOf(_target) : -1;
            bool first = _target == null;
            _target = _alive[(index + 1) % _alive.Count];
            if (first) transform.position = _target.position - _target.forward * DISTANCE + Vector3.up * HEIGHT;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            string who = _target != null ? MatchNames.Of(_target.gameObject) : "-";
            _label.text = LocalizationManager.Localize("MP.Spectate.Watching", who, _alive.Count);
        }
    }
}
