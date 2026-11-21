using System.Collections.Generic;
using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FastAndFractured
{
    public sealed class GraphicsQualitySettingRow : MonoBehaviour
    {
        public const string ROW_NAME = "GraphicsQuality";
        private const string LABEL_KEY = "Graphics.Quality";
        private const string AUTO_KEY = "Graphics.QualityAuto";
        private const string AUTO_WITH_LEVEL_KEY = "Graphics.QualityAutoWithLevel";
        private const string CONTENT = "SettingsContainer/VideoContainer/Viewport/Content";
        private const string TEMPLATE_ROW = "ScreenMode";
        private const string ROW_ABOVE = "Brightness";
        private const string SEPARATOR_BELOW_ROW_ABOVE = "Separator";
        private const int AUTO_OPTION = 0;

        private sealed class Finder : MonoBehaviour
        {
            private int _seen = -1;

            private void Update()
            {
                if (_seen == MenuScan.Version) return;
                _seen = MenuScan.Version;
                foreach (var settings in MenuScan.SettingsMenus)
                    if (settings != null && settings.gameObject.scene.IsValid() && settings.GetComponent<GraphicsQualitySettingRow>() == null)
                        settings.gameObject.AddComponent<GraphicsQualitySettingRow>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[GraphicsQualitySettingRow]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Finder>();
        }

        private TMP_Dropdown _dropdown;

        private void Awake()
        {
            var content = transform.Find(CONTENT);
            var template = content != null ? content.Find(TEMPLATE_ROW) : null;
            if (template == null || content.Find(ROW_NAME) != null) return;

            var rowAbove = content.Find(ROW_ABOVE);
            var separator = content.Find(SEPARATOR_BELOW_ROW_ABOVE);
            if (rowAbove == null || separator == null) return;
            var rowBelow = separator.GetSiblingIndex() + 1 < content.childCount ? content.GetChild(separator.GetSiblingIndex() + 1) : null;

            var row = Instantiate(template.gameObject, content);
            row.name = ROW_NAME;
            row.transform.SetSiblingIndex(separator.GetSiblingIndex() + 1);
            row.SetActive(true);
            var newSeparator = Instantiate(separator.gameObject, content);
            newSeparator.transform.SetSiblingIndex(row.transform.GetSiblingIndex() + 1);
            newSeparator.SetActive(true);

            var label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var localized = label.GetComponent<LocalizedText>();
                if (localized != null) localized.LocalizationKey = LABEL_KEY;
                label.text = LocalizationManager.Localize(LABEL_KEY);
            }

            _dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            if (_dropdown == null) return;
            for (int i = 0; i < _dropdown.onValueChanged.GetPersistentEventCount(); i++)
                _dropdown.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            _dropdown.onValueChanged.RemoveAllListeners();
            _dropdown.onValueChanged.AddListener(OnPicked);
            LinkNavigation(rowAbove.GetComponentInChildren<Selectable>(true),
                rowBelow != null ? rowBelow.GetComponentInChildren<Selectable>(true) : null);

            Refresh();
            LocalizationManager.OnLocalizationChanged += Refresh;
            GraphicsQuality.LevelChanged += OnLevelChanged;
        }

        private void OnEnable()
        {
            if (_dropdown != null) Refresh();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLocalizationChanged -= Refresh;
            GraphicsQuality.LevelChanged -= OnLevelChanged;
        }

        private void LinkNavigation(Selectable above, Selectable below)
        {
            var navigation = _dropdown.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = above;
            navigation.selectOnDown = below;
            _dropdown.navigation = navigation;

            if (above != null)
            {
                var aboveNavigation = above.navigation;
                aboveNavigation.selectOnDown = _dropdown;
                above.navigation = aboveNavigation;
            }
            if (below != null)
            {
                var belowNavigation = below.navigation;
                belowNavigation.selectOnUp = _dropdown;
                below.navigation = belowNavigation;
            }
        }

        private void OnLevelChanged(int level) => Refresh();

        private void Refresh()
        {
            if (_dropdown == null) return;
            var options = new List<string> { AutoLabel() };
            for (int level = 0; level < GraphicsQuality.LevelCount; level++)
                options.Add(LevelLabel(level));
            _dropdown.ClearOptions();
            _dropdown.AddOptions(options);
            _dropdown.SetValueWithoutNotify(GraphicsQuality.IsAuto ? AUTO_OPTION : GraphicsQuality.SavedLevel + 1);
            _dropdown.RefreshShownValue();
        }

        private static string AutoLabel()
        {
            if (!GraphicsQuality.IsAuto || !GraphicsQuality.HasSavedLevel || GraphicsQualityDetector.IsRunning)
                return LocalizationManager.Localize(AUTO_KEY);
            return LocalizationManager.Localize(AUTO_WITH_LEVEL_KEY, LevelLabel(GraphicsQuality.SavedLevel));
        }

        private static string LevelLabel(int level)
        {
            string key = GraphicsQuality.LevelNameKey(level);
            return key != null ? LocalizationManager.Localize(key) : QualitySettings.names[level];
        }

        private void OnPicked(int option)
        {
            if (option == AUTO_OPTION)
            {
                GraphicsQuality.SetAuto();
                GraphicsQualityDetector.Run();
            }
            else
            {
                GraphicsQualityDetector.Cancel();
                GraphicsQuality.SaveManual(option - 1);
            }
            Refresh();
        }
    }
}
