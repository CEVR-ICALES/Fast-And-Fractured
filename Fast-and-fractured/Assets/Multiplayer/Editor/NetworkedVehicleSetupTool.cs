using UnityEngine;
using UnityEditor;
using FishNet.Object;
using FastAndFractured.Multiplayer;
using FastAndFractured;

namespace FastAndFractured.Multiplayer.Editor
{
    public class NetworkedVehicleSetupTool : EditorWindow
    {
        private enum Mode { CreateVariant, UpdateExisting, BatchFromCharacterData }
        private Mode _mode = Mode.CreateVariant;

        private GameObject _sourceCarPrefab;
        private string _outputFolder = "Assets/Multiplayer";
        private string _variantName = "";

        private GameObject _existingPrefab;
        private DefaultAsset _characterDataFolder;
        // Off: singleplayer keeps its cars; online the networked variants are swapped in at runtime.
        private bool _updateCharacterDataPrefabReference = false;

        private const string DEFAULT_CHARACTER_DATA_FOLDER = "Assets/FastAndFractured/Characters";
        private const float SECTION_SPACING = 10f;
        private const float BUTTON_HEIGHT = 28f;
        private const float PRIMARY_BUTTON_HEIGHT = 35f;

        [Header("Networked Bullet Prefabs")]
        private GameObject _normalBulletPrefab;
        private GameObject _pushBulletPrefab;
        private GameObject _minePrefab;

        [MenuItem("Tools/Fast And Fractured/Setup/Networked Vehicle Setup", priority = 20)]
        public static void ShowWindow()
        {
            GetWindow<NetworkedVehicleSetupTool>("Networked Vehicle Setup");
        }

