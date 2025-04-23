using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;


namespace Utilities
{
    public class FBXToPrefabWindow : EditorWindow
    {
        // Note: all the folders are relative to selection
        private const string MATERIALS_FOLDER_NAME = "Materials";
        private const string TEXTURES_FOLDER_NAME = "Textures";
        private const string PREFABS_FOLDER_NAME = "Prefabs";
        private const string VISUALS_NODE_NAME = "Visuals";
        private const string MODEL_NODE_NAME = "Model";
        private const string BASE_PREFAB_VISUALS_TARGET = "Visuals";
        private const string BASEMAP_SUFFIX = "_BaseMap";
        private const string EMISSIVE_SUFFIX = "_Emissive";
        private const string MASKMAP_SUFFIX = "_MaskMap";
        private const string NORMALMAP_SUFFIX = "_Normal";
        private const string SHADER_HDRP_LIT = "HDRP/Lit";
        private const string PROP_BASECOLORMAP = "_BaseColorMap";
        private const string PROP_MASKMAP = "_MaskMap";
        private const string PROP_NORMALMAP = "_NormalMap";
        private const string PROP_EMISSIVEMAP = "_EmissiveColorMap";
        private const string PROP_EMISSIVECOLOR = "_EmissiveColor";
        private const string KEYWORD_NORMALMAP = "_NORMALMAP";
        private const string KEYWORD_EMISSION = "_EMISSION";
        private const string KEYWORD_MASKMAP = "_MASKMAP";
        private const string DEFAULT_COMMON_BASE_NAME = "Shared_Base";

        // Tool related 
        private List<GameObject> _selectedFBXAssets = new List<GameObject>();
        private GameObject _basePrefab = null;
        private Vector2 _scrollPosition;
        private Vector2 _selectionScrollPos;

        // Texture Override related
        private bool _useManualTextureOverrides = false;
        private Texture2D _manualBaseMap = null;
        private Texture2D _manualNormalMap = null;
        private Texture2D _manualMaskMap = null;
        private Texture2D _manualEmissiveMap = null;
        private bool _useSharedMaterial = false;

        private Material _overrideMaterial = null;

        [MenuItem("Tools/Utilities/FBX To Prefab Tool (HDRP)", false, 50)]
        public static void ShowWindow()
        {
            GetWindow<FBXToPrefabWindow>("FBX To Prefab (HDRP)");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("FBX To Prefab/Variant Creator (HDRP) - Batch & Manual Textures",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select FBX/OBJ(s). Use 'Process Selected' for standard/existing-variant creation. " +
                "Use 'Create Common Base + Variants' for a new shared base. " +
                "Enable 'Manual Texture Overrides' to apply specific textures to the entire selection.",
                MessageType.Info);


            EditorGUILayout.LabelField($"Selected FBX/OBJ Assets: {_selectedFBXAssets.Count}", EditorStyles.boldLabel);
            if (_selectedFBXAssets.Count > 0)
            {
                DrawSelectionList();
            }
            else
            {
                EditorGUILayout.HelpBox("Select FBX or OBJ file(s) in the Project view.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            _basePrefab = (GameObject)EditorGUILayout.ObjectField("Use Existing Base Prefab (Optional)", _basePrefab,
                typeof(GameObject), false);
            DrawBasePrefabHelp();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Existing Material Override (Optional)", EditorStyles.boldLabel);
            _overrideMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Override Material",
                    "If set, this existing material will be used for ALL processed models, ignoring texture settings and material creation."),
                _overrideMaterial,
                typeof(Material),
                false
            );
            using (new EditorGUI.DisabledScope(_overrideMaterial != null))
            {
                EditorGUILayout.LabelField("Manual Texture Overrides (for entire batch)", EditorStyles.boldLabel);
                _useManualTextureOverrides =
                    EditorGUILayout.BeginToggleGroup("Enable Manual Overrides", _useManualTextureOverrides);
                EditorGUI.indentLevel++;
                _manualBaseMap =
                    (Texture2D)EditorGUILayout.ObjectField("Base Map", _manualBaseMap, typeof(Texture2D), false);
                _manualNormalMap =
                    (Texture2D)EditorGUILayout.ObjectField("Normal Map", _manualNormalMap, typeof(Texture2D), false);
                _manualMaskMap =
                    (Texture2D)EditorGUILayout.ObjectField("Mask Map", _manualMaskMap, typeof(Texture2D), false);
                _manualEmissiveMap =
                    (Texture2D)EditorGUILayout.ObjectField("Emissive Map", _manualEmissiveMap, typeof(Texture2D),
                        false);
            }

