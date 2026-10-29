using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastAndFractured.Multiplayer.Editor
{
    [InitializeOnLoad]
    internal static class MultiplayerLogFilterBootstrap
    {
        static MultiplayerLogFilterBootstrap()
        {
            MultiplayerLogFilterService.EnsureInitialized();
            InstallLogHandler();
        }

        private static void InstallLogHandler()
        {
            if (Debug.unityLogger.logHandler is MultiplayerFilteringLogHandler)
            {
                return;
            }

            Debug.unityLogger.logHandler = new MultiplayerFilteringLogHandler(Debug.unityLogger.logHandler);
        }
    }

    internal sealed class MultiplayerFilteringLogHandler : ILogHandler
    {
        private readonly ILogHandler _innerHandler;

        public MultiplayerFilteringLogHandler(ILogHandler innerHandler)
        {
            _innerHandler = innerHandler;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (!MultiplayerLogFilterService.ShouldEmit(logType, format, context))
            {
                return;
            }

            _innerHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context)
        {
            _innerHandler.LogException(exception, context);
        }
    }

    [Serializable]
    internal sealed class MultiplayerLogFilterConfig
    {
        public bool filteringEnabled = true;
        public bool filterWarnings = false;
        public bool showUncategorized = true;
        public List<MultiplayerLogCategoryState> categories = new List<MultiplayerLogCategoryState>();
    }

    [Serializable]
    internal sealed class MultiplayerLogCategoryState
    {
        public string name;
        public bool enabled = true;
    }

    internal static class MultiplayerLogFilterService
    {
        private const string ConfigPrefsKey = "FastAndFractured.Multiplayer.LogFilterConfig.v1";

        private static readonly Regex LogTagRegex = new Regex(
            "Debug\\.Log(?:Warning|Error)?\\(\\$?\"[^\"\\r\\n]*\\[(?<tag>[^\\]\\r\\n]+)\\]",
            RegexOptions.Compiled);

        private static readonly Dictionary<string, bool> CategoryMap =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;
        private static MultiplayerLogFilterConfig _config;

        public static bool FilteringEnabled
        {
            get
            {
                EnsureInitialized();
                return _config.filteringEnabled;
            }
        }

        public static bool FilterWarnings
        {
            get
            {
                EnsureInitialized();
                return _config.filterWarnings;
            }
        }

        public static bool ShowUncategorized
        {
            get
            {
                EnsureInitialized();
                return _config.showUncategorized;
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            LoadConfig();

            // New log tags come from the window's "Rescan Categories", not a scan on every domain reload.
        }

        public static bool ShouldEmit(LogType logType, string message, Object context)
        {
            EnsureInitialized();

            if (!_config.filteringEnabled)
            {
                return true;
            }

            if (logType == LogType.Error || logType == LogType.Assert || logType == LogType.Exception)
            {
                return true;
            }

            if (logType == LogType.Warning && !_config.filterWarnings)
            {
                return true;
            }

            string category = ExtractCategory(message, context);
            if (string.IsNullOrWhiteSpace(category))
            {
                return _config.showUncategorized;
            }

            bool enabled;
            if (!CategoryMap.TryGetValue(category, out enabled))
            {
                return _config.showUncategorized;
            }

            return enabled;
        }

        public static List<MultiplayerLogCategoryState> GetCategorySnapshot()
        {
            EnsureInitialized();

            List<string> names = new List<string>(CategoryMap.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);

            List<MultiplayerLogCategoryState> snapshot = new List<MultiplayerLogCategoryState>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string categoryName = names[i];
                snapshot.Add(new MultiplayerLogCategoryState
                {
                    name = categoryName,
                    enabled = CategoryMap[categoryName]
                });
            }

            return snapshot;
        }

        public static void SetGlobalSettings(bool filteringEnabled, bool filterWarnings, bool showUncategorized)
        {
            EnsureInitialized();

            _config.filteringEnabled = filteringEnabled;
            _config.filterWarnings = filterWarnings;
            _config.showUncategorized = showUncategorized;
            SaveConfig();
        }

        public static void SetCategoryEnabled(string category, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return;
            }

            EnsureInitialized();

            CategoryMap[category.Trim()] = enabled;
            SaveConfig();
        }

        public static bool EnsureCategory(string category, bool enabled = true)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            EnsureInitialized();

            string trimmedCategory = category.Trim();
            if (CategoryMap.ContainsKey(trimmedCategory))
            {
                return false;
            }

            CategoryMap[trimmedCategory] = enabled;
            SaveConfig();
            return true;
        }

        public static void SetAllCategories(bool enabled)
        {
            EnsureInitialized();

            List<string> keys = new List<string>(CategoryMap.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                CategoryMap[keys[i]] = enabled;
            }

            SaveConfig();
        }

        public static void ResetToDefaults()
        {
            _config = CreateDefaultConfig();
            RebuildMapFromConfig();
            SaveConfig();
        }

        public static int RescanProjectCategories()
        {
            EnsureInitialized();

            int addedCount = 0;
            HashSet<string> discoveredTags = DiscoverProjectLogTags();
            foreach (string tag in discoveredTags)
            {
                if (CategoryMap.ContainsKey(tag))
                {
                    continue;
                }

                CategoryMap[tag] = true;
                addedCount++;
            }

            if (addedCount > 0)
            {
                SaveConfig();
            }

            return addedCount;
        }

        private static void LoadConfig()
        {
            string json = EditorPrefs.GetString(ConfigPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                _config = JsonUtility.FromJson<MultiplayerLogFilterConfig>(json);
            }

            if (_config == null)
            {
                _config = CreateDefaultConfig();
            }

            if (_config.categories == null)
            {
                _config.categories = new List<MultiplayerLogCategoryState>();
            }

            RebuildMapFromConfig();
        }

        private static MultiplayerLogFilterConfig CreateDefaultConfig()
        {
            MultiplayerLogFilterConfig defaultConfig = new MultiplayerLogFilterConfig();
            HashSet<string> discoveredTags = DiscoverProjectLogTags();

            foreach (string tag in discoveredTags)
            {
                defaultConfig.categories.Add(new MultiplayerLogCategoryState
                {
                    name = tag,
                    enabled = true
                });
            }

            return defaultConfig;
        }

        private static void RebuildMapFromConfig()
        {
            CategoryMap.Clear();

            for (int i = 0; i < _config.categories.Count; i++)
            {
                MultiplayerLogCategoryState category = _config.categories[i];
                if (category == null || string.IsNullOrWhiteSpace(category.name))
                {
                    continue;
                }

                CategoryMap[category.name.Trim()] = category.enabled;
            }
        }

        private static void SaveConfig()
        {
            SyncConfigFromMap();
            string json = JsonUtility.ToJson(_config);
            EditorPrefs.SetString(ConfigPrefsKey, json);
        }

        private static void SyncConfigFromMap()
        {
            List<string> names = new List<string>(CategoryMap.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);

            _config.categories.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                string categoryName = names[i];
                _config.categories.Add(new MultiplayerLogCategoryState
                {
                    name = categoryName,
                    enabled = CategoryMap[categoryName]
                });
            }
        }

        private static HashSet<string> DiscoverProjectLogTags()
        {
            HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string multiplayerPath = Path.Combine(Application.dataPath, "Multiplayer");

            if (!Directory.Exists(multiplayerPath))
            {
                return tags;
            }

            string[] scriptPaths = Directory.GetFiles(multiplayerPath, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < scriptPaths.Length; i++)
            {
                string sourceCode = File.ReadAllText(scriptPaths[i]);
                MatchCollection matches = LogTagRegex.Matches(sourceCode);

                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    string tag = matches[matchIndex].Groups["tag"].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }

            return tags;
        }

        private static string ExtractCategory(string message, Object context)
        {
            if (!string.IsNullOrEmpty(message))
            {
                int categoryStart = message.IndexOf('[');
                if (categoryStart >= 0)
                {
                    int categoryEnd = message.IndexOf(']', categoryStart + 1);
                    if (categoryEnd > categoryStart + 1)
                    {
                        string category = message.Substring(categoryStart + 1, categoryEnd - categoryStart - 1).Trim();
                        if (!string.IsNullOrEmpty(category))
                        {
                            return category;
                        }
                    }
                }
            }

            if (context == null)
            {
                return string.Empty;
            }

            Type contextType = context.GetType();
            if (contextType.Namespace != null &&
                contextType.Namespace.StartsWith("FastAndFractured.Multiplayer", StringComparison.Ordinal))
            {
                return contextType.Name;
            }

            return string.Empty;
        }
    }
}
