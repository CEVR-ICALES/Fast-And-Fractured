using UnityEngine;
using UnityEditor;
using FishNet.Object;
using FastAndFractured.Multiplayer;
using FastAndFractured;

namespace FastAndFractured.Multiplayer.Editor
{
    public class NetworkedAISetupTool : EditorWindow
    {
        private const float SECTION_SPACING = 10f;
        private const float BUTTON_HEIGHT = 35f;

        private GameObject _aiPrefab;
        private GameObject _normalBulletPrefab;
        private GameObject _pushBulletPrefab;
        private GameObject _minePrefab;

        [MenuItem("Tools/Fast And Fractured/Setup/Networked AI Setup", priority = 21)]
        public static void ShowWindow()
        {
            GetWindow<NetworkedAISetupTool>("Networked AI Setup");
        }

        [MenuItem("Assets/Fast And Fractured/Setup Networked AI", true)]
        private static bool ValidateContextMenu()
        {
            if (Selection.activeObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
                return go.GetComponentInChildren<EnemyAIBrain>() != null;
            return false;
        }

        [MenuItem("Assets/Fast And Fractured/Setup Networked AI")]
        private static void CreateFromContextMenu()
        {
            var window = GetWindow<NetworkedAISetupTool>("Networked AI Setup");
            window._aiPrefab = Selection.activeObject as GameObject;
        }

        private void OnGUI()
        {
            GUILayout.Label("Networked AI Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adds multiplayer components to an AI vehicle prefab.\n" +
                "Adds: NetworkObject (if missing), NetworkedAIShoot.\n" +
                "Modifies the prefab IN PLACE (no variant created).",
                MessageType.Info);

            EditorGUILayout.Space(SECTION_SPACING);

            _aiPrefab = (GameObject)EditorGUILayout.ObjectField("AI Prefab", _aiPrefab, typeof(GameObject), false);

            if (_aiPrefab != null)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("Networked Bullet Prefabs", EditorStyles.miniBoldLabel);
                _normalBulletPrefab = (GameObject)EditorGUILayout.ObjectField("Normal Bullet", _normalBulletPrefab, typeof(GameObject), false);
                _pushBulletPrefab = (GameObject)EditorGUILayout.ObjectField("Push Bullet", _pushBulletPrefab, typeof(GameObject), false);
                _minePrefab = (GameObject)EditorGUILayout.ObjectField("Mine", _minePrefab, typeof(GameObject), false);

                EditorGUILayout.Space(SECTION_SPACING);
                DrawAnalysis();
            }

            EditorGUILayout.Space(SECTION_SPACING);

            GUI.enabled = _aiPrefab != null;
            if (GUILayout.Button("Setup AI for Multiplayer", GUILayout.Height(BUTTON_HEIGHT)))
            {
                SetupAIPrefab();
            }
            GUI.enabled = true;
        }

        private void DrawAnalysis()
        {
            EditorGUILayout.LabelField("Component Analysis", EditorStyles.boldLabel);

            bool hasNetworkObject = _aiPrefab.GetComponent<NetworkObject>() != null;
            bool hasAIShoot = _aiPrefab.GetComponent<NetworkedAIShoot>() != null;
            bool hasNormalShoot = _aiPrefab.GetComponentInChildren<NormalShootHandle>() != null;
            bool hasPushShoot = _aiPrefab.GetComponentInChildren<PushShootHandle>() != null;
            bool hasAIBrain = _aiPrefab.GetComponentInChildren<EnemyAIBrain>() != null;

            EditorGUILayout.LabelField($"  NetworkObject: {(hasNetworkObject ? "[OK] Already exists" : "Will add")}");
            EditorGUILayout.LabelField($"  NetworkedAIShoot: {(hasAIShoot ? "[OK] Already exists" : "Will add")}");
            EditorGUILayout.LabelField($"  EnemyAIBrain: {(hasAIBrain ? "Present" : "WARNING")}");
            EditorGUILayout.LabelField($"  NormalShootHandle: {(hasNormalShoot ? "Present" : "Will add")}");
            EditorGUILayout.LabelField($"  PushShootHandle: {(hasPushShoot ? "Present" : "Will add")}");

            if (hasAIShoot)
            {
                EditorGUILayout.HelpBox("NetworkedAIShoot already exists. Running again will update bullet prefab references.", MessageType.Warning);
            }
        }

        private void SetupAIPrefab()
        {
            string prefabPath = AssetDatabase.GetAssetPath(_aiPrefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                EditorUtility.DisplayDialog("Error", "Cannot find prefab asset path.", "OK");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                bool modified = false;

                if (prefabRoot.GetComponent<NetworkObject>() == null)
                {
                    prefabRoot.AddComponent<NetworkObject>();
                    Debug.Log("[AISetup] Added NetworkObject");
                    modified = true;
                }

                var aiShoot = prefabRoot.GetComponent<NetworkedAIShoot>();
                if (aiShoot == null)
                {
                    aiShoot = prefabRoot.AddComponent<NetworkedAIShoot>();
                    Debug.Log("[AISetup] Added NetworkedAIShoot");
                    modified = true;
                }

                var so = new SerializedObject(aiShoot);
                bool prefabsModified = false;

                if (_normalBulletPrefab != null)
                {
                    var prop = so.FindProperty("_networkedNormalBulletPrefab");
                    if (prop != null && prop.objectReferenceValue != _normalBulletPrefab)
                    {
                        prop.objectReferenceValue = _normalBulletPrefab;
                        prefabsModified = true;
                    }
                }
                if (_pushBulletPrefab != null)
                {
                    var prop = so.FindProperty("_networkedPushBulletPrefab");
                    if (prop != null && prop.objectReferenceValue != _pushBulletPrefab)
                    {
                        prop.objectReferenceValue = _pushBulletPrefab;
                        prefabsModified = true;
                    }
                }
                if (_minePrefab != null)
                {
                    var prop = so.FindProperty("_networkedMinePrefab");
                    if (prop != null && prop.objectReferenceValue != _minePrefab)
                    {
                        prop.objectReferenceValue = _minePrefab;
                        prefabsModified = true;
                    }
                }

                if (prefabsModified)
                {
                    so.ApplyModifiedProperties();
                    modified = true;
                    Debug.Log("[AISetup] Updated bullet prefab references");
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    Debug.Log($"<color=green>[AISetup] [OK] AI prefab updated at {prefabPath}</color>");
                }
                else
                {
                    Debug.Log("[AISetup] No changes needed  Eprefab already configured.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done", "AI prefab setup complete!", "OK");
        }
    }
}
