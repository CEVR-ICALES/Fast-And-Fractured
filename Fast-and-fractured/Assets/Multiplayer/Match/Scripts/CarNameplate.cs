using System.Collections.Generic;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private const float NEAR_PERCENT_SIZE = 42f;
        private const float FAR_PERCENT_SIZE = 26f;
        private const float PERCENT_ROW_HEIGHT = 1.1f;
        private const float FALLBACK_PERCENT_FONT_SIZE = 54f;
        private const float ATTACH_CHECK_SECONDS = 0.5f;
        private const float ATTACH_GIVE_UP_SECONDS = 60f;
        private static readonly Vector2 CENTRE = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BOTTOM_CENTRE = new Vector2(0.5f, 0f);
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
        private RectTransform _nameRect;
        private RectTransform _percentHolder;
        private DamagePercentText _percent;
        private float _percentFontSize = FALLBACK_PERCENT_FONT_SIZE;
        private float _shownDamage = float.NaN;
        private bool _everShown;
        private float _nextNameRefresh;
        private float _height = 3.5f;
        private bool _isHuman;
        private bool _isLocalCar;
        private static TMP_FontAsset _font;
        private static DamagePercentText _hudPercent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            WatchScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => WatchScene(scene);

        private static void WatchScene(Scene scene)
        {
            if (!scene.IsValid() || scene.buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX) return;
            var go = new GameObject("[CarNameplateAttacher]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Attacher>();
        }

        // A single player match has no network wrapper to call AttachAll: wait for its characters.
        private sealed class Attacher : MonoBehaviour
        {
            private float _next;
            private float _giveUp;

            private void OnEnable() => _giveUp = Time.unscaledTime + ATTACH_GIVE_UP_SECONDS;

            private void Update()
            {
                float now = Time.unscaledTime;
                if (now < _next) return;
                _next = now + ATTACH_CHECK_SECONDS;
                LevelController level = LevelController.ExistingInstance;
                bool ready = level != null && level.LocalPlayer != null && level.InGameCharacters.Count > 0;
                if (ready) AttachAll(level.InGameCharacters);
                if (ready || now > _giveUp) Destroy(gameObject);
            }
        }

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
            textRect.anchorMin = textRect.anchorMax = textRect.pivot = BOTTOM_CENTRE;
            textRect.sizeDelta = NAMEPLATE_SIZE;
            _nameRect = textRect;
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

            _percent = CreateDamagePercent(rect, HudDamagePercent(), HudFont());
            _percentHolder = (RectTransform)_percent.transform.parent;
            _percentFontSize = Mathf.Max(1f, _percent.GetComponent<TMP_Text>().fontSize);
            _shownDamage = DamageOf(_stats);
            _percent.SnapTo(_shownDamage);
            RefreshName();
        }

        public static DamagePercentText CreateDamagePercent(RectTransform plate, DamagePercentText hudPercent, TMP_FontAsset fallbackFont)
        {
            // Inactive while it is laid out: DamagePercentText keeps the position and scale it wakes up with.
            var holderGo = new GameObject("Damage", typeof(RectTransform));
            holderGo.SetActive(false);
            var holder = (RectTransform)holderGo.transform;
            holder.SetParent(plate, false);
            holder.anchorMin = holder.anchorMax = holder.pivot = BOTTOM_CENTRE;
            holder.anchoredPosition = Vector2.zero;

            GameObject textGo;
            TextMeshProUGUI created = null;
            if (hudPercent != null)
            {
                textGo = Instantiate(hudPercent.gameObject, holder);
            }
            else
            {
                textGo = new GameObject("Percent", typeof(RectTransform));
                textGo.transform.SetParent(holder, false);
                created = textGo.AddComponent<TextMeshProUGUI>();
                if (fallbackFont != null) created.font = fallbackFont;
                created.fontSize = FALLBACK_PERCENT_FONT_SIZE;
                textGo.AddComponent<DamagePercentText>();
            }
            textGo.name = "Percent";
            textGo.SetActive(true);

            var text = textGo.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            float rowHeight = text.fontSize * PERCENT_ROW_HEIGHT;
            holder.sizeDelta = new Vector2(NAMEPLATE_SIZE.x, rowHeight);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = CENTRE;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = holder.sizeDelta;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            holderGo.SetActive(true);
            if (created != null && created.fontSharedMaterial != null)
            {
                created.outlineWidth = OUTLINE_WIDTH;
                created.outlineColor = OUTLINE_COLOR;
            }
            return textGo.GetComponent<DamagePercentText>();
        }

        public static float Damage01(float damagePercent) => Mathf.Max(0f, damagePercent) / 100f;

        private static float DamageOf(StatsController stats) =>
            stats != null && stats.CharacterData != null ? Damage01(stats.DamagePercent) : 0f;

        public static float PercentScale(float farFraction, float fontSize) =>
            Mathf.Lerp(NEAR_PERCENT_SIZE, FAR_PERCENT_SIZE, farFraction) / Mathf.Max(1f, fontSize);

        public static bool IsSameCar(Transform car, Transform other) =>
            car != null && other != null && (car == other || car.IsChildOf(other) || other.IsChildOf(car));

        private static DamagePercentText HudDamagePercent()
        {
            if (_hudPercent != null) return _hudPercent;
            HUDManager hud = HUDManager.ExistingInstance;
            return _hudPercent = hud != null ? hud.GetComponentInChildren<DamagePercentText>(true) : null;
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
            if (_carNob == null)
            {
                LevelController level = LevelController.ExistingInstance;
                _isLocalCar = level != null && level.LocalPlayer != null && IsSameCar(_car, level.LocalPlayer.transform);
            }
        }

        private void LateUpdate()
        {
            if (_car == null) { Destroy(gameObject); return; }
            if (Time.unscaledTime >= _nextNameRefresh) RefreshName();

            float damage = DamageOf(_stats);
            if (!Mathf.Approximately(damage, _shownDamage))
            {
                _shownDamage = damage;
                // Nobody saw the old value: no hit or heal effect for the change.
                if (_everShown) _percent.SetDamage(damage);
                else _percent.SnapTo(damage);
            }

            var cam = Camera.main;
            bool own = _carNob != null ? _carNob.IsOwner : _isLocalCar;
            bool alive = _car.gameObject.activeInHierarchy && !NetworkedStatsController.IsDeadCar(_stats);
            if (cam == null || own || !alive)
            {
                _group.alpha = 0f;
                return;
            }
            bool named = _isHuman ? MultiplayerSettings.ShowPlayerNames : MultiplayerSettings.ShowBotNames;
            if (_text.enabled != named) _text.enabled = named;

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
            if (_group.alpha > 0f) _everShown = true;
            float far = Mathf.InverseLerp(NEAR_DISTANCE, fadeEnd, distance);
            _text.fontSize = Mathf.Lerp(NEAR_FONT_SIZE, FAR_FONT_SIZE, far);
            float percentScale = PercentScale(far, _percentFontSize);
            _percentHolder.localScale = new Vector3(percentScale, percentScale, 1f);
            _nameRect.anchoredPosition = new Vector2(0f, _percentHolder.sizeDelta.y * percentScale);
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
