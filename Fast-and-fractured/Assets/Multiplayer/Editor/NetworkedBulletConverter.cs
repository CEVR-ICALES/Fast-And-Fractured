using UnityEngine;
using UnityEditor;
using FishNet.Object;
using FishNet.Component.Transforming.Beta;
using FastAndFractured.Multiplayer;
using FastAndFractured.Utilities;
using System.Reflection;

namespace FastAndFractured.Multiplayer.Editor
{
    /// <summary>
    /// Editor tool to convert singleplayer bullet/push/mine prefabs into networked variants.
    /// Adds: NetworkObject, NetworkedProjectile, NetworkTickSmoother, MultiplayerRigidbodyAdapter
    /// and removes LocalEventRunner.
    /// 
    /// Usage: Right-click a bullet prefab in Project ->"Fast And Fractured/Create Networked Bullet Variant"
    /// Or use the menu: Tools ->Fast And Fractured ->Networked Bullet Converter
    /// </summary>
    public class NetworkedBulletConverter : EditorWindow
    {
        private GameObject _sourcePrefab;
        private string _outputFolder = "Assets/Multiplayer/Pool";
        private string _variantName = "";
        
        // Optional references the user can assign
        private GameObject _visualsOverride;
        private GameObject _impactParticlesOverride;
        private GameObject _explosionPrefabOverride;

        [MenuItem("Tools/Fast And Fractured/Setup/Networked Bullet Converter", priority = 22)]
        public static void ShowWindow()
        {
            GetWindow<NetworkedBulletConverter>("Networked Bullet Converter");
        }

        [MenuItem("Assets/Fast And Fractured/Create Networked Bullet Variant", true)]
        private static bool ValidateContextMenu()
        {
            return Selection.activeObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go);
        }

        [MenuItem("Assets/Fast And Fractured/Create Networked Bullet Variant")]
        private static void CreateFromContextMenu()
        {
            var window = GetWindow<NetworkedBulletConverter>("Networked Bullet Converter");
            window._sourcePrefab = Selection.activeObject as GameObject;
            window._variantName = $"Networked{window._sourcePrefab.name} Variant";
        }

