#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RenameProfileWindow : EditorWindow
{
    private static string newProfileName = "";
    private static System.Action<string> onConfirm;

    public static bool currenltyOnRenameWindow = false;

    public static void ShowWindow(string currentName, System.Action<string> confirmCallback)
    {
        currenltyOnRenameWindow = true;
        newProfileName = currentName;
        onConfirm = confirmCallback;

        var window = GetWindow<RenameProfileWindow>(true, "Rename Profile");
        window.minSize = new Vector2(300, 80);
        window.maxSize = new Vector2(300, 80);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        newProfileName = EditorGUILayout.TextField("New Profile Name", newProfileName);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Confirm"))
        {
            currenltyOnRenameWindow = false;
            if (!string.IsNullOrEmpty(newProfileName))
            {
                onConfirm?.Invoke(newProfileName);//callback to the changeProfileName Fnction with the user input
                Close(); 
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid Name", "Profile name cannot be empty.", "OK");
            }
        }

        if (GUILayout.Button("Cancel"))
        {
            currenltyOnRenameWindow = false;
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