        [MenuItem("Assets/Fast And Fractured/Setup Networked Vehicle", true)]
        private static bool ValidateContextMenu()
        {
            if (Selection.activeObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
                return go.GetComponent<CarMovementController>() != null;
            return false;
        }

        [MenuItem("Assets/Fast And Fractured/Setup Networked Vehicle")]
        private static void CreateFromContextMenu()
        {
            var window = GetWindow<NetworkedVehicleSetupTool>("Networked Vehicle Setup");
            window._sourceCarPrefab = Selection.activeObject as GameObject;
            window._variantName = $"Networked{window._sourceCarPrefab.name} Variant";
        }

        private void OnGUI()
        {
            GUILayout.Label("Networked Vehicle Setup", EditorStyles.boldLabel);

            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Create New Variant", "Update Existing Prefab", "Batch From CharacterData" });
            EditorGUILayout.Space(5);

            if (_mode == Mode.CreateVariant)
            {
                DrawCreateVariantGUI();
            }
            else if (_mode == Mode.UpdateExisting)
            {
                DrawUpdateExistingGUI();
            }
            else
            {
                DrawBatchGUI();
            }

            EditorGUILayout.Space(SECTION_SPACING);
            EditorGUILayout.HelpBox("Car fixes (rigidbody adapter, smoother targets, turret smoother, motion smoothing) run on every networked car after creating or updating one. Run them again here after editing a car by hand.", MessageType.None);
            if (GUILayout.Button("Apply Car Fixes To All Networked Cars", GUILayout.Height(BUTTON_HEIGHT)))
            {
                NetworkedCarFixes.ApplyToAllCars();
            }
        }

        private void DrawCreateVariantGUI()
        {
            EditorGUILayout.HelpBox(
                "Creates a NEW prefab variant from a singleplayer car prefab.\n" +
                "The original prefab is NOT modified.",
                MessageType.Info);

            EditorGUILayout.Space(SECTION_SPACING);

            _sourceCarPrefab = (GameObject)EditorGUILayout.ObjectField("Source Car Prefab", _sourceCarPrefab, typeof(GameObject), false);

            if (_sourceCarPrefab != null && string.IsNullOrEmpty(_variantName))
                _variantName = $"Networked{_sourceCarPrefab.name} Variant";

            _variantName = EditorGUILayout.TextField("Variant Name", _variantName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

            DrawBulletFields();

            if (_sourceCarPrefab != null)
            {
                EditorGUILayout.Space(SECTION_SPACING);
                DrawPreview(_sourceCarPrefab);
                string outputPath = $"{_outputFolder}/{_variantName}.prefab";
                EditorGUILayout.LabelField($"  Output: {outputPath}");
            }

            EditorGUILayout.Space(SECTION_SPACING);

            GUI.enabled = _sourceCarPrefab != null && !string.IsNullOrEmpty(_variantName);
            if (GUILayout.Button("Create Networked Vehicle Variant", GUILayout.Height(PRIMARY_BUTTON_HEIGHT)))
            {
                CreateNetworkedVariant();
            }
            GUI.enabled = true;
        }

        private void DrawUpdateExistingGUI()
        {
            EditorGUILayout.HelpBox(
                "Updates an EXISTING networked prefab, adding any missing multiplayer components.\n" +
                "Use this for prefabs like NetworkedJosefinoGameplayCar Variant that were set up manually.",
                MessageType.Info);

            EditorGUILayout.Space(SECTION_SPACING);

            _existingPrefab = (GameObject)EditorGUILayout.ObjectField("Existing Networked Prefab", _existingPrefab, typeof(GameObject), false);

            DrawBulletFields();

            if (_existingPrefab != null)
            {
                EditorGUILayout.Space(SECTION_SPACING);
                DrawPreview(_existingPrefab);
            }

            EditorGUILayout.Space(SECTION_SPACING);

            GUI.enabled = _existingPrefab != null;
            if (GUILayout.Button("Update Existing Prefab", GUILayout.Height(PRIMARY_BUTTON_HEIGHT)))
            {
                UpdateExistingPrefab();
            }
            GUI.enabled = true;
        }

        private void DrawBatchGUI()
        {
            EditorGUILayout.HelpBox(
                "Generates/updates networked vehicle variants for all CharacterData assets in a folder.\n" +
                "Useful when adding many new characters/cars.",
                MessageType.Info);

            EditorGUILayout.Space(SECTION_SPACING);

            if (_characterDataFolder == null && AssetDatabase.IsValidFolder(DEFAULT_CHARACTER_DATA_FOLDER))
                _characterDataFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DEFAULT_CHARACTER_DATA_FOLDER);

            _characterDataFolder = (DefaultAsset)EditorGUILayout.ObjectField("CharacterData Folder", _characterDataFolder, typeof(DefaultAsset), false);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _updateCharacterDataPrefabReference = EditorGUILayout.Toggle("Update CarDefaultPrefab", _updateCharacterDataPrefabReference);

            DrawBulletFields();

            int dataCount = 0;
            string folderPath = string.Empty;
            if (_characterDataFolder != null)
            {
                folderPath = AssetDatabase.GetAssetPath(_characterDataFolder);
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    dataCount = AssetDatabase.FindAssets("t:CharacterData", new[] { folderPath }).Length;
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"CharacterData assets found: {dataCount}");

            GUI.enabled = _characterDataFolder != null && dataCount > 0;
            if (GUILayout.Button("Generate / Update Networked Variants", GUILayout.Height(PRIMARY_BUTTON_HEIGHT)))
            {
                GenerateBatchFromCharacterData();
            }
            GUI.enabled = true;
        }

        private void DrawBulletFields()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("Networked Bullet Prefabs (for NetworkedShoot)", EditorStyles.miniBoldLabel);
            _normalBulletPrefab = (GameObject)EditorGUILayout.ObjectField("Normal Bullet Prefab", _normalBulletPrefab, typeof(GameObject), false);
            _pushBulletPrefab = (GameObject)EditorGUILayout.ObjectField("Push Bullet Prefab", _pushBulletPrefab, typeof(GameObject), false);
            _minePrefab = (GameObject)EditorGUILayout.ObjectField("Mine Prefab", _minePrefab, typeof(GameObject), false);
        }