        private void OnGUI()
        {
            GUILayout.Label("Networked Bullet Converter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Converts a singleplayer bullet prefab into a networked variant.\n" +
                "Adds: NetworkObject, NetworkedProjectile, NetworkTickSmoother, MultiplayerRigidbodyAdapter\n" +
                "Removes: LocalEventRunner",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", _sourcePrefab, typeof(GameObject), false);

            if (_sourcePrefab != null && string.IsNullOrEmpty(_variantName))
                _variantName = $"Networked{_sourcePrefab.name} Variant";

            _variantName = EditorGUILayout.TextField("Variant Name", _variantName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

            EditorGUILayout.Space(5);
            GUILayout.Label("Optional Overrides", EditorStyles.miniBoldLabel);
            _visualsOverride = (GameObject)EditorGUILayout.ObjectField("Visuals (child GO)", _visualsOverride, typeof(GameObject), false);
            _impactParticlesOverride = (GameObject)EditorGUILayout.ObjectField("Impact Particles Prefab", _impactParticlesOverride, typeof(GameObject), false);
            _explosionPrefabOverride = (GameObject)EditorGUILayout.ObjectField("Explosion Prefab", _explosionPrefabOverride, typeof(GameObject), false);

            EditorGUILayout.Space(10);

            if (_sourcePrefab != null)
            {
                // Show preview of what will happen
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                
                var hasLocalEventRunner = _sourcePrefab.GetComponentInChildren<LocalEventRunner>() != null;
                var hasRigidbody = _sourcePrefab.GetComponent<Rigidbody>() != null;
                var hasPushBullet = _sourcePrefab.GetComponentInChildren<PushBulletBehaviour>() != null;
                var hasNormalBullet = _sourcePrefab.GetComponentInChildren<NormalBulletBehaviour>() != null;
                
                EditorGUILayout.LabelField($"  Type: {(hasPushBullet ? "Push/Mine Bullet" : hasNormalBullet ? "Normal Bullet" : "Unknown Bullet")}");
                EditorGUILayout.LabelField($"  LocalEventRunner: {(hasLocalEventRunner ? "[OK]Will be removed" : "[OK]Not found")}");
                EditorGUILayout.LabelField($"  Rigidbody: {(hasRigidbody ? "[OK]Will keep (adapter wraps it)" : "[OK]Not found")}");
                
                // Auto-detect visuals and particles
                var autoVisuals = FindVisualsChild(_sourcePrefab);
                var autoParticles = FindImpactParticles(_sourcePrefab);
                var autoExplosion = FindExplosionPrefab(_sourcePrefab);
                
                EditorGUILayout.LabelField($"  Auto-detected visuals: {(autoVisuals != null ? autoVisuals.name : "None (will use root)")}");
                EditorGUILayout.LabelField($"  Auto-detected impact particles: {(autoParticles != null ? autoParticles.name : "None")}");
                EditorGUILayout.LabelField($"  Auto-detected explosion: {(autoExplosion != null ? autoExplosion.name : "None")}");

                string outputPath = $"{_outputFolder}/{_variantName}.prefab";
                EditorGUILayout.LabelField($"  Output: {outputPath}");
                
                bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath) != null;
                if (exists)
                {
                    EditorGUILayout.HelpBox("A prefab already exists at this path. It will be overwritten.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            GUI.enabled = _sourcePrefab != null && !string.IsNullOrEmpty(_variantName);
            if (GUILayout.Button("Create Networked Variant", GUILayout.Height(30)))
            {
                CreateNetworkedVariant();
            }
            GUI.enabled = true;
        }

        private void CreateNetworkedVariant()
        {
            if (!AssetDatabase.IsValidFolder(_outputFolder))
            {
                string[] parts = _outputFolder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            string path = $"{_outputFolder}/{_variantName}.prefab";

            // Create prefab variant
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_sourcePrefab);
            
            try
            {
                // 1. Remove LocalEventRunner (NetworkedProjectile handles physics)
                var localEventRunner = instance.GetComponentInChildren<LocalEventRunner>();
                if (localEventRunner != null)
                {
                    DestroyImmediate(localEventRunner);
                    Debug.Log("[NetworkedBulletConverter] Removed LocalEventRunner");
                }

                // 2. Add MultiplayerRigidbodyAdapter (wraps Rigidbody for FishNet prediction)
                if (instance.GetComponent<MultiplayerRigidbodyAdapter>() == null)
                {
                    instance.AddComponent<MultiplayerRigidbodyAdapter>();
                    Debug.Log("[NetworkedBulletConverter] Added MultiplayerRigidbodyAdapter");
                }

                // 3. Add NetworkObject (FishNet core)
                var networkObject = instance.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    networkObject = instance.AddComponent<NetworkObject>();
                    Debug.Log("[NetworkedBulletConverter] Added NetworkObject");
                }
                // Configure prediction
                var noProp = typeof(NetworkObject);
                var enablePredField = noProp.GetField("_enablePrediction", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enablePredField != null)
                    enablePredField.SetValue(networkObject, true);

                // 4. Add NetworkedProjectile FIRST (before NetworkTickSmoother)
                // Order matters: FishNet assigns RPC link indices based on component order.
                // NetworkedProjectile must be at a stable index.
                var networkedProjectile = instance.GetComponent<NetworkedProjectile>();
                if (networkedProjectile == null)
                {
                    networkedProjectile = instance.AddComponent<NetworkedProjectile>();
                    Debug.Log("[NetworkedBulletConverter] Added NetworkedProjectile");
                }

                if (instance.GetComponent<NetworkedPredictedProjectileSpawn>() == null)
                    instance.AddComponent<NetworkedPredictedProjectileSpawn>();

                // 5. Add NetworkTickSmoother
                var tickSmoother = instance.GetComponent<NetworkTickSmoother>();
                if (tickSmoother == null)
                {
                    tickSmoother = instance.AddComponent<NetworkTickSmoother>();
                    Debug.Log("[NetworkedBulletConverter] Added NetworkTickSmoother");
                }
                EnsureTickSmootherTargetTransform(tickSmoother, instance.transform);

                // 6. Wire up references
                // Visuals
                GameObject visuals = _visualsOverride != null ? _visualsOverride : FindVisualsChild(instance);
                if (visuals != null)
                {
                    SetPrivateField(networkedProjectile, "_visuals", visuals);
                    Debug.Log($"[NetworkedBulletConverter] Set _visuals to '{visuals.name}'");
                }

                // Impact particles
                GameObject impactParticles = _impactParticlesOverride != null ? _impactParticlesOverride : FindImpactParticles(instance);
                if (impactParticles != null)
                {
                    SetPrivateField(networkedProjectile, "_impactParticlesPrefab", impactParticles);
                    Debug.Log($"[NetworkedBulletConverter] Set _impactParticlesPrefab to '{impactParticles.name}'");
                }

                // Explosion prefab
                GameObject explosionPrefab = _explosionPrefabOverride != null ? _explosionPrefabOverride : FindExplosionPrefab(instance);
                if (explosionPrefab != null)
                {
                    SetPrivateField(networkedProjectile, "_explosionPrefab", explosionPrefab);
                    Debug.Log($"[NetworkedBulletConverter] Set _explosionPrefab to '{explosionPrefab.name}'");
                }

                // Save as prefab variant
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, path);

                if (savedPrefab != null)
                {
                    Debug.Log($"<color=green>[NetworkedBulletConverter] [OK]Created networked variant at {path}</color>");
                    EditorGUIUtility.PingObject(savedPrefab);
                    Selection.activeObject = savedPrefab;
                }
                else
                {
                    Debug.LogError("[NetworkedBulletConverter] Failed to save prefab variant!");
                }
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Find the child GameObject most likely to be the visual mesh.
        /// Looks for "Model", "Visual", "Mesh" children, or first child with MeshRenderer.
        /// </summary>
        private static GameObject FindVisualsChild(GameObject root)
        {
            // Try known naming patterns
            string[] visualNames = { "Models", "Model", "Visuals", "Visual", "Mesh" };
            foreach (var name in visualNames)
            {
                var child = root.transform.Find(name);
                if (child != null) return child.gameObject;
            }

            // Try first child with MeshRenderer (skip explosion/particle children)
            foreach (Transform child in root.transform)
            {
                if (child.GetComponent<MeshRenderer>() != null && 
                    child.GetComponent<ParticleSystem>() == null &&
                    !child.name.Contains("Explosion") && !child.name.Contains("VFX"))
                {
                    return child.gameObject;
                }
            }

            // If root itself has MeshRenderer, use root
            if (root.GetComponent<MeshRenderer>() != null)
                return root;

            return null;
        }

        /// <summary>
        /// Find impact particle prefab  Elooks for particle children named "Impact", "Hit", "Particles".
        /// For push bullets, this is typically the explosion VFX child.
        /// </summary>
        private static GameObject FindImpactParticles(GameObject root)
        {
            string[] particleNames = { "BulletTomatoExplosion", "ImpactParticles", "Impact", "HitVFX", "HitParticles" };
            foreach (var name in particleNames)
            {
                var child = root.transform.Find(name);
                if (child != null) return child.gameObject;
            }

            // Try children with ParticleSystem that aren't explosion hitboxes
            foreach (Transform child in root.transform)
            {
                if (child.GetComponent<ParticleSystem>() != null && !child.name.Contains("Explosion"))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Find explosion prefab  Efor push bullets, look for ExplosionVFX or child with ExplosionForce.
        /// </summary>
        private static GameObject FindExplosionPrefab(GameObject root)
        {
            string[] explosionNames = { "ExplosionVFX", "Explosion", "ExplosionHitbox" };
            foreach (var name in explosionNames)
            {
                var child = root.transform.Find(name);
                if (child != null && child.GetComponent<ParticleSystem>() != null)
                    return child.gameObject;
            }

            // Look for children with ParticleSystem named "Explosion"
            foreach (Transform child in root.transform)
            {
                if (child.name.Contains("Explosion") && child.GetComponent<ParticleSystem>() != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                // Try SerializedObject for Unity serialized fields
                var so = new SerializedObject((Object)target);
                var prop = so.FindProperty(fieldName);
                if (prop != null && value is GameObject go)
                {
                    prop.objectReferenceValue = go;
                    so.ApplyModifiedProperties();
                }
            }
        }

        private static void EnsureTickSmootherTargetTransform(NetworkTickSmoother tickSmoother, Transform targetTransform)
        {
            if (tickSmoother == null || targetTransform == null) return;

            var field = typeof(NetworkTickSmoother).GetField("_initializationSettings",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return;

            object boxed = field.GetValue(tickSmoother);
            if (!(boxed is InitializationSettings))
                return;

            InitializationSettings settings = (InitializationSettings)boxed;
            settings.TargetTransform = targetTransform;
            field.SetValue(tickSmoother, settings);
        }
    }
}