            using (new EditorGUI.DisabledScope(!_useManualTextureOverrides))
            {
                _useSharedMaterial = EditorGUILayout.Toggle(
                    new GUIContent("Use One Shared Material",
                        "If enabled, creates ONE material for the entire batch using the manual textures above, instead of one material per FBX."),
                    _useSharedMaterial);
            }


            if (_useManualTextureOverrides &&
                (_manualBaseMap || _manualNormalMap || _manualMaskMap || _manualEmissiveMap))
            {
                EditorGUILayout.HelpBox(
                    "Manually assigned textures WILL OVERRIDE automatic detection for those slots for ALL processed models.",
                    MessageType.Warning);
                if (_useSharedMaterial)
                {
                    EditorGUILayout.HelpBox(
                        "A single material asset will be created and shared by all prefabs in this batch.",
                        MessageType.Info);
                }
            }
            else if (_useManualTextureOverrides)
            {
                EditorGUILayout.HelpBox(
                    "Toggle enabled, but no textures assigned. Automatic detection will still be used for all slots.",
                    MessageType.Info);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.Space(15);


            bool canProcess = _selectedFBXAssets.Count > 0;
            using (new EditorGUI.DisabledScope(!canProcess))
            {
                DrawProcessSelectedButton();
                EditorGUILayout.Space(5);
                DrawCreateCommonBaseButton();
            }

            EditorGUILayout.EndScrollView();
        }

        #region GUI Helpers