        private void DrawPreview(GameObject prefab)
        {
            EditorGUILayout.LabelField("Component Analysis", EditorStyles.boldLabel);

            bool hasCarMovement = prefab.GetComponent<CarMovementController>() != null;
            bool hasRigidbody = prefab.GetComponent<Rigidbody>() != null;
            bool hasStatsController = prefab.GetComponent<StatsController>() != null;
            bool hasNormalShoot = prefab.GetComponentInChildren<NormalShootHandle>() != null;
            bool hasPushShoot = prefab.GetComponentInChildren<PushShootHandle>() != null;
            bool hasUniqueAbility = prefab.GetComponentInChildren<BaseUniqueAbility>() != null;
            bool hasLocalShoot = prefab.GetComponentInChildren<LocalShootController>() != null;

            bool hasNob = prefab.GetComponent<NetworkObject>() != null;
            bool hasNV = prefab.GetComponent<NetworkedVehicle>() != null;
            bool hasMRA = prefab.GetComponent<MultiplayerRigidbodyAdapter>() != null;
            bool hasNS = prefab.GetComponent<NetworkedShoot>() != null;
            bool hasNSC = prefab.GetComponent<NetworkedStatsController>() != null;
            bool hasNUA = prefab.GetComponent<NetworkedUniqueAbility>() != null;

            EditorGUILayout.LabelField($"  CarMovementController: {(hasCarMovement ? "Present" : "REQUIRED")}");
            EditorGUILayout.LabelField($"  Rigidbody: {(hasRigidbody ? "Present" : "REQUIRED")}");
            EditorGUILayout.LabelField($"  StatsController: {(hasStatsController ? "Present" : "Skipped")}");
            EditorGUILayout.LabelField($"  NormalShootHandle: {(hasNormalShoot ? "Present" : "Skipped")}");
            EditorGUILayout.LabelField($"  PushShootHandle: {(hasPushShoot ? "Present" : "Skipped")}");
            EditorGUILayout.LabelField($"  UniqueAbility: {(hasUniqueAbility ? "Present" : "Skipped")}");
            EditorGUILayout.LabelField($"  LocalShootController: {(hasLocalShoot ? "Will be disabled at runtime" : "N/A")}");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Multiplayer Components", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"  NetworkObject: {(hasNob ? "Present" : "Will add")}");
            EditorGUILayout.LabelField($"  NetworkedVehicle: {(hasNV ? "Present" : "Will add")}");
            EditorGUILayout.LabelField($"  MultiplayerRigidbodyAdapter: {(hasMRA ? "Present" : "Will add")}");
            EditorGUILayout.LabelField($"  NetworkedShoot: {(hasNS ? "Present" : hasNormalShoot ? "Will add" : "N/A")}");
            EditorGUILayout.LabelField($"  NetworkedStatsController: {(hasNSC ? "Present" : hasStatsController ? "Will add" : "N/A")}");
            EditorGUILayout.LabelField($"  NetworkedUniqueAbility: {(hasNUA ? "Present" : hasUniqueAbility ? "Will add" : "N/A")}");

            if (!hasCarMovement)
            {
                EditorGUILayout.HelpBox("Source prefab MUST have CarMovementController!", MessageType.Error);
            }
        }

        private void CreateNetworkedVariant()
        {
            if (_sourceCarPrefab.GetComponent<CarMovementController>() == null)
            {
                EditorUtility.DisplayDialog("Error", "Source prefab must have CarMovementController.", "OK");
                return;
            }

            EnsureFolder(_outputFolder);
            string path = $"{_outputFolder}/{_variantName}.prefab";

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_sourceCarPrefab);

            try
            {
                int added = AddMissingComponents(instance);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
                if (savedPrefab != null)
                {
                    Debug.Log($"<color=green>[VehicleSetup] Created networked vehicle at {path} ({added} components added)</color>");
                    EditorGUIUtility.PingObject(savedPrefab);
                    Selection.activeObject = savedPrefab;
                }
            }
            finally
            {
                DestroyImmediate(instance);
            }
            NetworkedCarFixes.ApplyToAllCars();
        }

