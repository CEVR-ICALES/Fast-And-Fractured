using Assets.SimpleLocalization.Scripts;
using System.Collections;
using System.Reflection;
using FishNet;
using FishNet.Managing.Scened;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Loading screen for FishNet scene loads, which LoadingManager never sees: the menu's singleplayer loading
    /// canvas is cloned into DontDestroyOnLoad and its bar driven from FishNet's load progress, with a code-built
    /// fallback when there is none.
    /// </summary>
    public class MultiplayerLoadingScreen : MonoBehaviour
    {
        private const float HIDE_DELAY_SECONDS = 0.6f;
        // Above the game UI, below the connection-lost toast.
        private const int FALLBACK_SORTING_ORDER = 32650;
        private const float LABEL_FONT_SIZE = 52f;
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920, 1080);
        private static readonly Color BACKGROUND_COLOR = new Color(0.05f, 0.05f, 0.08f, 1f);
        private static readonly Color LABEL_COLOR = new Color(1f, 0.9f, 0.5f, 1f);
        private static readonly Color BAR_BACKGROUND_COLOR = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color BAR_FILL_COLOR = new Color(1f, 0.75f, 0.2f, 1f);

        private static MultiplayerLoadingScreen _instance;
        private static FieldInfo _progressBarImageField;

        private bool _subscribed;
        private Coroutine _hideRoutine;

        private GameObject _spClone;
        private Image _spProgressFill;

        private Canvas _fallbackCanvas;
        private CanvasGroup _fallbackGroup;
        private Image _fallbackFill;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[MultiplayerLoadingScreen]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MultiplayerLoadingScreen>();
        }

        private void Update()
        {
            if (_subscribed || InstanceFinder.NetworkManager == null)
                return;

            var sm = InstanceFinder.NetworkManager.SceneManager;
            sm.OnLoadStart += HandleLoadStart;
            sm.OnLoadPercentChange += HandleLoadPercent;
            sm.OnLoadEnd += HandleLoadEnd;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (_subscribed && InstanceFinder.NetworkManager != null)
            {
                var sm = InstanceFinder.NetworkManager.SceneManager;
                sm.OnLoadStart -= HandleLoadStart;
                sm.OnLoadPercentChange -= HandleLoadPercent;
                sm.OnLoadEnd -= HandleLoadEnd;
            }
        }

        private void HandleLoadStart(SceneLoadStartEventArgs args)
        {
            MpCanary.Mark("FishNet scene load START");
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            // A host migration reloads the map behind its own frozen-frame overlay.
            if (HostMigration.HostMigrationOverlay.IsShown)
                return;

            if (TrySpawnSingleplayerScreen())
                return;

            EnsureFallbackUi();
            _fallbackFill.fillAmount = 0f;
            _fallbackGroup.alpha = 1f;
            _fallbackCanvas.gameObject.SetActive(true);
        }

        private void HandleLoadPercent(SceneLoadPercentEventArgs args)
        {
            float percent = Mathf.Clamp01(args.Percent);
            if (_spProgressFill != null)
                _spProgressFill.fillAmount = percent;
            else if (_fallbackFill != null)
                _fallbackFill.fillAmount = percent;
        }

        private void HandleLoadEnd(SceneLoadEndEventArgs args)
        {
            MpCanary.Mark("FishNet scene load END");
            if (_spProgressFill != null) _spProgressFill.fillAmount = 1f;
            if (_fallbackFill != null) _fallbackFill.fillAmount = 1f;
            if (_spClone != null || (_fallbackCanvas != null && _fallbackCanvas.gameObject.activeSelf))
                _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(HIDE_DELAY_SECONDS);
            if (_spClone != null)
            {
                Destroy(_spClone);
                _spClone = null;
                _spProgressFill = null;
            }
            if (_fallbackCanvas != null)
                _fallbackCanvas.gameObject.SetActive(false);
            _hideRoutine = null;
        }

        /// <summary>
        /// Clones the singleplayer LoadingManager canvas into DontDestroyOnLoad and caches its progress bar. Its
        /// OnEnable picks the background and tip as in singleplayer; with no AsyncOperation its Update is inert.
        /// </summary>
        private bool TrySpawnSingleplayerScreen()
        {
            if (_spClone != null) return true;

            var template = FindFirstObjectByType<LoadingManager>(FindObjectsInactive.Include);
            if (template == null) return false;

            var canvas = template.GetComponentInParent<Canvas>(true);
            GameObject source = canvas != null ? canvas.gameObject : template.gameObject;

            _spClone = Instantiate(source);
            _spClone.name = "[MpLoadingScreen-SP]";
            DontDestroyOnLoad(_spClone);

            var cloneManager = _spClone.GetComponentInChildren<LoadingManager>(true);
            if (cloneManager != null)
            {
                _progressBarImageField ??= typeof(LoadingManager)
                    .GetField("progressBarImage", BindingFlags.NonPublic | BindingFlags.Instance);
                _spProgressFill = _progressBarImageField?.GetValue(cloneManager) as Image;
                if (_spProgressFill != null)
                    _spProgressFill.fillAmount = 0f;
            }

            _spClone.SetActive(true);
            return true;
        }

        private void EnsureFallbackUi()
        {
            if (_fallbackCanvas != null) return;

            var canvasGO = new GameObject("[MpLoadingCanvas]");
            canvasGO.transform.SetParent(transform, false);
            _fallbackCanvas = canvasGO.AddComponent<Canvas>();
            _fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fallbackCanvas.sortingOrder = FALLBACK_SORTING_ORDER;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            _fallbackGroup = canvasGO.AddComponent<CanvasGroup>();
            _fallbackGroup.blocksRaycasts = true;

            var bg = new GameObject("Background").AddComponent<Image>();
            bg.transform.SetParent(canvasGO.transform, false);
            bg.color = BACKGROUND_COLOR;
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one);

            var label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(canvasGO.transform, false);
            label.text = LocalizationManager.Localize("MP.Loading.Map");
            label.fontSize = LABEL_FONT_SIZE;
            label.alignment = TextAlignmentOptions.Center;
            label.color = LABEL_COLOR;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(900, 90);
            labelRt.anchoredPosition = new Vector2(0, 40);

            var barBg = new GameObject("BarBg").AddComponent<Image>();
            barBg.transform.SetParent(canvasGO.transform, false);
            barBg.color = BAR_BACKGROUND_COLOR;
            var barBgRt = barBg.rectTransform;
            barBgRt.anchorMin = new Vector2(0.5f, 0.5f);
            barBgRt.anchorMax = new Vector2(0.5f, 0.5f);
            barBgRt.sizeDelta = new Vector2(700, 18);
            barBgRt.anchoredPosition = new Vector2(0, -40);

            _fallbackFill = new GameObject("BarFill").AddComponent<Image>();
            _fallbackFill.transform.SetParent(barBg.transform, false);
            _fallbackFill.color = BAR_FILL_COLOR;
            _fallbackFill.type = Image.Type.Filled;
            _fallbackFill.fillMethod = Image.FillMethod.Horizontal;
            _fallbackFill.sprite = null;
            Stretch(_fallbackFill.rectTransform, Vector2.zero, Vector2.one);

            _fallbackCanvas.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
