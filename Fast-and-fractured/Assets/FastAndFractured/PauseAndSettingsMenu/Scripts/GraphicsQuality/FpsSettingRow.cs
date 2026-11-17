using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FastAndFractured
{
    // The row's name is its tooltip key.
    public sealed class FpsSettingRow : MonoBehaviour
    {
        public const string ROW_NAME = "ShowFps";
        private const string LABEL_KEY = "Graphics.ShowFps";
        private const string CONTENT = "SettingsContainer/VideoContainer/Viewport/Content";
        private const string TEMPLATE_ROW = "VSync";
        private const string SEPARATOR = "Separator6";
        private const float CHECK_EVERY = 0.5f;

        private sealed class Finder : MonoBehaviour
        {
            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + CHECK_EVERY;
                foreach (var settings in FindObjectsByType<SettingsMenuBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (settings != null && settings.gameObject.scene.IsValid() && settings.GetComponent<FpsSettingRow>() == null)
                        settings.gameObject.AddComponent<FpsSettingRow>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[FpsSettingRow]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Finder>();
        }

        private Toggle _toggle;

        private void Awake()
        {
            var content = transform.Find(CONTENT);
            var template = content != null ? content.Find(TEMPLATE_ROW) : null;
            if (template == null || content.Find(ROW_NAME) != null) return;

            var row = Instantiate(template.gameObject, content);
            row.name = ROW_NAME;
            row.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);
            row.SetActive(true);
            var separator = content.Find(SEPARATOR);
            if (separator != null)
            {
                var copy = Instantiate(separator.gameObject, content);
                copy.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);
                copy.SetActive(true);
            }

            var label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var localized = label.GetComponent<LocalizedText>();
                if (localized != null) localized.LocalizationKey = LABEL_KEY;
                label.text = LocalizationManager.Localize(LABEL_KEY);
            }

            _toggle = row.GetComponentInChildren<Toggle>(true);
            if (_toggle == null) return;
            // The copied toggle keeps the VSync listeners: drop them.
            for (int i = 0; i < _toggle.onValueChanged.GetPersistentEventCount(); i++)
                _toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            _toggle.onValueChanged.RemoveAllListeners();
            _toggle.SetIsOnWithoutNotify(FpsCounter.Visible);
            _toggle.onValueChanged.AddListener(value => FpsCounter.Visible = value);
            LinkNavigation(template.GetComponentInChildren<Selectable>(true));
        }

        private void OnEnable()
        {
            if (_toggle != null) _toggle.SetIsOnWithoutNotify(FpsCounter.Visible);
        }

        private void LinkNavigation(Selectable above)
        {
            if (above == null) return;
            Selectable below = above.navigation.selectOnDown;
            var navigation = _toggle.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = above;
            navigation.selectOnDown = below;
            _toggle.navigation = navigation;

            var aboveNavigation = above.navigation;
            aboveNavigation.mode = Navigation.Mode.Explicit;
            aboveNavigation.selectOnDown = _toggle;
            above.navigation = aboveNavigation;
            if (below == null) return;
            var belowNavigation = below.navigation;
            belowNavigation.mode = Navigation.Mode.Explicit;
            belowNavigation.selectOnUp = _toggle;
            below.navigation = belowNavigation;
        }
    }
}
