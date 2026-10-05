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
    /// Loading screen for FishNet scene loads (lobby → networked map). The singleplayer loading
    /// screen is driven by LoadingManager's own AsyncOperation and never activates for
    /// FishNet-managed loads, so multiplayer scene transitions showed a frozen menu instead.
    ///
    /// Shows the REAL singleplayer loading screen: at load start the scene's LoadingManager canvas
    /// (inactive until a map button would activate it) is cloned into DontDestroyOnLoad, activated
    /// (its own OnEnable picks the random background and tip exactly like singleplayer), and its
    /// progress bar is driven from FishNet's OnLoadPercentChange instead of an AsyncOperation.
    /// The clone is destroyed shortly after OnLoadEnd. If no LoadingManager canvas exists in the
    /// current scene (unexpected flow), a minimal code-built overlay is used as fallback.
    ///
    /// Bootstrapped once per session, zero scene wiring.
    /// </summary>
    public class MultiplayerLoadingScreen : MonoBehaviour
    {
        private const float HIDE_DELAY_SECONDS = 0.6f;

        private static MultiplayerLoadingScreen _instance;
        private static FieldInfo _progressBarImageField;

        private bool _subscribed;
        private Coroutine _hideRoutine;

        // Singleplayer-clone path
        private GameObject _spClone;
        private Image _spProgressFill;

        // Fallback code-built path
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
            // The NetworkManager appears when the menu scene loads; poll cheaply until found.
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

            // A host migration reloads the map behind its own frozen-frame overlay; a loading
            // screen popping up over it would undo the "the match just paused" look.
            if (HostMigration.HostMigrationOverlay.IsShown)
                return;

            if (TrySpawnSingleplayerScreen())
                return;

            // Fallback: keep the old minimal overlay so the player is never left staring at a
            // frozen frame, even when no LoadingManager canvas exists in the departing scene.
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

        // ── Singleplayer screen clone ────────────────────────────────────────────
        /// <summary>
        /// Clones the singleplayer LoadingManager canvas (found in any loaded scene, active or not)
        /// into DontDestroyOnLoad so it survives the FishNet scene swap, activates it, and caches
        /// its private progressBarImage to drive from network load progress. The LoadingManager
        /// component stays enabled: with no AsyncOperation its Update is inert, while its OnEnable
        /// runs the exact singleplayer behaviour (random background, localized tip, rotation timer).
        /// </summary>
        private bool TrySpawnSingleplayerScreen()
        {
            if (_spClone != null) return true;

            var template = FindFirstObjectByType<LoadingManager>(FindObjectsInactive.Include);
            if (template == null) return false;

            // Clone the whole canvas root the LoadingManager lives under (the component sits on the
            // canvas GameObject itself in the menu prefab; take its root-most canvas to be safe).
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

        // ── Fallback UI construction (no scene assets required) ──────────────────
        private void EnsureFallbackUi()
        {
            if (_fallbackCanvas != null) return;

            var canvasGO = new GameObject("[MpLoadingCanvas]");
            canvasGO.transform.SetParent(transform, false);
            _fallbackCanvas = canvasGO.AddComponent<Canvas>();
            _fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fallbackCanvas.sortingOrder = 32650; // above game UI, below the connection-lost toast
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _fallbackGroup = canvasGO.AddComponent<CanvasGroup>();
            _fallbackGroup.blocksRaycasts = true;

            var bg = new GameObject("Background").AddComponent<Image>();
            bg.transform.SetParent(canvasGO.transform, false);
            bg.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one);

            var label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(canvasGO.transform, false);
            label.text = "CARGANDO MAPA…";
            label.fontSize = 52;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.9f, 0.5f, 1f);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(900, 90);
            labelRt.anchoredPosition = new Vector2(0, 40);

            var barBg = new GameObject("BarBg").AddComponent<Image>();
            barBg.transform.SetParent(canvasGO.transform, false);
            barBg.color = new Color(1f, 1f, 1f, 0.15f);
            var barBgRt = barBg.rectTransform;
            barBgRt.anchorMin = new Vector2(0.5f, 0.5f);
            barBgRt.anchorMax = new Vector2(0.5f, 0.5f);
            barBgRt.sizeDelta = new Vector2(700, 18);
            barBgRt.anchoredPosition = new Vector2(0, -40);

            _fallbackFill = new GameObject("BarFill").AddComponent<Image>();
            _fallbackFill.transform.SetParent(barBg.transform, false);
            _fallbackFill.color = new Color(1f, 0.75f, 0.2f, 1f);
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
