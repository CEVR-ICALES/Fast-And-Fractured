using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Settings ▸ Accessibility ▸ "Share gameplay statistics" (Telemetry.Sharing), in every
    /// settings menu - main menu and pause menus, singleplayer and multiplayer. Built at runtime
    /// by copying the tab's own toggle row, so no prefab or scene is edited. The row is named
    /// like its label, which is also its tooltip key (SettingsTooltips).
    /// </summary>
    public sealed class TelemetrySettingRow : MonoBehaviour
    {
        public const string ROW_NAME = "Share gameplay statistics";
        private const string LABEL_KEY = "MP.Settings.ShareStats";
        private const string CONTENT = "SettingsContainer/AccesibilityContainer/Viewport/Content";
        private const string TOGGLE_ROW = "ColorblindToggle";
        private const string SEPARATOR = "Panel (7)";
        private const float CHECK_EVERY = 0.5f;

        /// <summary>Finds settings menus as they appear (the pause menus come with each player).</summary>
        private sealed class Finder : MonoBehaviour
        {
            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + CHECK_EVERY;
                foreach (var settings in FindObjectsByType<SettingsMenuBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (settings != null && settings.gameObject.scene.IsValid() && settings.GetComponent<TelemetrySettingRow>() == null)
                        settings.gameObject.AddComponent<TelemetrySettingRow>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[TelemetrySettingRow]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Finder>();
        }

        private Toggle _toggle;

        private void Awake()
        {
            var content = transform.Find(CONTENT);
            var template = content != null ? content.Find(TOGGLE_ROW) : null;
            if (template == null || content.Find(ROW_NAME) != null) return;

            var separator = content.Find(SEPARATOR);
            if (separator != null) Instantiate(separator.gameObject, content).SetActive(true);
            var row = Instantiate(template.gameObject, content);
            row.name = ROW_NAME;
            row.SetActive(true);

            var label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var localized = label.GetComponent<LocalizedText>();
                if (localized == null && label is TextMeshProUGUI) localized = label.gameObject.AddComponent<LocalizedText>();
                if (localized != null) localized.LocalizationKey = LABEL_KEY;
                label.text = LocalizationManager.Localize(LABEL_KEY);
            }

            _toggle = row.GetComponentInChildren<Toggle>(true);
            if (_toggle == null) return;
            // The copied toggle keeps the colour blind listeners: drop them.
            for (int i = 0; i < _toggle.onValueChanged.GetPersistentEventCount(); i++)
                _toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            _toggle.onValueChanged.RemoveAllListeners();
            _toggle.SetIsOnWithoutNotify(Telemetry.Sharing);
            _toggle.onValueChanged.AddListener(value => Telemetry.Sharing = value);
        }

        private void OnEnable()
        {
            if (_toggle != null) _toggle.SetIsOnWithoutNotify(Telemetry.Sharing);
        }
    }
}
