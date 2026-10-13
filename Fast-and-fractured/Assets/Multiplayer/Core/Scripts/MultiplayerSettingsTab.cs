using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Settings ▸ Multiplayer: a fourth tab next to Audio / Graphics / Accessibility with the
    /// online preferences (MultiplayerSettings). Built at runtime by copying the menu's own tab
    /// button, scroll panel and rows (toggle row, slider row, separator), so it looks like the
    /// rest of the settings and needs no prefab or scene edit; it appears in the main menu and in
    /// the pause menu alike.
    /// </summary>
    public sealed class MultiplayerSettingsTab : MonoBehaviour
    {
        private const string TAB_BUTTON = "SettingsContainer/SettingsButtons/AccesibilityButton";
        private const string TEMPLATE_PANEL = "SettingsContainer/AccesibilityContainer";
        private const string TOGGLE_ROW = "Viewport/Content/ColorblindToggle";
        private const string SLIDER_ROW = "Viewport/Content/MinimapScale";
        private const string SEPARATOR = "Viewport/Content/Panel (7)";

        private static readonly string[] OtherPanels =
        {
            "SettingsContainer/AudioContainer", "SettingsContainer/VideoContainer", "SettingsContainer/AccesibilityContainer",
            "SettingsContainer/KeyboardInputRemapingContainer", "SettingsContainer/GamepadInputRemapingContainer",
        };
        private static readonly string[] OtherTabs =
        {
            "SettingsContainer/SettingsButtons/AudioButton", "SettingsContainer/SettingsButtons/VideoButton",
            "SettingsContainer/SettingsButtons/AccesibilityButton",
        };

        private GameObject _panel;
        private Button _tab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachAll();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll();

        /// <summary>Also called for settings menus that appear later (the pause menu on each player root).</summary>
        public static void AttachAll()
        {
            foreach (var settings in Resources.FindObjectsOfTypeAll<SettingsMenuBehaviour>())
            {
                if (settings == null || !settings.gameObject.scene.IsValid()) continue;   // prefab assets
                if (settings.GetComponent<MultiplayerSettingsTab>() == null)
                    settings.gameObject.AddComponent<MultiplayerSettingsTab>();
            }
        }

        private void Awake() => Build();

        private void OnDisable()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void Build()
        {
            var tabTemplate = transform.Find(TAB_BUTTON);
            var panelTemplate = transform.Find(TEMPLATE_PANEL);
            if (tabTemplate == null || panelTemplate == null)
            {
                Debug.LogWarning("[MultiplayerSettingsTab] Settings layout changed - Multiplayer tab not added.");
                return;
            }

            // Tab button.
            var tabGo = Instantiate(tabTemplate.gameObject, tabTemplate.parent);
            tabGo.name = "MultiplayerButton";
            _tab = tabGo.GetComponent<Button>();
            Silence(_tab.onClick);
            _tab.onClick.AddListener(Open);
            SetLabel(tabGo.transform, "Multiplayer");
            foreach (var path in OtherTabs)
            {
                var other = transform.Find(path);
                if (other != null && other.TryGetComponent(out Button button)) button.onClick.AddListener(Close);
            }

            // Panel: the accessibility scroll view, emptied.
            _panel = Instantiate(panelTemplate.gameObject, panelTemplate.parent);
            _panel.name = "MultiplayerContainer";
            foreach (var behaviour in _panel.GetComponents<MonoBehaviour>())
                if (!(behaviour is ScrollRect) && !(behaviour is Image) && behaviour.GetType().Name != "AutoScrollSelected")
                    Destroy(behaviour);
            var content = _panel.transform.Find("Viewport/Content");
            var toggleTemplate = panelTemplate.Find(TOGGLE_ROW);
            var sliderTemplate = panelTemplate.Find(SLIDER_ROW);
            var separatorTemplate = panelTemplate.Find(SEPARATOR);
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

            AddToggle(content, toggleTemplate, separatorTemplate, "Player names",
                () => MultiplayerSettings.ShowPlayerNames, v => MultiplayerSettings.ShowPlayerNames = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Bot names",
                () => MultiplayerSettings.ShowBotNames, v => MultiplayerSettings.ShowBotNames = v);
            AddSlider(content, sliderTemplate, separatorTemplate, "Name distance",
                MultiplayerSettings.MIN_NAME_DISTANCE, MultiplayerSettings.MAX_NAME_DISTANCE,
                () => MultiplayerSettings.NameTagDistance, v => MultiplayerSettings.NameTagDistance = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Connection panel (F3)",
                () => MultiplayerSettings.ShowNetworkStats, v => MultiplayerSettings.ShowNetworkStats = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Host the room if the host leaves",
                () => MultiplayerSettings.WillingToHost, v => MultiplayerSettings.WillingToHost = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Share gameplay statistics",
                () => GameTelemetry.Telemetry.Sharing, v => GameTelemetry.Telemetry.Sharing = v);

            _panel.SetActive(false);
        }

        private void Open()
        {
            foreach (var path in OtherPanels)
            {
                var other = transform.Find(path);
                if (other != null) other.gameObject.SetActive(false);
            }
            if (_panel != null) _panel.SetActive(true);
        }

        private void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // -- Rows --------------------------------------------------------------------------
        private static void AddToggle(Transform content, Transform template, Transform separator, string label,
                                      System.Func<bool> get, UnityAction<bool> set)
        {
            if (template == null) return;
            var row = Instantiate(template.gameObject, content);
            row.name = label;
            SetLabel(row.transform, label);
            var toggle = row.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                Silence(toggle.onValueChanged);
                toggle.SetIsOnWithoutNotify(get());
                toggle.onValueChanged.AddListener(set);
            }
            AddSeparator(content, separator);
        }

        private static void AddSlider(Transform content, Transform template, Transform separator, string label,
                                      float min, float max, System.Func<float> get, UnityAction<float> set)
        {
            if (template == null) return;
            var row = Instantiate(template.gameObject, content);
            row.name = label;
            SetLabel(row.transform, label);
            var slider = row.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                Silence(slider.onValueChanged);
                slider.wholeNumbers = true;
                slider.minValue = min;
                slider.maxValue = max;
                slider.SetValueWithoutNotify(get());
                slider.onValueChanged.AddListener(set);
            }
            AddSeparator(content, separator);
        }

        private static void AddSeparator(Transform content, Transform template)
        {
            if (template != null) Instantiate(template.gameObject, content).SetActive(true);
        }

        /// <summary>The row / button text, without the localization that would overwrite it.</summary>
        private static void SetLabel(Transform root, string text)
        {
            var label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            foreach (var behaviour in label.GetComponents<MonoBehaviour>())
                if (behaviour.GetType().Name == "LocalizedText") Destroy(behaviour);
            label.text = text;
        }

        /// <summary>Copied controls keep their original listeners (colour blind mode, minimap...): drop them.</summary>
        private static void Silence(UnityEventBase unityEvent)
        {
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
                unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
            if (unityEvent is UnityEvent e) e.RemoveAllListeners();
            else if (unityEvent is UnityEvent<bool> b) b.RemoveAllListeners();
            else if (unityEvent is UnityEvent<float> f) f.RemoveAllListeners();
        }
    }
}
