#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PrefabPlacerWindow : EditorWindow //handles UI and user inputs (on the editor window) 
{

    // GENERAL VARIABLES
    private string[] profileNames;
    private int selectedProfileIndex = 0;
    private Vector2 scrollPosition;
    private PrefabModeSinglePlacer placer = new PrefabModeSinglePlacer();
    private PrefabModeEraser eraser = new PrefabModeEraser();
    private PrefabModeMultiplePlacer multiplePlacer = new PrefabModeMultiplePlacer();
    public ProfilePlacedObjectsTrackerSO activeTracker;
    private List<GameObject> prefabs;

    // MODES
    private ToolMode currentToolMode = ToolMode.SinglePlace;
    private ToolMode previousToolMode = ToolMode.Erase;
    private enum ToolMode
    {
        SinglePlace,
        Erase,
        MultiplePlace
    }
    private Dictionary<ToolMode, IPrefabPlacerMode> modeHandlers;
    private float eraserRadius = 2f;

    private float multiplePlacingRadius = 2f;
    private float multiplePlacingDensity = 1f;
    private float multiplePlacingSeparation = 1f;

    //PREFAB SELECTION
    private int selectedPrefabIndex = 0;
    private bool canSelectMultiplePrefabIndex = false;
    private List<int> selectedPrefabIndexList = new List<int>();

    private const float PREFAB_BUTTON_WIDTH = 110f;
    private const float PREFAB_BUTTON_HEIGHT = 100f;
    private const float PREFAB_PADDING = 10f;
    private Color highlightColor = new Color(0.5f, 0.7f, 1f);

    [Header("Constants")]
    private const float MIN_WINDOW_HEIGHT = 350f;
    private const float MIN_WINDOW_WIDTH = 400f;

    private const float DRAG_AND_DROP_MIN_HEIGHT = 50f;
    private const float DRAG_AND_DROP_MAX_HEIGHT = 50f;

    [MenuItem("Tools/Utilities/Prefab Placer")]
    public static void ShowWindow() => GetWindow<PrefabPlacerWindow>("Prefab Placer");

    private void OnEnable()
    {
        minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        EditorApplication.projectChanged += RefreshProfiles;
        InitializeModeDictionary();
        previousToolMode = currentToolMode;
        RefreshProfiles();
        LoadSelectedProfile();
        ValidateProfileSelection();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= RefreshProfiles;
        SaveSelectedProfile();
        placer.StopPlacing();
        eraser.StopErasing();
        multiplePlacer.StopPlacing();
    }

    private void InitializeModeDictionary()
    {
        modeHandlers = new Dictionary<ToolMode, IPrefabPlacerMode>
        {
            { ToolMode.SinglePlace, placer},
            { ToolMode.Erase, eraser},
            { ToolMode.MultiplePlace, multiplePlacer}
        };
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
            activeTracker = null;
            return;
        }

        string selectedProfile = profileNames[selectedProfileIndex];
        prefabs = PrefabProfileManager.GetPrefabsForProfile(selectedProfile);
        selectedPrefabIndex = Mathf.Clamp(selectedPrefabIndex, 0, prefabs.Count - 1);

        activeTracker = PrefabProfileManager.GetTrackerForProfile(selectedProfile); // load current tracker SO
        Repaint();
    }

    private void OnGUI()
    {
        DrawProfileManagement();
        EditorGUILayout.Space(20);
        DrawToolModeSlector();
        DrawToolModeSettings();
        //DrawPrefabSelection();
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

            EditorGUILayout.BeginHorizontal();
            //rename Button
            if (GUILayout.Button("Rename Profile"))
                ShowRenameProfileWindow();                    

            if (GUILayout.Button("Create New Profile"))
                ShowProfileCreationDialog();

            if (GUILayout.Button("Delete Current Profile"))
                ShowProfileDeletionDialog();

            EditorGUILayout.EndHorizontal();
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

    private void DrawToolModeSlector()
    {
        EditorGUILayout.LabelField("Tool Mode", EditorStyles.boldLabel);
        currentToolMode = (ToolMode)EditorGUILayout.EnumPopup("Current Mode", currentToolMode);
    }

    private void DrawToolModeSettings()
    {
        
        if(currentToolMode != previousToolMode)
        {
            if(modeHandlers.TryGetValue(previousToolMode, out IPrefabPlacerMode oldMode))
            {
                oldMode.OnModeDeactivated();
            }

            if(modeHandlers.TryGetValue(currentToolMode, out IPrefabPlacerMode newMode))
            {
                newMode.OnModeActivated(activeTracker);
            }

            previousToolMode = currentToolMode;
        }
        //StopAllToolModes();
        switch (currentToolMode)
        {
            case ToolMode.SinglePlace:
                //eraser.StopErasing();
                canSelectMultiplePrefabIndex = false;
                DrawPrefabSelection();
                break;

            case ToolMode.Erase:
                //placer.StopPlacing();
                DrawEraseSettings();
                break;

            case ToolMode.MultiplePlace:
                canSelectMultiplePrefabIndex = true;
                List<GameObject> selectedObjects = selectedPrefabIndexList
                .Where(index => index >= 0 && index < prefabs.Count)
                .Select(index => prefabs[index])
                .ToList();
                multiplePlacer.UpdatePrefabSelection(selectedObjects, activeTracker);
                DrawMutiplePlacingSettings();
                // need to manually draw each prefab thumbnail to treat it as either a button or a toggle, since a selection grid doesnt support multiple selections
                break;
        }
    }

    #region Erase Mode
    private void DrawEraseSettings()
    {
        EditorGUILayout.LabelField("Eraser Settings", EditorStyles.boldLabel);
        eraserRadius = EditorGUILayout.Slider("Eraser Radius", eraserRadius, 0.1f, 50f);
        eraser.eraserRadius = eraserRadius;
        eraser.StartErasing(activeTracker);
    }

    #endregion

    #region Multiple Prefab Placing

    private Vector2 SettingsRandomScale(string label, Vector2 currentValues)
    {
        float minVal = currentValues.x;
        float maxVal = currentValues.y;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 100));

        EditorGUILayout.LabelField("Min", GUILayout.Width(30));
        minVal = EditorGUILayout.FloatField(minVal, GUILayout.Width(50));   
        
        EditorGUILayout.LabelField("Max", GUILayout.Width(30));
        maxVal = EditorGUILayout.FloatField(maxVal, GUILayout.Width(50));

        EditorGUILayout.EndHorizontal();

        minVal = Mathf.Max(0.01f, minVal);
        maxVal = Mathf.Max(0.01f, maxVal);
        if(minVal > maxVal)
        {
            maxVal = minVal;
        }

        return new Vector2(minVal, maxVal);
    }

    private void DrawMutiplePlacingSettings()
    {
        EditorGUILayout.LabelField("Mutiple Placing Settings", EditorStyles.boldLabel);
        multiplePlacingDensity = EditorGUILayout.Slider("Placing Density", multiplePlacingDensity, 0.1f, 50f);
        multiplePlacingRadius = EditorGUILayout.Slider("Placing Radius", multiplePlacingRadius, 0.1f, 50f);
        multiplePlacingSeparation = EditorGUILayout.Slider("Placing Spacing", multiplePlacingSeparation, 0.1f, 50f);

        multiplePlacer.scaleValues = SettingsRandomScale("Scalings", multiplePlacer.scaleValues);
        multiplePlacer.placingRadius = multiplePlacingRadius;
        multiplePlacer.density = multiplePlacingDensity;
        multiplePlacer.placingSeparation = multiplePlacingSeparation;

        DrawMultiplePrefabSelection();
    }
    private void DrawMultiplePrefabSelection()
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            return; // Should be handled by the calling method (DrawPrefabListAndControls)
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 20f) / (PREFAB_BUTTON_WIDTH + PREFAB_PADDING))); // -20 for potential scrollbar

        GUIContent[] thumbnails = GetPrefabThumbnails(); 
        bool selectionChanged = false;

        // Manual grid layout
        int totalRows = Mathf.CeilToInt((float)prefabs.Count / columns);
        float gridHeight = totalRows * (PREFAB_BUTTON_HEIGHT + PREFAB_PADDING);
        Rect gridRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.currentViewWidth - 20f, gridHeight, gridHeight);

        Event currentEvent = Event.current; // cache current event

        for (int i = 0; i < prefabs.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;

            Rect buttonRect = new Rect(
                gridRect.x + col * (PREFAB_BUTTON_WIDTH + PREFAB_PADDING) + (PREFAB_PADDING / 2f), // ddd some padding at the start
                gridRect.y + row * (PREFAB_BUTTON_HEIGHT + PREFAB_PADDING) + (PREFAB_PADDING / 2f),
                PREFAB_BUTTON_WIDTH,
                PREFAB_BUTTON_HEIGHT
            );

            Color originalBackgroundColor = GUI.backgroundColor;
            bool isCurrentlySelected = false;

            if (canSelectMultiplePrefabIndex)
            {
                isCurrentlySelected = selectedPrefabIndexList.Contains(i);
            }
            else
            {
                isCurrentlySelected = (selectedPrefabIndex == i);
            }

            if (isCurrentlySelected)
            {
                GUI.backgroundColor = highlightColor; 
            }

            if (GUI.Button(buttonRect, thumbnails[i]))
            {
                selectionChanged = true;
                if (canSelectMultiplePrefabIndex)
                {
                    if (currentEvent.control || currentEvent.command) // Ctrl/Cmd click for toggle
                    {
                        if (isCurrentlySelected)
                        {
                            selectedPrefabIndexList.Remove(i);
                        }
                        else
                        {
                            selectedPrefabIndexList.Add(i);
                        }
                    }
                    else if (currentEvent.shift && selectedPrefabIndexList.Count > 0) // Shift click for range
                    {
                        int anchorIndex = selectedPrefabIndexList.Last(); 
                        int start = Mathf.Min(anchorIndex, i);
                        int end = Mathf.Max(anchorIndex, i);
                        for (int j = start; j <= end; j++)
                        {
                            if (!selectedPrefabIndexList.Contains(j))
                            {
                                selectedPrefabIndexList.Add(j);
                            }
                        }
                    }
                    else // normal click in multi-select mode
                    {
                        // if already selected and it's not the only one, or if not selected at all
                        if (!isCurrentlySelected || selectedPrefabIndexList.Count > 1)
                        {
                            selectedPrefabIndexList.Clear();
                            selectedPrefabIndexList.Add(i);
                        }
                        else // clicking the only selected item (already in the list)
                        {
                            selectedPrefabIndexList.Remove(i);
                        }
                    }
                }
                else 
                {
                    if (selectedPrefabIndex == i && !currentEvent.control && !currentEvent.command && !currentEvent.shift)
                    {
                       
                    }
                    selectedPrefabIndex = i; 
                }
            }
            GUI.backgroundColor = originalBackgroundColor; // Restore original color
        }

        EditorGUILayout.EndScrollView();

        if (selectionChanged)
        {
            if (canSelectMultiplePrefabIndex)
            {
                // notify that the list of selected prefabs has changed.
                Debug.Log("Multiple prefab selection changed. Count: " + selectedPrefabIndexList.Count + ". Indices: " + string.Join(", ", selectedPrefabIndexList));
                StartPlacingSelectedPrefabsList();
            }
            Repaint();
        }
    }


    #endregion
    #region Prefab list & handeling
    private void DrawPrefabSelection()//prefab painting handler
    {
        DetectPrefabDrop();

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
        //placer.scaleValues = SettingsRandomScale("Scale settings", placer.scaleValues);

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

    private void DetectPrefabDrop()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 1000, DRAG_AND_DROP_MIN_HEIGHT, DRAG_AND_DROP_MAX_HEIGHT);
        GUI.Box(dropArea, "Drag prefabs here to add to profile", EditorStyles.helpBox);

        // detect Drag-and-Drop
        Event evt = Event.current;
        if (dropArea.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    float progress;
                    float count = 0;
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        string path = AssetDatabase.GetAssetPath(draggedObject);
                        progress = (float) count / DragAndDrop.objectReferences.Length;
                        EditorUtility.DisplayProgressBar("Addings prefabs...", $"Addings prefab {path}", progress);

                        if (PrefabUtility.GetPrefabAssetType(draggedObject) != PrefabAssetType.NotAPrefab)
                        {
                            PrefabProfileManager.AddPrefabToProfile(profileNames[selectedProfileIndex], path);
                        }
                        count++;
                    }

                    EditorUtility.ClearProgressBar();

                    RefreshPrefabs();
                }

                evt.Use();
            }
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
        if (prefabs == null) return new GUIContent[0];

        GUIContent[] thumbnails = new GUIContent[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(prefabs[i]);
                // Tooltip will show prefab name. Image is the preview.
                thumbnails[i] = new GUIContent(preview, prefabs[i].name);
            }
            else
            {
                thumbnails[i] = new GUIContent("Missing Prefab " + i); // Placeholder for missing prefabs
            }
        }
        return thumbnails;
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

    #endregion
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
                EditorUtility.DisplayDialog("No Prefabs Found",$"{folderPath} is eempty",
                     "OK");
                return;
            }

            int addedCount = 0;
            float progress;


            foreach (string filePath in allFiles)
            {
                // Normalize path to Unity format
                string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length).Replace('\\', '/'); // Convert backslashes to forward slashe
                progress = (float) addedCount / allFiles.Length;
                EditorUtility.DisplayProgressBar("Adding prefabs...", $"Processing {relativePath}", progress);
                PrefabProfileManager.AddPrefabToProfile(
                    profileNames[selectedProfileIndex],
                    relativePath);

                addedCount++;
            }
            EditorUtility.ClearProgressBar(); // ALWAYS CLEAR PROGRESS BAR!!!

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
            placer.StartPlacing(prefabs[selectedPrefabIndex], activeTracker);
        }
    }

    private void StartPlacingSelectedPrefabsList()
    {
        List<GameObject> selectedObjects = selectedPrefabIndexList
            .Where(index => index >= 0 && index < prefabs.Count)
            .Select(index => prefabs[index])
            .ToList();
        multiplePlacer.UpdatePrefabSelection(selectedObjects, activeTracker);
    }

    #region UTILITIES

    private void StopAllToolModes()
    {
        foreach(var mode in modeHandlers)
        {
            mode.Value.OnModeDeactivated();
        }
    }

    #endregion
}

#endif