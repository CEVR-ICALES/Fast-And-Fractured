using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    public class MultiplayerLogFilterWindow : EditorWindow
    {
        private const float SECTION_SPACING = 6f;
        private const float BUTTON_HEIGHT = 24f;
        private const float SMALL_BUTTON_HEIGHT = 22f;
        private const float ADD_BUTTON_WIDTH = 80f;
        private static readonly Vector2 MIN_WINDOW_SIZE = new Vector2(480f, 420f);

        private Vector2 _scrollPosition;
        private string _searchFilter = string.Empty;
        private string _newCategory = string.Empty;

        [MenuItem("Tools/Fast And Fractured/Diagnostics/Multiplayer Log Filter", priority = 31)]
        public static void ShowWindow()
        {
            MultiplayerLogFilterService.EnsureInitialized();
            MultiplayerLogFilterWindow window = GetWindow<MultiplayerLogFilterWindow>("MP Log Filter");
            window.minSize = MIN_WINDOW_SIZE;
            window.Show();
        }

        private void OnGUI()
        {
            MultiplayerLogFilterService.EnsureInitialized();

            EditorGUILayout.HelpBox(
                "Filtra logs por la etiqueta entre corchetes, por ejemplo: [NetworkedVehicle].\n" +
                "Los Debug.Log se mantienen en código, solo se ocultan en consola según la configuración.",
                MessageType.Info);

            DrawGlobalSettings();
            EditorGUILayout.Space(SECTION_SPACING);
            DrawCategoryActions();
            EditorGUILayout.Space(SECTION_SPACING);
            DrawCategorySearchAndAdd();
            EditorGUILayout.Space(SECTION_SPACING);
            DrawCategoryList();
        }

        private static void DrawGlobalSettings()
        {
            EditorGUI.BeginChangeCheck();
            bool filteringEnabled = EditorGUILayout.ToggleLeft("Enable log filtering", MultiplayerLogFilterService.FilteringEnabled);
            bool filterWarnings = EditorGUILayout.ToggleLeft("Also filter warnings", MultiplayerLogFilterService.FilterWarnings);
            bool showUncategorized = EditorGUILayout.ToggleLeft("Show uncategorized logs", MultiplayerLogFilterService.ShowUncategorized);

            if (EditorGUI.EndChangeCheck())
            {
                MultiplayerLogFilterService.SetGlobalSettings(filteringEnabled, filterWarnings, showUncategorized);
            }
        }

        private static void DrawCategoryActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable All", GUILayout.Height(BUTTON_HEIGHT)))
                {
                    MultiplayerLogFilterService.SetAllCategories(true);
                }

                if (GUILayout.Button("Disable All", GUILayout.Height(BUTTON_HEIGHT)))
                {
                    MultiplayerLogFilterService.SetAllCategories(false);
                }

                if (GUILayout.Button("Rescan Categories", GUILayout.Height(BUTTON_HEIGHT)))
                {
                    int addedCount = MultiplayerLogFilterService.RescanProjectCategories();
                    Debug.Log($"[MultiplayerLogFilter] Rescan finished. Added categories: {addedCount}");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Defaults", GUILayout.Height(SMALL_BUTTON_HEIGHT)))
                {
                    MultiplayerLogFilterService.ResetToDefaults();
                }
            }
        }

        private void DrawCategorySearchAndAdd()
        {
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);

            using (new EditorGUILayout.HorizontalScope())
            {
                _newCategory = EditorGUILayout.TextField("Add Category", _newCategory);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newCategory)))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(ADD_BUTTON_WIDTH)))
                    {
                        bool added = MultiplayerLogFilterService.EnsureCategory(_newCategory);
                        if (added)
                        {
                            _newCategory = string.Empty;
                        }
                    }
                }
            }
        }

        private void DrawCategoryList()
        {
            List<MultiplayerLogCategoryState> categories = MultiplayerLogFilterService.GetCategorySnapshot();
            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No categories found. Use 'Rescan Categories' or add a category manually.",
                    MessageType.None);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < categories.Count; i++)
            {
                MultiplayerLogCategoryState category = categories[i];
                if (!MatchesSearch(category.name))
                {
                    continue;
                }

                bool enabled = EditorGUILayout.ToggleLeft(category.name, category.enabled);
                if (enabled != category.enabled)
                {
                    MultiplayerLogFilterService.SetCategoryEnabled(category.name, enabled);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesSearch(string category)
        {
            if (string.IsNullOrWhiteSpace(_searchFilter))
            {
                return true;
            }

            return category.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