        private void UpdateExistingPrefab()
        {
            if (_existingPrefab == null) return;

            string assetPath = AssetDatabase.GetAssetPath(_existingPrefab);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Error", "Could not find asset path for prefab.", "OK");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                int added = AddMissingComponents(contents);

                if (added > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                    Debug.Log($"<color=green>[VehicleSetup] Updated {_existingPrefab.name}: {added} components added</color>");
                }
                else
                {
                    Debug.Log($"<color=yellow>[VehicleSetup] {_existingPrefab.name} already has all multiplayer components. Nothing to add.</color>");
                }

                EditorUtility.DisplayDialog("Update Complete", $"{added} component(s) added to {_existingPrefab.name}.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            NetworkedCarFixes.ApplyToAllCars();
        }

        private void GenerateBatchFromCharacterData()
        {
            if (_characterDataFolder == null)
            {
                EditorUtility.DisplayDialog("Error", "Select a CharacterData folder first.", "OK");
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(_characterDataFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                EditorUtility.DisplayDialog("Error", "Selected CharacterData folder is not valid.", "OK");
                return;
            }

            string[] dataGuids = AssetDatabase.FindAssets("t:CharacterData", new[] { folderPath });
            if (dataGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("No CharacterData Found", $"No CharacterData assets found in:\n{folderPath}", "OK");
                return;
            }

            EnsureFolder(_outputFolder);

            int created = 0;
            int updated = 0;
            int skipped = 0;
            int dataReferencesUpdated = 0;
            var errors = new System.Collections.Generic.List<string>();

            foreach (string guid in dataGuids)
            {
                string dataPath = AssetDatabase.GUIDToAssetPath(guid);
                var characterData = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
                if (characterData == null || characterData.CarDefaultPrefab == null)
                {
                    skipped++;
                    continue;
                }

                GameObject sourcePrefab = characterData.CarDefaultPrefab;
                string variantName = sourcePrefab.name.StartsWith("Networked")
                    ? sourcePrefab.name
                    : $"Networked{sourcePrefab.name} Variant";
                string outputPath = $"{_outputFolder}/{variantName}.prefab";

                try
                {
                    GameObject existingNetworked = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                    GameObject networkedPrefab;

                    if (existingNetworked == null)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
                        try
                        {
                            AddMissingComponents(instance);
                            networkedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                        }
                        finally
                        {
                            DestroyImmediate(instance);
                        }

                        if (networkedPrefab == null)
                        {
                            skipped++;
                            errors.Add($"{characterData.name}: failed to create prefab at {outputPath}");
                            continue;
                        }

                        created++;
                    }
                    else
                    {
                        GameObject contents = PrefabUtility.LoadPrefabContents(outputPath);
                        try
                        {
                            AddMissingComponents(contents);
                            PrefabUtility.SaveAsPrefabAsset(contents, outputPath);
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(contents);
                        }

                        networkedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                        updated++;
                    }

                    if (_updateCharacterDataPrefabReference &&
                        networkedPrefab != null &&
                        characterData.CarDefaultPrefab != networkedPrefab)
                    {
                        characterData.CarDefaultPrefab = networkedPrefab;
                        EditorUtility.SetDirty(characterData);
                        dataReferencesUpdated++;
                    }
                }
                catch (System.Exception ex)
                {
                    skipped++;
                    errors.Add($"{characterData.name}: {ex.Message}");
                }
            }

            if (dataReferencesUpdated > 0)
            {
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            NetworkedCarFixes.ApplyToAllCars();

            string summary =
                $"Created: {created}\n" +
                $"Updated: {updated}\n" +
                $"Skipped: {skipped}\n" +
                $"CharacterData refs updated: {dataReferencesUpdated}\n" +
                $"Errors: {errors.Count}";

            Debug.Log($"<color=cyan>[VehicleSetup Batch] {summary}</color>");
            if (errors.Count > 0)
            {
                for (int i = 0; i < errors.Count; i++)
                {
                    Debug.LogWarning($"[VehicleSetup Batch] {errors[i]}");
                }
            }

            EditorUtility.DisplayDialog("Batch Complete", summary, "OK");
        }

        private int AddMissingComponents(GameObject instance)
        {
            int added = 0;

            if (instance.GetComponent<MultiplayerRigidbodyAdapter>() == null)
            {
                instance.AddComponent<MultiplayerRigidbodyAdapter>();
                Debug.Log("[VehicleSetup] Added MultiplayerRigidbodyAdapter");
                added++;
            }

            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = instance.AddComponent<NetworkObject>();
                Debug.Log("[VehicleSetup] Added NetworkObject");
                added++;
            }
            var enablePredField = typeof(NetworkObject).GetField("_enablePrediction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (enablePredField != null)
                enablePredField.SetValue(networkObject, true);

            if (instance.GetComponent<NetworkedVehicle>() == null)
            {
                instance.AddComponent<NetworkedVehicle>();
                Debug.Log("[VehicleSetup] Added NetworkedVehicle");
                added++;
            }

            if (instance.GetComponent<StatsController>() != null && instance.GetComponent<NetworkedStatsController>() == null)
            {
                instance.AddComponent<NetworkedStatsController>();
                Debug.Log("[VehicleSetup] Added NetworkedStatsController");
                added++;
            }

            if (instance.GetComponentInChildren<NormalShootHandle>() != null)
            {
                var shoot = instance.GetComponent<NetworkedShoot>();
                if (shoot == null)
                {
                    shoot = instance.AddComponent<NetworkedShoot>();
                    Debug.Log("[VehicleSetup] Added NetworkedShoot");
                    added++;
                }

                WireBulletPrefabs(shoot);
            }

            if (instance.GetComponentInChildren<BaseUniqueAbility>() != null && instance.GetComponent<NetworkedUniqueAbility>() == null)
            {
                instance.AddComponent<NetworkedUniqueAbility>();
                Debug.Log("[VehicleSetup] Added NetworkedUniqueAbility");
                added++;
            }

            return added;
        }

        private void WireBulletPrefabs(NetworkedShoot shoot)
        {
            var so = new SerializedObject(shoot);
            if (_normalBulletPrefab != null)
            {
                var prop = so.FindProperty("_networkedNormalBulletPrefab");
                if (prop != null) prop.objectReferenceValue = _normalBulletPrefab;
            }
            if (_pushBulletPrefab != null)
            {
                var prop = so.FindProperty("_networkedPushBulletPrefab");
                if (prop != null) prop.objectReferenceValue = _pushBulletPrefab;
            }
            if (_minePrefab != null)
            {
                var prop = so.FindProperty("_networkedMinePrefab");
                if (prop != null) prop.objectReferenceValue = _minePrefab;
            }
            so.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
