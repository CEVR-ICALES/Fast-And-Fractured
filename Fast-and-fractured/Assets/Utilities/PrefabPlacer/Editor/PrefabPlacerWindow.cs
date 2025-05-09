using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utilities
{
    public class PrefabPlacerWindow : EditorWindow //handles UI and user inputs (on the editor window) 
    {
        private string[] profileNames;
        private int selectedProfileIndex = 0;
        private List<GameObject> prefabs;
        private int selectedPrefabIndex = 0;
        private Vector2 scrollPosition;
        private PrefabPlacer placer = new PrefabPlacer();

        [MenuItem("Tools/Utilities/Prefab Placer")]
        public static void ShowWindow() => GetWindow<PrefabPlacerWindow>("Prefab Placer");

        private void OnEnable()
        {
            EditorApplication.projectChanged += RefreshProfiles;
            RefreshProfiles();
            ValidateProfileSelection();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshProfiles;
            SaveSelectedProfile();
            placer.StopPlacing();
        }

        private void ValidateProfileSelection()
        {
            if (profileNames.Length > 0)
            {
                //find valid selection
                bool selectionValid = false;
                for (int i = 0; i < profileNames.Length; i++)
                {
                    if (Directory.Exists(PrefabProfileManager.GetProfilePath(profileNames[i])))
                    {
                        selectedProfileIndex = i;
                        selectionValid = true;
                        break;
                    }
                }

                if (!selectionValid)
                {
                    selectedProfileIndex = 0;
                    PrefabProfileManager.SaveSelectedProfile("");
                }
            }
            else
            {
                selectedProfileIndex = -1;
                PrefabProfileManager.SaveSelectedProfile("");
            }

            RefreshPrefabs();
            Repaint();
        }

        private void RefreshProfiles()
        {
            profileNames = PrefabProfileManager.GetAllProfileNames();
            ValidateProfileSelection();
        }

        private void LoadSelectedProfile()
        {
            var selected = PrefabProfileManager.GetSelectedProfile();
            if (!string.IsNullOrEmpty(selected))
            {
                selectedProfileIndex = System.Array.IndexOf(profileNames, selected);
                RefreshPrefabs();
            }
        }

        private void SaveSelectedProfile()
        {
            if (profileNames != null && selectedProfileIndex < profileNames.Length && selectedProfileIndex >= 0)
            {
                PrefabProfileManager.SaveSelectedProfile(profileNames[selectedProfileIndex]);
            }
        }

        private void RefreshPrefabs()//refresh prefab list 
        {
            if (profileNames.Length == 0 || selectedProfileIndex >= profileNames.Length)
            {
                prefabs = new List<GameObject>();
                return;
            }

            string selectedProfile = profileNames[selectedProfileIndex];
            prefabs = PrefabProfileManager.GetPrefabsForProfile(selectedProfile);
            selectedPrefabIndex = Mathf.Clamp(selectedPrefabIndex, 0, prefabs.Count - 1);
            Repaint();
        }

        private void OnGUI()
        {
            DrawProfileManagement();
            EditorGUILayout.Space(20);
            DrawPrefabSelection();
        }

        private void DrawProfileManagement()//profiles painting handler
        {
            EditorGUILayout.LabelField("Profile Management", EditorStyles.boldLabel);

            if (profileNames.Length > 0)
            {
                int newIndex = EditorGUILayout.Popup("Selected Profile", selectedProfileIndex, profileNames);
                if (newIndex != selectedProfileIndex)//selected profile has been changed 
                {
                    selectedProfileIndex = newIndex;
                    RefreshPrefabs();
                    SaveSelectedProfile();
                }

                //rename Button
                if (GUILayout.Button("Rename Profile"))
                {
                    ShowRenameProfileWindow();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No profiles available. Create one first.", MessageType.Info);
                if (GUILayout.Button("Create New Profile"))
                {
                    ShowProfileCreationDialog();
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Profile"))
                ShowProfileCreationDialog();

            if (GUILayout.Button("Delete Current Profile"))
                ShowProfileDeletionDialog();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowRenameProfileWindow()
        {
            if (selectedProfileIndex < 0 || selectedProfileIndex >= profileNames.Length)
                return;

            string currentName = profileNames[selectedProfileIndex];
            RenameProfileWindow.ShowWindow(currentName, OnProfileRenamed);
        }

        private void OnProfileRenamed(string newName)
        {
            if (selectedProfileIndex < 0 || selectedProfileIndex >= profileNames.Length)
                return;

            string oldName = profileNames[selectedProfileIndex];
            if (PrefabProfileManager.ChangeProfileName(oldName, newName))//funciton checks if the name is not already used or there are no errors
            {
                RefreshProfiles();
                selectedProfileIndex = System.Array.IndexOf(profileNames, newName);
                SaveSelectedProfile();
                RefreshPrefabs();
            }
        }

        private void DrawPrefabSelection()//prefab painting handler
        {
            EditorGUILayout.LabelField("Prefab Selection", EditorStyles.boldLabel);

            if (profileNames.Length == 0) return;

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Add Single Prefab"))
                    ShowPrefabSelectionDialog();


                if (GUILayout.Button("Add Folder of Prefabs"))
                    ShowFolderSelectionDialog();

            }
            EditorGUILayout.EndHorizontal();

            if (prefabs.Count > 0)
            {
                DrawPrefabGrid();
                DrawPrefabControls();
                StartPlacingSelectedPrefab();
            }
            else
            {
                EditorGUILayout.HelpBox("No prefabs in this profile", MessageType.Info);
            }
        }

        private void DrawPrefabGrid()//show prefabs in grid structure
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            int columns = Mathf.FloorToInt(EditorGUIUtility.currentViewWidth / 120);

            int newSelectedIndex = GUILayout.SelectionGrid(
                selectedPrefabIndex,
                GetPrefabThumbnails(),
                columns,
                GUILayout.Height(100 * Mathf.Ceil(prefabs.Count / (float)columns))
            );

            if (newSelectedIndex != selectedPrefabIndex)
            {
                selectedPrefabIndex = newSelectedIndex;
                StartPlacingSelectedPrefab(); //auto-start placement on selection change
            }

            EditorGUILayout.EndScrollView();
        }

        private GUIContent[] GetPrefabThumbnails()//get prefab preview images
        {
            return prefabs.ConvertAll(prefab =>
                new GUIContent(AssetPreview.GetAssetPreview(prefab), prefab.name)).ToArray();
        }

        private void DrawPrefabControls()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Place Prefab"))
                StartPlacingSelectedPrefab();

            if (GUILayout.Button("Remove Prefab"))
                ShowPrefabRemovalConfirmation();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowProfileCreationDialog()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Profile",
                "NewProfile",
                "",
                "Enter profile name",
                PrefabProfileManager.PROFILE_FOLDER_PATH);

            if (!string.IsNullOrEmpty(path))
            {
                string profileName = Path.GetFileNameWithoutExtension(path);
                PrefabProfileManager.CreateProfile(profileName);
                RefreshProfiles();
                selectedProfileIndex = System.Array.IndexOf(profileNames, profileName);
                RefreshPrefabs();
            }
        }

        private void ShowProfileDeletionDialog()
        {
            if (EditorUtility.DisplayDialog("Confirm Deletion",
                $"Delete profile '{profileNames[selectedProfileIndex]}'?", "Delete", "Cancel"))
            {
                PrefabProfileManager.DeleteProfile(profileNames[selectedProfileIndex]);
                RefreshProfiles();
                RefreshPrefabs();
            }
        }

        private void ShowPrefabSelectionDialog()
        {
            string path = EditorUtility.OpenFilePanel("Select Prefab",
                Application.dataPath, "prefab");

            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                PrefabProfileManager.AddPrefabToProfile(
                    profileNames[selectedProfileIndex],
                    relativePath);
                RefreshPrefabs();
            }
        }

        private void ShowFolderSelectionDialog()
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select Folder with Prefabs",
            Application.dataPath, "");

            if (!string.IsNullOrEmpty(folderPath))
            {
                string relativeFolderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);

                // search for all the .prefab in the secleted folder
                string[] allFiles = Directory.GetFiles(folderPath, "*.prefab", SearchOption.AllDirectories);

                if (allFiles.Length == 0)
                {
                    EditorUtility.DisplayDialog("No Prefabs Found", $"Folder: {folderPath} is empty",
                         "OK");
                    return;
                }

                int addedCount = 0;

                foreach (string filePath in allFiles)
                {
                    // normalize to desired slashes format
                    string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length)
                                           .Replace('\\', '/'); 

                    PrefabProfileManager.AddPrefabToProfile(
                        profileNames[selectedProfileIndex],
                        relativePath);

                    addedCount++;
                }

                RefreshPrefabs();
                EditorUtility.DisplayDialog("Prefabs Added",
                    $"{addedCount} prefabs added from the folder", "OK");
            }
        }

        private void ShowPrefabRemovalConfirmation()
        {
            if (prefabs.Count == 0 || selectedPrefabIndex >= prefabs.Count) return;

            if (EditorUtility.DisplayDialog("Confirm Removal",
                "Remove selected prefab from profile?", "Remove", "Cancel"))
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefabs[selectedPrefabIndex]);
                PrefabProfileManager.RemovePrefabFromProfile(
                    profileNames[selectedProfileIndex],
                    prefabPath);
                RefreshPrefabs();
            }
        }

        private void StartPlacingSelectedPrefab()
        {
            if (selectedPrefabIndex >= 0 && selectedPrefabIndex < prefabs.Count)
            {
                placer.StopPlacing();
                placer.StartPlacing(prefabs[selectedPrefabIndex]);
            }
        }
    }
}

