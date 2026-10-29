using System.Collections.Generic;
using FishNet.Object;
using TMPro;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The driver's name above each other alive car in a multiplayer match, fading with distance. It follows the
    /// car's model smoothed per frame (cars move on ticks) and runs after CinemachineBrain.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CarNameplate : MonoBehaviour
    {
        // Above the top of the car and its driver, measured from the car's renderers.
        private const float CLEARANCE = 0.7f;
        private const float MIN_HEIGHT = 2f;
        private const float MAX_HEIGHT = 7f;
        // On a screen overlay so nothing covers it; a little smaller far away.
        private const float NEAR_FONT_SIZE = 34f;
        private const float FAR_FONT_SIZE = 22f;
        private const float NEAR_DISTANCE = 15f;
        private const int SORTING_ORDER = -10;   // under the match HUD
        private const float OUTLINE_WIDTH = 0.25f;
        private const float MATCH_WIDTH_OR_HEIGHT = 0.5f;
        private static readonly Vector2 NAMEPLATE_SIZE = new Vector2(420f, 50f);
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);
        private static readonly Color32 OUTLINE_COLOR = new Color32(20, 16, 24, 255);
        // Fades out towards Settings > Multiplayer > name distance.
        private const float FADE_START_FRACTION = 0.65f;
        private const float NAME_REFRESH_SECONDS = 2f;
        private static readonly Color HumanColor = Color.white;
        private static readonly Color BotColor = new Color(0.80f, 0.86f, 0.92f, 1f);
        private const float SMOOTH_TIME = 0.05f;
        private const float TELEPORT_DISTANCE = 5f;

        private Transform _car;
        private Transform _point;        // the car's Visuals (or the car), followed smoothly
        private Vector3 _smoothed;
        private Vector3 _smoothVelocity;
        private Vector3 _lastPoint;
        private Vector3 _pointVelocity;
        private StatsController _stats;
        private NetworkObject _carNob;
        private CanvasGroup _group;
        private TextMeshProUGUI _text;
        private float _nextNameRefresh;
        private float _height = 3.5f;
        private bool _isHuman;
        private static TMP_FontAsset _font;

        /// <summary>One nameplate per character of the match (idempotent).</summary>
        public static void AttachAll(IEnumerable<GameObject> characters)
        {
            if (characters == null) return;
            foreach (var character in characters)
            {
                if (character == null) continue;
                var stats = character.GetComponent<StatsController>();
                if (stats == null) stats = character.GetComponentInChildren<StatsController>(true);
                if (stats == null || stats.GetComponent<CarNameplateLink>() != null) continue;
                stats.gameObject.AddComponent<CarNameplateLink>();

                var go = new GameObject($"Nameplate ({stats.gameObject.name})", typeof(RectTransform));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, stats.gameObject.scene);
                go.AddComponent<CarNameplate>().Bind(stats);
            }
        }

        private sealed class CarNameplateLink : MonoBehaviour { }

        private void Bind(StatsController stats)
        {
            _stats = stats;
            _car = stats.transform;
            _carNob = stats.GetComponentInParent<NetworkObject>();
            _height = MeasureHeight(_car);
            _point = NetworkedSpectatorController.SmoothedPointOf(_car);
            if (_point == _car && _carNob != null) _point = NetworkedSpectatorController.SmoothedPointOf(_carNob.transform);
            _smoothed = _lastPoint = _point.position;

            var rect = (RectTransform)transform;
            rect.SetParent(SharedCanvas(stats.gameObject.scene), false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = NAMEPLATE_SIZE;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var textGo = new GameObject("Name", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            _text = textGo.AddComponent<TextMeshProUGUI>();
            var font = HudFont();
            if (font != null) _text.font = font;
            _text.fontSize = NEAR_FONT_SIZE;
            _text.alignment = TextAlignmentOptions.Center;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Ellipsis;
            _text.raycastTarget = false;
            _text.outlineWidth = OUTLINE_WIDTH;
            _text.outlineColor = OUTLINE_COLOR;
            RefreshName();
        }

        private static Canvas _canvas;

        /// <summary>Height of the car's top (driver included) above its pivot, plus a margin.</summary>
        private static float MeasureHeight(Transform car)
        {
            bool any = false;
            float top = car.position.y;
            foreach (var renderer in car.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                top = any ? Mathf.Max(top, renderer.bounds.max.y) : renderer.bounds.max.y;
                any = true;
            }
            return Mathf.Clamp(top - car.position.y + CLEARANCE, MIN_HEIGHT, MAX_HEIGHT);
        }

        /// <summary>One overlay canvas for all the nameplates, living in the map scene.</summary>
        private static Transform SharedCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            if (_canvas != null) return _canvas.transform;
            var go = new GameObject("[CarNameplates]", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORTING_ORDER;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            scaler.matchWidthOrHeight = MATCH_WIDTH_OR_HEIGHT;
            return go.transform;
        }

        /// <summary>The match HUD's display font (Alfa Slab One), if it is loaded.</summary>
        private static TMP_FontAsset HudFont()
        {
            if (_font != null) return _font;
            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (asset.name.IndexOf("AlfaSlab", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return _font = asset;
            return null;
        }

        private void RefreshName()
        {
            _nextNameRefresh = Time.unscaledTime + NAME_REFRESH_SECONDS;
            bool human = MatchNames.TryGetOwner(_car, out _);
            _isHuman = human;
            _text.text = MatchNames.Of(_car.gameObject) ?? string.Empty;
            _text.color = human ? HumanColor : BotColor;
        }

        private void LateUpdate()
        {
            if (_car == null) { Destroy(gameObject); return; }
            if (Time.unscaledTime >= _nextNameRefresh) RefreshName();

            var cam = Camera.main;
            bool own = _carNob != null && _carNob.IsOwner;
            bool alive = _car.gameObject.activeInHierarchy && (_stats == null || _stats.Endurance > 0f);
            bool wanted = _isHuman ? MultiplayerSettings.ShowPlayerNames : MultiplayerSettings.ShowBotNames;
            if (cam == null || own || !alive || !wanted)
            {
                _group.alpha = 0f;
                return;
            }

            Vector3 position = SmoothedPoint() + Vector3.up * _height;
            Vector3 screen = cam.WorldToScreenPoint(position);
            float distance = Vector3.Distance(position, cam.transform.position);
            if (screen.z <= 0f)
            {
                _group.alpha = 0f;   // behind the camera
                return;
            }
            float fadeEnd = MultiplayerSettings.NameTagDistance;
            _group.alpha = 1f - Mathf.InverseLerp(fadeEnd * FADE_START_FRACTION, fadeEnd, distance);
            transform.position = new Vector3(screen.x, screen.y, 0f);
            _text.fontSize = Mathf.Lerp(NEAR_FONT_SIZE, FAR_FONT_SIZE, Mathf.InverseLerp(NEAR_DISTANCE, fadeEnd, distance));
        }

        /// <summary>The followed point smoothed per frame; a jump its own motion does not explain snaps.</summary>
        private Vector3 SmoothedPoint()
        {
            Transform point = _point != null ? _point : _car;
            Vector3 target = point.position;
            float deltaTime = Time.deltaTime;
            Vector3 expected = _lastPoint + _pointVelocity * deltaTime;
            bool teleported = (target - expected).sqrMagnitude > TELEPORT_DISTANCE * TELEPORT_DISTANCE;
            _pointVelocity = teleported || deltaTime <= 0f ? Vector3.zero : (target - _lastPoint) / deltaTime;
            _lastPoint = target;
            if (teleported)
            {
                _smoothed = target;
                _smoothVelocity = Vector3.zero;
            }
            else
            {
                _smoothed = Vector3.SmoothDamp(_smoothed, target, ref _smoothVelocity, SMOOTH_TIME, Mathf.Infinity, deltaTime);
            }
            return _smoothed;
        }
    }
}
