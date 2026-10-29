using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured
{
    /// <summary>
    /// Curtain between a match and the win/lose screen: two slanted halves close, the logo pops, the scene changes
    /// behind them and they open on the new scene. <c>SceneCurtain.Close(() => SceneManager.LoadScene(index));</c>
    /// Code-built on its own DontDestroyOnLoad canvas, on unscaled time.
    /// </summary>
    public sealed class SceneCurtain : MonoBehaviour
    {
        private const string STYLE_RESOURCES_PATH = "SceneCurtainStyle";

        private const float CLOSE_SECONDS = 0.42f;
        private const float POP_SECONDS = 0.34f;
        private const float CALLBACK_DELAY = 0.2f;          // logo up before the load hitch
        private const float MIN_CLOSED_SECONDS = 0.5f;
        private const float OPEN_AFTER_LOAD_SECONDS = 0.35f;
        private const float LOGO_OUT_SECONDS = 0.16f;
        private const float OPEN_SECONDS = 0.5f;
        private const float SAFETY_OPEN_SECONDS = 12f;      // a load that never came must not leave it shut
        private const float SLANT_DEGREES = 9f;
        private const float SEAM_WIDTH = 34f;
        private const float SEAM_INSET = 5f;
        private const float SHAKE_SECONDS = 0.16f;
        private static readonly Vector2 ShakeStrength = new Vector2(12f, 6f);
        private const float LOGO_WIDTH = 1000f;
        private const int SORTING_ORDER = 32000;
        private const float MATCH_WIDTH_OR_HEIGHT = 0.5f;
        private const float EDGE_CLEARANCE = 60f;
        private const float LOGO_START_SCALE = 0.35f;
        private const float LOGO_START_TILT_DEGREES = -7f;
        private const float LOGO_OUT_SCALE = 0.85f;
        private const float STRIPES_TILE_PIXELS = 96f;
        private const float SEAM_SHADOW_WIDTH = 22f;
        private const float SEAM_SHADOW_OVERLAP = 6f;
        private const int CHALK_SEED = 1337;
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);
        private static readonly Vector2 LOGO_SHADOW_OFFSET = new Vector2(9f, -9f);
        private static readonly Color STRIPES_COLOR = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color SEAM_SHADOW_COLOR = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color LOGO_SHADOW_COLOR = new Color(0f, 0f, 0f, 0.45f);

        private enum State { Hidden, Closing, Closed, Opening }

        private static SceneCurtain _instance;

        /// <summary>True from the moment it starts closing until it is fully open again.</summary>
        public static bool IsBusy => _instance != null && _instance._state != State.Hidden;

        private State _state = State.Hidden;
        private Canvas _canvas;
        private RectTransform _root, _left, _right, _logo;
        private CanvasGroup _logoGroup;
        private Action _onClosed;
        private bool _callbackDone;      // shut and the scene change has been asked for
        private bool _openRequested;
        private float _closedAt;
        private float _openAt = float.MaxValue;
        private Sequence _halves, _logoPop;
        private Tween _shake;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>Shuts the curtain, then runs <paramref name="onClosed"/> (usually the scene load).</summary>
        public static void Close(Action onClosed)
        {
            var curtain = GetOrCreate();
            switch (curtain._state)
            {
                case State.Closed:
                    RunCallback(onClosed);
                    return;
                case State.Closing:
                    curtain._onClosed += onClosed;
                    return;
            }
            curtain._onClosed = onClosed;
            curtain.PlayClose();
        }

        /// <summary>Opens it without waiting for a scene load (no-op when it is not shut).</summary>
        public static void Open()
        {
            if (_instance == null) return;
            if (_instance._state == State.Closing || _instance._state == State.Closed)
                _instance._openRequested = true;
        }

        private static SceneCurtain GetOrCreate()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[SceneCurtain]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneCurtain>();
            _instance.Build();
            return _instance;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && _state == State.Closed && _callbackDone)
                _openAt = Time.unscaledTime + OPEN_AFTER_LOAD_SECONDS;
        }

        /// <summary>Shut, the scene asked for: opens once the new scene is in (or when told, or as a safety net).</summary>
        private void Update()
        {
            if (_state != State.Closed || !_callbackDone) return;
            float now = Time.unscaledTime;
            bool due = _openRequested || now >= _openAt || now - _closedAt >= SAFETY_OPEN_SECONDS;
            if (due && now - _closedAt >= MIN_CLOSED_SECONDS)
                PlayOpen();
        }

        private void PlayClose()
        {
            if (_state == State.Hidden) PlaceHalves(OpenOffset());   // the screen size may have changed
            _state = State.Closing;
            _callbackDone = false;
            _openRequested = false;
            _openAt = float.MaxValue;
            _canvas.enabled = true;

            _halves?.Kill();
            _halves = DOTween.Sequence().SetUpdate(true)
                .Append(_left.DOAnchorPosX(0f, CLOSE_SECONDS).SetEase(Ease.InCubic))
                .Join(_right.DOAnchorPosX(0f, CLOSE_SECONDS).SetEase(Ease.InCubic))
                .AppendCallback(OnShut)
                .AppendInterval(CALLBACK_DELAY)
                .AppendCallback(() =>
                {
                    var callback = _onClosed;
                    _onClosed = null;
                    _callbackDone = true;
                    RunCallback(callback);
                });
        }

        private void OnShut()
        {
            _state = State.Closed;
            _closedAt = Time.unscaledTime;

            _shake?.Kill();
            _root.anchoredPosition = Vector2.zero;
            _shake = _root.DOShakeAnchorPos(SHAKE_SECONDS, ShakeStrength, vibrato: 30)
                .SetUpdate(true)
                .OnComplete(() => _root.anchoredPosition = Vector2.zero);

            if (_logo == null) return;
            _logoPop?.Kill();
            _logoGroup.alpha = 0f;
            _logo.localScale = Vector3.one * LOGO_START_SCALE;
            _logo.localRotation = Quaternion.Euler(0f, 0f, LOGO_START_TILT_DEGREES);
            _logoPop = DOTween.Sequence().SetUpdate(true)
                .Append(_logoGroup.DOFade(1f, POP_SECONDS / 3f))
                .Join(_logo.DOScale(1f, POP_SECONDS).SetEase(Ease.OutBack))
                .Join(_logo.DOLocalRotate(Vector3.zero, POP_SECONDS).SetEase(Ease.OutBack));
        }

        private void PlayOpen()
        {
            _state = State.Opening;
            _logoPop?.Kill();
            float to = OpenOffset();

            _halves?.Kill();
            _halves = DOTween.Sequence().SetUpdate(true);
            if (_logo != null)
                _halves.Append(_logoGroup.DOFade(0f, LOGO_OUT_SECONDS))
                       .Join(_logo.DOScale(LOGO_OUT_SCALE, LOGO_OUT_SECONDS));
            _halves.Append(_left.DOAnchorPosX(-to, OPEN_SECONDS).SetEase(Ease.InOutCubic))
                   .Join(_right.DOAnchorPosX(to, OPEN_SECONDS).SetEase(Ease.InOutCubic))
                   .OnComplete(() =>
                   {
                       _canvas.enabled = false;
                       _state = State.Hidden;
                   });
        }

        private static void RunCallback(Action callback)
        {
            try { callback?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void PlaceHalves(float offset)
        {
            _left.anchoredPosition = new Vector2(-offset, 0f);
            _right.anchoredPosition = new Vector2(offset, 0f);
        }

        /// <summary>How far each half travels so that its slanted edge clears the screen.</summary>
        private float OpenOffset()
        {
            Vector2 size = CanvasSize();
            return size.x * 0.5f + size.y * 0.5f * Mathf.Tan(SLANT_DEGREES * Mathf.Deg2Rad) + SEAM_WIDTH + EDGE_CLEARANCE;
        }

        private Vector2 CanvasSize()
        {
            Vector2 size = ((RectTransform)_canvas.transform).rect.size;
            return size.x > 1f && size.y > 1f ? size : REFERENCE_RESOLUTION;
        }

        private void Build()
        {
            var style = Resources.Load<SceneCurtainStyle>(STYLE_RESOURCES_PATH);
            if (style == null) style = ScriptableObject.CreateInstance<SceneCurtainStyle>();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORTING_ORDER;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            scaler.matchWidthOrHeight = MATCH_WIDTH_OR_HEIGHT;
            gameObject.AddComponent<GraphicRaycaster>();   // swallow clicks while it covers the screen

            _root = NewRect("Root", transform);
            Stretch(_root);

            var chalk = BuildChalkTexture();
            var stripes = BuildStripeTexture();
            _left = BuildHalf("Left", style.leftColor, style.seamColor, chalk, stripes, leftSide: true);
            _right = BuildHalf("Right", style.rightColor, style.seamColor, chalk, stripes, leftSide: false);
            BuildLogo(style);

            _canvas.enabled = false;
            Canvas.ForceUpdateCanvases();
            PlaceHalves(OpenOffset());
        }

        /// <summary>One half: a large slanted panel pivoted on the seam, so both halves share one edge line.</summary>
        private RectTransform BuildHalf(string name, Color color, Color seamColor, Texture2D chalk, Texture2D stripes, bool leftSide)
        {
            const float SIZE = 5000f;
            var half = NewRect(name, _root);
            half.anchorMin = half.anchorMax = new Vector2(0.5f, 0.5f);
            half.pivot = new Vector2(leftSide ? 1f : 0f, 0.5f);
            half.sizeDelta = new Vector2(SIZE, SIZE);
            half.localRotation = Quaternion.Euler(0f, 0f, -SLANT_DEGREES);
            var body = half.gameObject.AddComponent<Image>();
            body.color = color;

            var lines = NewRect("Stripes", half);
            Stretch(lines);
            var linesImage = lines.gameObject.AddComponent<RawImage>();
            linesImage.texture = stripes;
            linesImage.color = STRIPES_COLOR;
            linesImage.uvRect = new Rect(0f, 0f, SIZE / STRIPES_TILE_PIXELS, SIZE / STRIPES_TILE_PIXELS);
            linesImage.raycastTarget = false;

            var shadow = NewRect("SeamShadow", half);
            EdgeAnchor(shadow, leftSide, SEAM_INSET + SEAM_WIDTH - SEAM_SHADOW_OVERLAP, SEAM_SHADOW_WIDTH);
            var shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.color = SEAM_SHADOW_COLOR;
            shadowImage.raycastTarget = false;

            var seam = NewRect("Seam", half);
            EdgeAnchor(seam, leftSide, SEAM_INSET, SEAM_WIDTH);
            var seamImage = seam.gameObject.AddComponent<RawImage>();
            seamImage.texture = chalk;
            seamImage.color = seamColor;
            float repeat = SIZE / chalk.height;
            seamImage.uvRect = leftSide ? new Rect(0f, 0f, 1f, repeat) : new Rect(1f, 0.37f, -1f, repeat);
            seamImage.raycastTarget = false;
            return half;
        }

        /// <summary>A strip running along the seam edge of a half, <paramref name="inset"/> inside it.</summary>
        private static void EdgeAnchor(RectTransform rect, bool leftSide, float inset, float width)
        {
            float x = leftSide ? 1f : 0f;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(x, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(leftSide ? -inset : inset, 0f);
        }

        private void BuildLogo(SceneCurtainStyle style)
        {
            if (style.logo == null) return;
            Rect uv = style.logoUv;
            float aspect = (uv.width * style.logo.width) / Mathf.Max(1f, uv.height * style.logo.height);
            var size = new Vector2(LOGO_WIDTH, LOGO_WIDTH / Mathf.Max(0.01f, aspect));

            _logo = NewRect("Logo", _root);
            _logo.anchorMin = _logo.anchorMax = _logo.pivot = new Vector2(0.5f, 0.5f);
            _logo.sizeDelta = size;
            _logoGroup = _logo.gameObject.AddComponent<CanvasGroup>();
            _logoGroup.alpha = 0f;
            _logoGroup.blocksRaycasts = false;

            var shadow = NewRect("Shadow", _logo);
            Stretch(shadow);
            shadow.anchoredPosition = LOGO_SHADOW_OFFSET;
            var shadowImage = shadow.gameObject.AddComponent<RawImage>();
            shadowImage.texture = style.logo;
            shadowImage.uvRect = uv;
            shadowImage.color = LOGO_SHADOW_COLOR;

            var art = NewRect("Art", _logo);
            Stretch(art);
            var artImage = art.gameObject.AddComponent<RawImage>();
            artImage.texture = style.logo;
            artImage.uvRect = uv;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>A vertical chalk stroke that tiles top to bottom (whole-period sines for the ragged edges).</summary>
        private static Texture2D BuildChalkTexture()
        {
            const int W = 64, H = 512;
            var rng = new System.Random(CHALK_SEED);
            var pixels = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)H * Mathf.PI * 2f;
                float left = 9f + 3.0f * Mathf.Sin(3f * v + 0.4f) + 2.0f * Mathf.Sin(11f * v + 2.1f) + 1.2f * Mathf.Sin(29f * v + 4.0f);
                float right = 55f - 3.0f * Mathf.Sin(4f * v + 1.3f) - 2.0f * Mathf.Sin(13f * v + 0.2f) - 1.2f * Mathf.Sin(31f * v + 3.3f);
                for (int x = 0; x < W; x++)
                {
                    float a = Mathf.Clamp01((x - left) / 2.5f) * Mathf.Clamp01((right - x) / 2.5f);
                    double grain = rng.NextDouble();
                    if (grain < 0.10) a *= 0.35f;
                    else a *= 0.82f + 0.18f * (float)grain;
                    pixels[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = "SceneCurtainChalk",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Diagonal speed lines, tiling in both directions.</summary>
        private static Texture2D BuildStripeTexture()
        {
            const int S = 64;
            var pixels = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = (x + y) % S;
                    float a = Mathf.Clamp01(Mathf.Min(d, 22f - d) / 1.5f);   // a 22 px stripe with soft edges
                    pixels[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            var texture = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                name = "SceneCurtainStripes",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
