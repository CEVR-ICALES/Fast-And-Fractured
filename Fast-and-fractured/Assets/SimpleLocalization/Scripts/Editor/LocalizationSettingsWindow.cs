using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.SimpleLocalization.Scripts.Editor
{
    public class LocalizationSettingsWindow : EditorWindow
    {
        private static SerializedObject _serializedObject;

        private static LocalizationSettings Settings => LocalizationSettings.Instance;

        [MenuItem("Window/◆ Simple Localization/Settings")]
        public static void ShowWindow()
        {
            GetWindow<LocalizationSettingsWindow>("Localization Settings");
        }

        [MenuItem("Window/◆ Simple Localization/Reset")]
        public static void ResetSettings()
        {
            if (EditorUtility.DisplayDialog("Simple Localization", "Do you want to reset settings?", "Yes", "No"))
            {
                LocalizationSettings.Instance.Reset();
            }
        }

        [MenuItem("Window/◆ Simple Localization/Help")]
        public static void Help()
        {
            Application.OpenURL("https://github.com/hippogamesunity/SimpleLocalization/wiki");
        }

        public void OnGUI()
        {
            MakeSettingsWindow();
        }
        
        private void MakeSettingsWindow()
        {
            minSize = new Vector2(300, 500);
            Settings.DisplayHelp();
            Settings.TableId = EditorGUILayout.TextField("Table Id", Settings.TableId, GUILayout.MinWidth(200));
            DisplaySheets();
            Settings.SaveFolder = EditorGUILayout.ObjectField("Save Folder", Settings.SaveFolder, typeof(Object), false);
            Settings.DisplayButtons();
            Settings.DisplayWarnings();
        }

        private static void DisplaySheets()
        {
            if (_serializedObject == null || _serializedObject.targetObject == null)
            {
                _serializedObject = new SerializedObject(Settings);
            }
            else
            {
                _serializedObject.Update();
            }

            var property = _serializedObject.FindProperty("Sheets");

            EditorGUILayout.PropertyField(property, new GUIContent("Sheets"), true);

            if (property.isArray)
            {
                property.Next(true);
                property.Next(true);

                var length = property.intValue;

                property.Next(true);
                
                Settings.Sheets.Clear();

                var lastIndex = length - 1;

                for (var i = 0; i < length; i++)
                {
                    Settings.Sheets.Add(new Sheet
                    {
                        Name = property.FindPropertyRelative("Name").stringValue,
                        Id = property.FindPropertyRelative("Id").longValue,
                        TextAsset = property.FindPropertyRelative("TextAsset").objectReferenceValue as TextAsset
                    });

                    if (i < lastIndex)
                    {
                        property.Next(false);
                    }
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }
    }
}