        private void DrawSelectionList()
        {
            _selectionScrollPos = EditorGUILayout.BeginScrollView(_selectionScrollPos,
                GUILayout.Height(Mathf.Min(_selectedFBXAssets.Count * 20f + 5f, 100f)), GUILayout.ExpandWidth(true));
            EditorGUI.indentLevel++;
            foreach (var fbx in _selectedFBXAssets)
            {
                EditorGUILayout.LabelField(fbx.name);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndScrollView();
        }

        private void DrawBasePrefabHelp()
        {
            if (_basePrefab != null)
            {
                if (!PrefabUtility.IsPartOfPrefabAsset(_basePrefab))
                {
                    EditorGUILayout.HelpBox("Selected object is not a prefab asset.", MessageType.Error);
                    _basePrefab = null;
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"PROCESS: Creates VARIANTS of '{_basePrefab.name}', replacing '{BASE_PREFAB_VISUALS_TARGET}'.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("PROCESS: Creates STANDARD prefabs (Root -> Visuals -> Model).",
                    MessageType.Info);
            }
        }

        private void DrawProcessSelectedButton()
        {
            string processButtonText = $"Process Selected ({_selectedFBXAssets.Count})";
            if (_basePrefab != null) processButtonText += " as Variants";
            else processButtonText += " as Standard Prefabs";

            if (_useManualTextureOverrides &&
                (_manualBaseMap || _manualNormalMap || _manualMaskMap || _manualEmissiveMap))
            {
                processButtonText += " [Manual Textures]";
            }

            if (GUILayout.Button(processButtonText, GUILayout.Height(35)))
            {
                ProcessSelection();
            }
        }

        private void DrawCreateCommonBaseButton()
        {
            bool canCreateCommonBase = _selectedFBXAssets.Count > 1;
            using (new EditorGUI.DisabledScope(!canCreateCommonBase))
            {
                string commonBaseButtonText = $"Create Common Base + Variants ({_selectedFBXAssets.Count})";
                if (_useManualTextureOverrides &&
                    (_manualBaseMap || _manualNormalMap || _manualMaskMap || _manualEmissiveMap))
                {
                    commonBaseButtonText += " [Manual Textures]";
                }

                if (GUILayout.Button(commonBaseButtonText, GUILayout.Height(35)))
                {
                    CreateCommonBaseAndProcessVariants();
                }
            }

            if (!canCreateCommonBase && _selectedFBXAssets.Count > 0)
            {
                EditorGUILayout.HelpBox("Select at least two FBX/OBJ files to enable 'Create Common Base'.",
                    MessageType.None);
            }
        }

        #endregion

        #region Selection Handling

        private void OnSelectionChange()
        {
            _selectedFBXAssets.Clear();
            var selectedObjects = Selection.GetFiltered<GameObject>(SelectionMode.Assets);

            foreach (var obj in selectedObjects)
            {
                if (IsValidModelAsset(obj))
                {
                    _selectedFBXAssets.Add(obj);
                }
            }

            Repaint();
        }

        private bool IsValidModelAsset(GameObject obj)
        {
            if (obj == null) return false;
            string path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) &&
                   (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Core Processing Logic (Instance Methods)

        /// <summary> Process all selected assets using current settings (base prefab, manual textures). </summary>
        private void ProcessSelection()
        {
            if (_selectedFBXAssets.Count == 0)
            {
                return;
            }

            Material sharedBatchMaterial = null;
            int successCount = 0;
            int failCount = 0;

            if (_overrideMaterial == null &&
                _useManualTextureOverrides && _useSharedMaterial && _selectedFBXAssets.Count > 0)
            {
                sharedBatchMaterial = CreateAndPrepareSharedMaterial("Shared_Batch_Mat");
                if (sharedBatchMaterial == null)
                {
                    Debug.LogError(
                        "Shared material creation failed (not cancelled by user). Aborting batch process.");
                    return;
                }
            }


            AssetDatabase.StartAssetEditing();
            try
            {
                float total = _selectedFBXAssets.Count;
                string progressBarTitle = _basePrefab != null ? "Creating Variants" : "Creating Standard Prefabs";
                if (_useManualTextureOverrides) progressBarTitle += " (Manual Textures)";

                for (int i = 0; i < _selectedFBXAssets.Count; i++)
                {
                    GameObject fbxAsset = _selectedFBXAssets[i];
                    if (EditorUtility.DisplayCancelableProgressBar(progressBarTitle,
                            $"Processing {fbxAsset.name} ({i + 1}/{total})", (i + 1) / total))
                    {
                        Debug.LogWarning("FBX Processor (HDRP): Batch processing cancelled.");
                        break;
                    }

                    if (ProcessSingleFBX(fbxAsset, _basePrefab, sharedBatchMaterial)) successCount++;
                    else failCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"FBX Processor (HDRP): Batch process finished. Successful: {successCount}, Failed: {failCount}");
            }
        }

        /// <summary> Creates a NEW base, then processes selected assets as variants of it using current settings. </summary>
        private void CreateCommonBaseAndProcessVariants()
        {
            if (_selectedFBXAssets.Count <= 1)
            {
                return;
            }

            GameObject newlyCreatedBasePrefab = CreateNewEmptyBasePrefab();
            if (newlyCreatedBasePrefab == null)
            {
                Debug.LogError("FBX Processor (HDRP): Aborting variant creation, failed to create common base.");
                return;
            }

            Material sharedBatchMaterial = null;
            if (_overrideMaterial == null && _useManualTextureOverrides && _useSharedMaterial && _selectedFBXAssets.Count > 0)
            {
                sharedBatchMaterial = CreateAndPrepareSharedMaterial("Shared_BaseVariant_Mat");
                if (sharedBatchMaterial == null)  
                {
                    Debug.LogWarning("Shared material creation cancelled or failed. Proceeding with unique materials if possible.");
                }
            }

            int successCount = 0;
            int failCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                float total = _selectedFBXAssets.Count;
                string progressBarTitle = "Creating Variants from New Base";
                if (_useManualTextureOverrides) progressBarTitle += " (Manual Textures)";

                for (int i = 0; i < _selectedFBXAssets.Count; i++)
                {
                    GameObject fbxAsset = _selectedFBXAssets[i];
                    if (EditorUtility.DisplayCancelableProgressBar(progressBarTitle,
                            $"Processing {fbxAsset.name} ({i + 1}/{total})", (i + 1) / total))
                    {
                        Debug.LogWarning("FBX Processor (HDRP): Variant creation cancelled.");
                        break;
                    }

                    if (ProcessSingleFBX(fbxAsset, newlyCreatedBasePrefab,sharedBatchMaterial)) successCount++;
                    else failCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"FBX Processor (HDRP): Common base variant creation finished. Successful: {successCount}, Failed: {failCount}");
                Selection.activeObject = newlyCreatedBasePrefab;
            }
        }

        /// <summary> Processes a single FBX asset: Creates Material, Finds/Assigns Textures (respecting manual override), Creates Prefab/Variant. </summary>
        /// <returns>True if successful, False otherwise.</returns>
        private bool ProcessSingleFBX(GameObject fbxAsset, GameObject optionalBasePrefab,
            Material sharedMaterial = null)
        {
            if (fbxAsset == null || !IsValidModelAsset(fbxAsset)) return false;

            string assetPath = AssetDatabase.GetAssetPath(fbxAsset);
            string assetFolder = Path.GetDirectoryName(assetPath);
            string parentFolder = Directory.GetParent(assetFolder)?.FullName;
            if (string.IsNullOrEmpty(parentFolder) || !parentFolder.StartsWith(Application.dataPath.Replace('/', '\\')))
            {
                Debug.LogError($"Invalid parent: {assetPath}");
                return false;
            }

            string relativeParentFolder = "Assets" + parentFolder.Substring(Application.dataPath.Length);
            relativeParentFolder = relativeParentFolder.Replace('\\', '/');
            string materialsFolderPath = Path.Combine(relativeParentFolder, MATERIALS_FOLDER_NAME).Replace('\\', '/');
            string texturesFolderPath =
                Path.Combine(relativeParentFolder, TEXTURES_FOLDER_NAME).Replace('\\', '/');
            string prefabsFolderPath = Path.Combine(relativeParentFolder, PREFABS_FOLDER_NAME).Replace('\\', '/');
            EnsureFolderExists(relativeParentFolder, MATERIALS_FOLDER_NAME);
            EnsureFolderExists(relativeParentFolder, TEXTURES_FOLDER_NAME);
            EnsureFolderExists(relativeParentFolder, PREFABS_FOLDER_NAME);

            string modelName = Path.GetFileNameWithoutExtension(assetPath);

            Material materialToUse = null;
            bool createdUniqueMaterial = false;
            bool setupTexturesForThisMaterial = false;
           
            if (_overrideMaterial != null)
            {
                if (!AssetDatabase.Contains(_overrideMaterial)) { Debug.LogError($"Override Mat '{_overrideMaterial.name}' invalid. Skip {modelName}."); return false; }
                materialToUse = _overrideMaterial;
                Debug.Log($"Decision for {modelName}: USING EXPLICIT OVERRIDE MATERIAL '{materialToUse.name}'.");
                setupTexturesForThisMaterial = false; 
            }
            else 
            if (sharedMaterial != null)
            {
                if (!AssetDatabase.Contains(sharedMaterial)) { Debug.LogError($"Passed shared Mat '{sharedMaterial.name}' is invalid/deleted. Creating unique for {modelName}."); }
                else {
                    materialToUse = sharedMaterial;
                    Debug.Log($"Decision for {modelName}: USING BATCH SHARED MATERIAL '{materialToUse.name}'.");
                    setupTexturesForThisMaterial = false; 
                }
            }
            
            
            if (materialToUse == null) // If we haven't assigned a material yet...
            {
                string materialPath = Path.Combine(materialsFolderPath, $"{modelName}_Mat.mat").Replace('\\', '/');
                materialToUse = CreateHDRPMaterial(materialPath);
                if (materialToUse == null)
                {
                    Debug.LogError($"Unique material creation failed: {modelName}");
                    return false;
                }

                materialToUse.enableInstancing = true;

                this.FindAndAssignTexturesHDRP(materialToUse, texturesFolderPath, modelName);
                EditorUtility.SetDirty(materialToUse);
                if (materialToUse == null)
                {
                    Debug.LogError($"Material creation failed: {modelName}");
                    return false;
                }
                Debug.Log($"Decision for {modelName}: CREATING UNIQUE MATERIAL '{materialToUse.name}'.");
                setupTexturesForThisMaterial = true;
             }
            if (setupTexturesForThisMaterial)
            {
                Debug.Log($"   -> Setting up textures for unique material '{materialToUse.name}'...");
                this.FindAndAssignTexturesHDRP(materialToUse, texturesFolderPath, modelName);  
                EditorUtility.SetDirty(materialToUse);  
            }

             string prefabPath = Path.Combine(prefabsFolderPath, $"{modelName}.prefab").Replace('\\', '/');
            bool success;
            Debug.Log($"   -> Creating Prefab/Variant '{prefabPath}' using material '{materialToUse.name}'...");
            if (optionalBasePrefab == null)
            {
                success = this.CreateStandardPrefab(fbxAsset, materialToUse, prefabPath, modelName);
            }
            else
            {
                if (!PrefabUtility.IsPartOfPrefabAsset(optionalBasePrefab)) {  return false; }
                success = this.CreateVariantPrefab(fbxAsset, materialToUse, optionalBasePrefab, prefabPath, modelName);
            }

            Debug.Log($"--- Finished Processing: {modelName} - Success: {success} ---");
            return success;
        }

        #endregion

        #region Prefab Creation Helpers (Instance Methods)

        private bool CreateStandardPrefab(GameObject fbxSource, Material material, string prefabPath, string baseName)
        {
            GameObject modelInstance = null;
            GameObject prefabRoot = null;
            try
            {
                modelInstance = Instantiate(fbxSource);
                modelInstance.name = MODEL_NODE_NAME;
                prefabRoot = new GameObject(baseName);
                GameObject visualsContainer = new GameObject(VISUALS_NODE_NAME);
                visualsContainer.transform.SetParent(prefabRoot.transform, false);
                modelInstance.transform.SetParent(visualsContainer.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                AssignMaterialToRenderers(modelInstance, material);
                return SavePrefab(prefabRoot, prefabPath, $"standard prefab '{baseName}'");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Std Prefab Exception {baseName}: {e.Message}");
                return false;
            }
            finally
            {
                if (prefabRoot != null) DestroyImmediate(prefabRoot);
            }
        }

        private bool CreateVariantPrefab(GameObject fbxSource, Material newMaterial, GameObject basePrefabAsset,
            string variantPath, string variantName)
        {
            GameObject baseInstance = null;
            try
            {
                baseInstance = PrefabUtility.InstantiatePrefab(basePrefabAsset) as GameObject;
                if (baseInstance == null)
                {
                    Debug.LogError($"Base inst fail: {basePrefabAsset.name}");
                    return false;
                }

                baseInstance.name = variantName;
                Transform visualsTarget = baseInstance.transform.Find(BASE_PREFAB_VISUALS_TARGET);
                if (visualsTarget == null)
                {
                    Debug.LogError($"No '{BASE_PREFAB_VISUALS_TARGET}' in base: {basePrefabAsset.name}");
                    return false;
                }

                for (int i = visualsTarget.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(visualsTarget.GetChild(i).gameObject);
                }

                GameObject modelInstance = Instantiate(fbxSource);
                modelInstance.name = MODEL_NODE_NAME;
                modelInstance.transform.SetParent(visualsTarget, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                AssignMaterialToRenderers(modelInstance, newMaterial);
                return SavePrefabVariant(baseInstance, variantPath, $"variant '{variantName}'");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Variant Prefab Exception {variantName}: {e.Message}");
                return false;
            }
            finally
            {
                if (baseInstance != null) DestroyImmediate(baseInstance);
            }
        }


        private GameObject CreateNewEmptyBasePrefab()
        {
            string firstAssetPath = AssetDatabase.GetAssetPath(_selectedFBXAssets[0]);
            string firstAssetFolder = Path.GetDirectoryName(firstAssetPath);
            string parentFolder = Directory.GetParent(firstAssetFolder)?.FullName;
            if (string.IsNullOrEmpty(parentFolder) || !parentFolder.StartsWith(Application.dataPath.Replace('/', '\\')))
            {
                return null;
            }

            string relativeParentFolder = "Assets" + parentFolder.Substring(Application.dataPath.Length);
            relativeParentFolder = relativeParentFolder.Replace('\\', '/');
            string defaultPrefabsFolder = Path.Combine(relativeParentFolder, PREFABS_FOLDER_NAME).Replace('\\', '/');
            EnsureFolderExists(relativeParentFolder, PREFABS_FOLDER_NAME);

            string newBasePrefabPath = EditorUtility.SaveFilePanelInProject("Save New Common Base Prefab",
                DEFAULT_COMMON_BASE_NAME + ".prefab", "prefab", "Enter file name for the common base prefab.",
                defaultPrefabsFolder);
            if (string.IsNullOrEmpty(newBasePrefabPath))
            {
                Debug.Log("Common base creation cancelled.");
                return null;
            }

            string newBaseName = Path.GetFileNameWithoutExtension(newBasePrefabPath);
            GameObject newBaseRoot = new GameObject(newBaseName);
            GameObject visualsNode = new GameObject(VISUALS_NODE_NAME);
            visualsNode.transform.SetParent(newBaseRoot.transform, false);

            GameObject savedPrefab = null;
            bool success = false;
            try
            {
                PrefabUtility.SaveAsPrefabAsset(newBaseRoot, newBasePrefabPath, out success);
                if (success)
                {
                    savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newBasePrefabPath);
                    Debug.Log($"Created common base prefab: {newBasePrefabPath}");
                }
                else
                {
                    Debug.LogError($"Failed to save base prefab: {newBasePrefabPath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving base prefab: {e.Message}");
                success = false;
            }
            finally
            {
                DestroyImmediate(newBaseRoot);
            }

            return savedPrefab;
        }

        #endregion

        #region Material/Texture Helpers (Static & Instance)

        private static void EnsureFolderExists(string parentPath, string folderName)
        {
            string fullPath = Path.Combine(parentPath, folderName);
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        private static Material CreateHDRPMaterial(string materialPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
            {
                AssetDatabase.DeleteAsset(materialPath);
            }

            Shader hdrpLitShader = Shader.Find(SHADER_HDRP_LIT);
            if (hdrpLitShader == null)
            {
                Debug.LogError($"Shader '{SHADER_HDRP_LIT}' not found.");
                return null;
            }

            Material material = new Material(hdrpLitShader);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }


        private void FindAndAssignTexturesHDRP(Material material, string texturesFolderPath, string modelNameBase)
        {
            bool baseColorFound = false;
            bool normalFound = false;
            bool maskFound = false;
            bool emissionFound = false;

            if (_useManualTextureOverrides && _manualBaseMap != null)
            {
                baseColorFound = AssignManualTexture(material, _manualBaseMap, PROP_BASECOLORMAP,
                    TextureImporterType.Default, true);
            }
            else
            {
                baseColorFound = TryAssignAutoDetectedTexture(material, texturesFolderPath, modelNameBase,
                    BASEMAP_SUFFIX, PROP_BASECOLORMAP, TextureImporterType.Default, true);
            }

            if (_useManualTextureOverrides && _manualNormalMap != null)
            {
                normalFound = AssignManualTexture(material, _manualNormalMap, PROP_NORMALMAP,
                    TextureImporterType.NormalMap, false);
            }
            else
            {
                normalFound = TryAssignAutoDetectedTexture(material, texturesFolderPath, modelNameBase,
                    NORMALMAP_SUFFIX, PROP_NORMALMAP, TextureImporterType.NormalMap, false);
            }

            if (_useManualTextureOverrides && _manualMaskMap != null)
            {
                maskFound = AssignManualTexture(material, _manualMaskMap, PROP_MASKMAP, TextureImporterType.Default,
                    false);
            }
            else
            {
                maskFound = TryAssignAutoDetectedTexture(material, texturesFolderPath, modelNameBase, MASKMAP_SUFFIX,
                    PROP_MASKMAP, TextureImporterType.Default, false);
            }

            if (_useManualTextureOverrides && _manualEmissiveMap != null)
            {
                emissionFound = AssignManualTexture(material, _manualEmissiveMap, PROP_EMISSIVEMAP,
                    TextureImporterType.Default, true);
            }
            else
            {
                emissionFound = TryAssignAutoDetectedTexture(material, texturesFolderPath, modelNameBase,
                    EMISSIVE_SUFFIX, PROP_EMISSIVEMAP, TextureImporterType.Default, true);
            }


            if (normalFound) material.EnableKeyword(KEYWORD_NORMALMAP);
            else material.DisableKeyword(KEYWORD_NORMALMAP);

            if (maskFound)
                material.EnableKeyword(
                    KEYWORD_MASKMAP);
            else material.DisableKeyword(KEYWORD_MASKMAP);

            if (emissionFound) material.EnableKeyword(KEYWORD_EMISSION);
            else material.DisableKeyword(KEYWORD_EMISSION);

            if (emissionFound)
            {
                material.SetColor(PROP_EMISSIVECOLOR, Color.white * 2);
                material.SetFloat("_UseEmissiveIntensity", 1);
                material.SetFloat("_EmissiveIntensity", 2);
            }
            else
            {
                material.SetColor(PROP_EMISSIVECOLOR, Color.black);
                material.SetFloat("_UseEmissiveIntensity", 0);
                material.SetFloat("_EmissiveIntensity", 0);
            }

            EditorUtility.SetDirty(material);
        }

        private bool AssignManualTexture(Material material, Texture2D texture, string propertyName,
            TextureImporterType expectedType, bool expectedSRGB)
        {
            if (texture == null) return false;
            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath))
            {
                Debug.LogWarning($"Manual texture '{texture.name}' is not a project asset.");
                return false;
            }

            CheckAndFixTextureImporterSettings(texturePath, propertyName, expectedType, expectedSRGB);

            material.SetTexture(propertyName, texture);
            return true;
        }


        private bool TryAssignAutoDetectedTexture(Material material, string searchFolder, string modelNameBase,
            string textureSuffix, string propertyName, TextureImporterType expectedType, bool expectedSRGB)
        {
            string modelNamePrefix = modelNameBase;
            int firstUnderscoreIndex = modelNameBase.IndexOf('_');
            if (firstUnderscoreIndex > 0)
            {
                modelNamePrefix = modelNameBase.Substring(0, firstUnderscoreIndex);
            }

            string[] searchInFolders = { searchFolder };
            string[] guids = AssetDatabase.FindAssets($"{modelNamePrefix}* t:Texture", searchInFolders);
            if (guids.Length == 0) return false;

            string bestMatchPath = null;
            List<string> potentialMatches = new List<string>();
            foreach (string guid in guids)
            {
                string currentPath = AssetDatabase.GUIDToAssetPath(guid);
                string currentFileName = Path.GetFileNameWithoutExtension(currentPath);
                bool startsCorrectly = currentFileName.StartsWith(modelNamePrefix + "_");
                bool endsCorrectly = currentFileName.EndsWith(textureSuffix, System.StringComparison.OrdinalIgnoreCase);
                if (startsCorrectly && endsCorrectly)
                {
                    potentialMatches.Add(currentPath);
                    if (bestMatchPath == null) bestMatchPath = currentPath;
                }
            }

            if (bestMatchPath == null) return false;

            if (potentialMatches.Count > 1)
            {
                Debug.LogWarning(
                    $"Multiple textures matched pattern '{modelNamePrefix}_...{textureSuffix}' in '{searchFolder}'. Using first: '{Path.GetFileName(bestMatchPath)}'");
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(bestMatchPath);
            if (texture == null)
            {
                Debug.LogError($"Failed to load texture asset: {bestMatchPath}");
                return false;
            }

            CheckAndFixTextureImporterSettings(bestMatchPath, propertyName, expectedType, expectedSRGB);

            material.SetTexture(propertyName, texture);
            return true;
        }


        private void CheckAndFixTextureImporterSettings(string texturePath, string propertyName,
            TextureImporterType expectedType, bool expectedSRGB)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) return;

            bool needsReimport = false;
            string logReason = "";

            if (expectedType != TextureImporterType.Default && importer.textureType != expectedType)
            {
                logReason += $"Type ('{importer.textureType}'->'{expectedType}') ";
                importer.textureType = expectedType;
                needsReimport = true;
            }

            if (importer.sRGBTexture != expectedSRGB)
            {
                logReason += $"sRGB ('{importer.sRGBTexture}'->'{expectedSRGB}') ";
                importer.sRGBTexture = expectedSRGB;
                needsReimport = true;
            }

            if (needsReimport)
            {
                Debug.LogWarning(
                    $"Texture '{Path.GetFileName(texturePath)}' ({propertyName}): Fixing importer settings. {logReason}");
                importer.SaveAndReimport();
            }
        }

        private Material CreateAndPrepareSharedMaterial(string defaultFileNameBase)
        {
            if (!_useManualTextureOverrides || !_useSharedMaterial || _selectedFBXAssets.Count == 0)
            {
                Debug.LogError("Internal Error: CreateAndPrepareSharedMaterial called under wrong conditions.");
                return null;
            }

            Debug.Log($"Attempting to create a shared material ({defaultFileNameBase})...");
            string firstAssetPath = AssetDatabase.GetAssetPath(_selectedFBXAssets[0]);
            string firstAssetFolder = Path.GetDirectoryName(firstAssetPath);
            string parentFolder = Directory.GetParent(firstAssetFolder)?.FullName;
            string sharedMaterialPath = null;

            if (!string.IsNullOrEmpty(parentFolder) && parentFolder.StartsWith(Application.dataPath.Replace('/', '\\')))
            {
                string relativeParentFolder = "Assets" + parentFolder.Substring(Application.dataPath.Length);
                relativeParentFolder = relativeParentFolder.Replace('\\', '/');
                string materialsFolderPath =
                    Path.Combine(relativeParentFolder, MATERIALS_FOLDER_NAME).Replace('\\', '/');
                EnsureFolderExists(relativeParentFolder, MATERIALS_FOLDER_NAME);


                sharedMaterialPath = EditorUtility.SaveFilePanelInProject(
                    "Save Shared Batch Material",
                    defaultFileNameBase + ".mat",
                    "mat",
                    "Please enter a file name for the shared material.",
                    materialsFolderPath);

                if (string.IsNullOrEmpty(sharedMaterialPath))
                {
                    Debug.LogWarning("Shared material creation cancelled by user.");
                    return null;
                }
            }
            else
            {
                Debug.LogError(
                    "Could not determine a valid parent folder for the shared material based on the first selected asset. Cannot create shared material.");
                return null;
            }

            Material sharedMaterial = CreateHDRPMaterial(sharedMaterialPath);
            if (sharedMaterial == null)
            {
                Debug.LogError($"Failed to create the shared material asset at {sharedMaterialPath}.");
                return null;
            }

            sharedMaterial.enableInstancing = true;
            this.FindAndAssignTexturesHDRP(sharedMaterial, "", "");
            EditorUtility.SetDirty(sharedMaterial);
            Debug.Log($"Created and prepared shared material: {sharedMaterialPath}");

            return sharedMaterial;
        }

        #endregion

        #region Other Helpers  related to Instance

        private void AssignMaterialToRenderers(GameObject modelRoot, Material material)
        {
            if (material == null)
            {
                Debug.LogError("AssignMaterial: Material is null");
                return;
            }

            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"AssignMaterial: No renderers found in '{modelRoot.name}'");
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                int materialCount = renderer.sharedMaterials.Length;
                if (materialCount == 0) continue;
                Material[] newMaterials = new Material[materialCount];
                for (int i = 0; i < materialCount; i++)
                {
                    newMaterials[i] = material;
                }

                renderer.sharedMaterials = newMaterials;
                EditorUtility.SetDirty(renderer);
            }
        }

        private bool SavePrefab(GameObject rootObject, string path, string logDescription)
        {
            try
            {
                if (File.Exists(path))
                {
                    Debug.LogWarning($"SavePrefab: Overwriting '{path}'.");
                }

                PrefabUtility.SaveAsPrefabAssetAndConnect(rootObject, path, InteractionMode.AutomatedAction);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SavePrefab Error '{path}': {e.Message}");
                return false;
            }
        }

        private bool SavePrefabVariant(GameObject instanceRoot, string path, string logDescription)
        {
            bool success = false;
            try
            {
                if (File.Exists(path))
                {
                    Debug.LogWarning($"SavePrefabVariant: Overwriting '{path}'.");
                }

                PrefabUtility.SaveAsPrefabAsset(instanceRoot, path, out success);
                if (!success) Debug.LogError($"SavePrefabVariant Failed: '{path}'. SaveAsPrefabAsset returned false.");
                return success;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SavePrefabVariant Error '{path}': {e.Message}");
                return false;
            }
        }

        #endregion
    }
}