using Assets.SimpleLocalization.Scripts;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Settings > Multiplayer tab, built at runtime from copies of the menu's own tab, panel and rows, in the main
    /// and pause menus alike.
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

            var tabGo = Instantiate(tabTemplate.gameObject, tabTemplate.parent);
            tabGo.name = "MultiplayerButton";
            _tab = tabGo.GetComponent<Button>();
            Silence(_tab.onClick);
            _tab.onClick.AddListener(Open);
            SetLabel(tabGo.transform, "MP.Menu.Multiplayer");
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

            AddToggle(content, toggleTemplate, separatorTemplate, "Player names", "MP.Settings.PlayerNames",
                () => MultiplayerSettings.ShowPlayerNames, v => MultiplayerSettings.ShowPlayerNames = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Bot names", "MP.Settings.BotNames",
                () => MultiplayerSettings.ShowBotNames, v => MultiplayerSettings.ShowBotNames = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Player levels in the room", "MP.Settings.PlayerLevels",
                () => MultiplayerSettings.ShowPlayerLevels, v => MultiplayerSettings.ShowPlayerLevels = v);
            AddSlider(content, sliderTemplate, separatorTemplate, "Name distance", "MP.Settings.NameDistance",
                MultiplayerSettings.MIN_NAME_DISTANCE, MultiplayerSettings.MAX_NAME_DISTANCE,
                () => MultiplayerSettings.NameTagDistance, v => MultiplayerSettings.NameTagDistance = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Connection panel (F3)", "MP.Settings.ConnectionPanel",
                () => MultiplayerSettings.ShowNetworkStats, v => MultiplayerSettings.ShowNetworkStats = v);
            AddToggle(content, toggleTemplate, separatorTemplate, "Host the room if the host leaves", "MP.Settings.WillingToHost",
                () => MultiplayerSettings.WillingToHost, v => MultiplayerSettings.WillingToHost = v);

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

        /// <param name="rowName">English name of the row: also its tooltip key (SettingsTooltips).</param>
        /// <param name="labelKey">Localization key of the label (Multiplayer sheet).</param>
        private static void AddToggle(Transform content, Transform template, Transform separator, string rowName, string labelKey,
                                      System.Func<bool> get, UnityAction<bool> set)
        {
            if (template == null) return;
            var row = Instantiate(template.gameObject, content);
            row.name = rowName;
            SetLabel(row.transform, labelKey);
            var toggle = row.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                Silence(toggle.onValueChanged);
                toggle.SetIsOnWithoutNotify(get());
                toggle.onValueChanged.AddListener(set);
            }
            AddSeparator(content, separator);
        }

        private static void AddSlider(Transform content, Transform template, Transform separator, string rowName, string labelKey,
                                      float min, float max, System.Func<float> get, UnityAction<float> set)
        {
            if (template == null) return;
            var row = Instantiate(template.gameObject, content);
            row.name = rowName;
            SetLabel(row.transform, labelKey);
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

        /// <summary>The row / button text: the copied LocalizedText now shows <paramref name="key"/>.</summary>
        private static void SetLabel(Transform root, string key)
        {
            var label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            var localized = label.GetComponent<LocalizedText>();
            if (localized == null && label is TextMeshProUGUI) localized = label.gameObject.AddComponent<LocalizedText>();
            if (localized != null) localized.LocalizationKey = key;
            label.text = LocalizationManager.Localize(key);
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
