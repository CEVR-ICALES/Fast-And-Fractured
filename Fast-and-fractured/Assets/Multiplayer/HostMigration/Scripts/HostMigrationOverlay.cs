using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// What players see during a host migration: the last frame of the match, frozen and dimmed,
    /// with a message and a spinner - instead of the trip through the main menu. Behind it the
    /// map is reloaded on the new host; it goes away when the match resumes (Hide) or the
    /// migration fails.
    /// </summary>
    public sealed class HostMigrationOverlay : MonoBehaviour
    {
        private static HostMigrationOverlay _instance;
        private const float MAX_SECONDS = 30f;   // never leave a frozen screen up for good

        private TextMeshProUGUI _message;
        private CanvasGroup _group;
        private Texture2D _frozenFrame;
        private float _shownAt;
        private bool _hiding;

        public static bool IsShown => _instance != null;

        /// <summary>Freezes the current frame (captured at the end of this frame) under the message.</summary>
        public static void Show(string message)
        {
            if (_instance == null)
            {
                var go = new GameObject("[HostMigrationOverlay]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HostMigrationOverlay>();
                _instance.StartCoroutine(_instance.Build());
            }
            _instance.SetMessage(message);
        }

        public static void SetStatus(string message)
        {
            if (_instance != null) _instance.SetMessage(message);
        }

        public static void Hide()
        {
            if (_instance != null && !_instance._hiding) _instance.StartCoroutine(_instance.FadeOut());
        }

        private string _pendingMessage;
        private void SetMessage(string message)
        {
            _pendingMessage = message;
            if (_message != null) _message.text = message;
        }

        private IEnumerator Build()
        {
            _shownAt = Time.unscaledTime;
            yield return new WaitForEndOfFrame();
            _frozenFrame = ScreenCapture.CaptureScreenshotAsTexture();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();   // swallow clicks while frozen
            _group = gameObject.AddComponent<CanvasGroup>();

            var frame = Stretch(new GameObject("Frame", typeof(RectTransform)).AddComponent<RawImage>());
            frame.texture = _frozenFrame;
            var dim = Stretch(new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>());
            dim.color = new Color(0f, 0f, 0f, 0.55f);

            var box = new GameObject("Message", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(transform, false);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(1200f, 220f);
            _message = box.gameObject.AddComponent<TextMeshProUGUI>();
            _message.alignment = TextAlignmentOptions.Center;
            _message.fontSize = 46f;
            _message.color = Color.white;
            _message.text = _pendingMessage;
            var font = FindGameFont();
            if (font != null) _message.font = font;

            var dots = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
            dots.SetParent(transform, false);
            dots.anchorMin = dots.anchorMax = new Vector2(0.5f, 0.5f);
            dots.anchoredPosition = new Vector2(0f, -140f);
            dots.sizeDelta = new Vector2(300f, 80f);
            _dots = dots.gameObject.AddComponent<TextMeshProUGUI>();
            _dots.alignment = TextAlignmentOptions.Center;
            _dots.fontSize = 110f;
            _dots.color = new Color(1f, 0.78f, 0.35f, 1f);
            if (font != null) _dots.font = font;
        }

        private static readonly string[] DotFrames =
        {
            ". <alpha=#40>. .", "<alpha=#40>. <alpha=#FF>. <alpha=#40>.", "<alpha=#40>. . <alpha=#FF>.",
        };
        private TextMeshProUGUI _dots;

        private T Stretch<T>(T graphic) where T : Graphic
        {
            var rt = graphic.rectTransform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            graphic.raycastTarget = true;
            return graphic;
        }

        private static TMP_FontAsset FindGameFont()
        {
            foreach (var text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.font != null && text.font.name.StartsWith("AlfaSlabOne")) return text.font;
            return null;
        }

        private void Update()
        {
            if (_dots != null) _dots.text = DotFrames[(int)((Time.unscaledTime - _shownAt) / 0.35f) % DotFrames.Length];
            if (!_hiding && Time.unscaledTime - _shownAt > MAX_SECONDS) Hide();
        }

        private IEnumerator FadeOut()
        {
            _hiding = true;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                if (_group != null) _group.alpha = 1f - t / 0.35f;
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_frozenFrame != null) Destroy(_frozenFrame);
            if (_instance == this) _instance = null;
        }
    }
}
