using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable InconsistentNaming
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace DestroyIt
{
    
    /// <summary>
    /// Manages destructible terrain trees.
    /// Attach this script to a gameobject (ie, _TreeManager) in your scene.
    /// On your Terrain, turn off Enable Tree Colliders. Tree colliders will be attached by this script at runtime.
    /// </summary>
    [RequireComponent(typeof(TerrainPreserver))]
    [DisallowMultipleComponent]
    public class TreeManager : MonoBehaviour
    {
        [Tooltip("The terrain managed by this script. Leave empty to manage the current active terrain.")]
        public Terrain terrain;

        [Tooltip("Backs up the active terrain in the editor when you play the scene. This way if a crash occurs, you'll be able to restore from the backup and won't lose your placed trees, since the TreeManager replaces terrain trees with destructible stand-ins at runtime.")]
        public bool backupTerrain = true;

        // NOTE: This folder and all its contents will be deleted each time Destructible Trees are updated!
        [Tooltip("The folder where the stripped-down destructible terrain tree prototype prefabs are stored.\n\nYou can change this if you want to store your tree stand-in resources somewhere else.")]
        public string pathToStandIns = "Assets/DestroyIt/Resources/TreeStandIns/"; 
        
        [Tooltip("These are stripped-down tree prototype objects, containing only colliders and other essential components to make them destructible.\n\nYou don't need to change these - they are automatically generated when the Update Destructible Trees button is clicked.")]
        public List<DestructibleTree> destructibleTrees;

        [HideInInspector]
        public List<TreeReset> treesToReset;

        // Hide the default constructor (use TreeManager.Instance to access this class)
        private TreeManager() { }

        private static TreeManager _instance;
        private List<TreeInstance> currentTreeInstances; // NOTE: It's important to keep this a List, don't convert to Array
        private TreeInstance[] originalTreeInstances;
        private bool isTerrainDataDirty; // Determines if the terrainData has a backup that hasn't been resolved (possibly from a Unity crash). Don't try to make changes to a dirty TerrainData - give the user an option to fix it first.
        
        // Public Instance reference other classes will use
        public static TreeManager Instance
        {
            get
            {
                // If _instance hasn't been set yet, we grab it from the scene.
                // This will only happen the first time this reference is used.
                if (_instance == null)
                    _instance = FindObjectOfType<TreeManager>();
                return _instance;
            }
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnEnable()
        {
            // Exit immediately if there are no Destructible trees. In that case, there is nothing to manage.
            if (destructibleTrees == null || destructibleTrees.Count == 0) return;
            
            // Get terrain to manage
            if (terrain == null)
                terrain = Terrain.activeTerrain; 

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("No terrain to manage destructible trees on.");
                return;
            }

            TreePrototype[] treePrototypes = terrain.terrainData.treePrototypes;
            TreeInstance[] treeInstances = terrain.terrainData.treeInstances;
            
#if UNITY_EDITOR
            // Check if there is already a backup of the terrainData and exit if so.
            string terrainDataPath = AssetDatabase.GetAssetPath(terrain.terrainData);
            string terrainDataBkpPath = terrainDataPath.Replace(".asset", "") + "_bkp.asset";
            TerrainData terrainDataBkp = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataBkpPath);
            if (terrainDataBkp != null)
            {
                // A terrainData backup already exists. Log an error and exit.
                isTerrainDataDirty = true;
                Debug.LogError("Cannot backup terrainData for [" + terrain.terrainData.name + "]. A backup already exists. Please exit Play mode to fix.");
                return;
            }
#endif
            
            if (treeInstances == null || treeInstances.Length == 0 || treePrototypes == null || treePrototypes.Length == 0)
            {
                Debug.LogWarning("No trees found on terrain. Nothing to manage.");
                return;
            }
            
            if (treePrototypes.Length != destructibleTrees.Count)
            {
                Debug.LogWarning("Tree prototypes do not match DestroyIt's tree stand-in prefabs. Please click the \"Update Trees\" button on the TreeManager script.");
                return;
            }

            for (int i = 0; i < treePrototypes.Length; i++)
            {
                if (destructibleTrees[i].Prefab == null || treePrototypes[i].prefab == null || treePrototypes[i].prefab.name != destructibleTrees[i].Prefab.name)
                {
                    Debug.LogWarning("Tree prototype names do not match Destructible tree stand-in prefab names. You may need to click the \"Update Trees\" button on the TreeManager script.");
                    return;
                }
            }
            
            // Capture original tree instances so we can reset them on application quit
            originalTreeInstances = treeInstances;
            currentTreeInstances = new List<TreeInstance>(treeInstances);
            treesToReset = new List<TreeReset>();

#if UNITY_EDITOR
            // Save the original terrainData object to a Resources folder, just in case there is a crash. 
            // This way, the TerrainPreserver can check for any Resources data to load and ask the user if he/she wants to restore terrainData.
            if (backupTerrain)
            {
                AssetDatabase.CopyAsset(terrainDataPath, terrainDataBkpPath);
                AssetDatabase.Refresh();
            }
#endif
            
            // For each terrain tree, place a stripped-down tree prototype object at its location
            for (int i = 0; i < treeInstances.Length; i++)
            {
                TreeInstance tree = treeInstances[i];
                DestructibleTree destructibleTree = destructibleTrees.Find(x => x.prototypeIndex == tree.prototypeIndex);
                if (destructibleTree == null) continue;
                
                GameObject treeObj = Instantiate(destructibleTree.Prefab, terrain.transform.parent, true);
                treeObj.transform.position = terrain.WorldPositionOfTree(i);
                treeObj.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
                treeObj.transform.rotation = Quaternion.AngleAxis(tree.rotation * Mathf.Rad2Deg, Vector3.up);
            }

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void FixedUpdate()
        {
            if (treesToReset == null || treesToReset.Count == 0) return;
            List<TreeReset> treesReset = new List<TreeReset>();
            foreach (TreeReset tree in treesToReset)
            {
                if (DateTime.Now >= tree.resetTime)
                {
                    TreeInstance treeInstance = new TreeInstance
                    {
                        position = tree.position,
                        color = Color.white,
                        heightScale = 1,
                        widthScale = 1,
                        prototypeIndex = tree.prototypeIndex
                    };
                    terrain.AddTreeInstance(treeInstance);
                    treesReset.Add(tree);
                }
            }
            
            foreach(TreeReset tree in treesReset)
                treesToReset.Remove(tree);
        }

#if UNITY_EDITOR
        public void UpdateTrees()
        {
            // Get the current scene asset so we can find the InstanceId assigned to it.
            Object sceneAsset = AssetDatabase.LoadAssetAtPath<Object>(SceneManager.GetActiveScene().path);
            if (sceneAsset == null)
            {
                Debug.LogWarning("Could not update trees. You must first save your Scene using File => Save Scene.");
                return;
            }

            // Get the MD5 Hash of the scene and add it to the destructible tree stand-in path so we can manage destructible trees for multiple scenes.
            string sceneMD5Hash = GetMD5Hash(sceneAsset.name, 8);
            string path = pathToStandIns + sceneMD5Hash + "/";
            
            // Clear out all existing tree prototype stand-in prefabs.
            destructibleTrees = new List<DestructibleTree>();
            
            if (Directory.Exists(path)) { Directory.Delete(path, true); }
            Directory.CreateDirectory(path);
            
            // Get terrain to manage
            if (terrain == null)
                terrain = Terrain.activeTerrain;

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("No terrain to update trees on.");
                return;
            }

            if (terrain.terrainData.treeInstances == null || terrain.terrainData.treeInstances.Length == 0 ||
                terrain.terrainData.treePrototypes == null || terrain.terrainData.treePrototypes.Length == 0)
            {
                Debug.LogWarning("No trees to update.");
                return;
            }

            // For each tree prototype prefab in the terrain
            for (int i = 0; i < terrain.terrainData.treePrototypes.Length; i++)
            {
                GameObject treePrefab = terrain.terrainData.treePrototypes[i].prefab;
                    
                // Make a new tree object destructible stand-in
                GameObject treeObj = Instantiate(treePrefab);
                treeObj.name = treePrefab.name;
                
                // If the tree prototype gameobject is a SpeedTree, tag it so we can determine proper rotation of the tree instance at runtime.
                if (IsSpeedTree(treeObj))
                    treeObj.AddTag(Tag.SpeedTree);

                // Strip the tree object down to essentials-only
                // NOTE: Add any additional components that you don't want to be removed from trees here
                foreach (Component comp in treeObj.GetComponentsInChildren<Component>())
                {
                    if (comp.GetType() != typeof(Transform) && comp.GetType() != typeof(CapsuleCollider) && comp.GetType() != typeof(BoxCollider) && 
                        comp.GetType() != typeof(SphereCollider) && comp.GetType() != typeof(MeshCollider) && comp.GetType() != typeof(Destructible) &&
                        comp.GetType() != typeof(HitEffects) && comp.GetType() != typeof(TagIt) && comp.GetType() != typeof(ParticleSystem) && 
                        comp.GetType() != typeof(ParticleSystemRenderer) && comp.GetType() != typeof(WhenDestroyedResetTree)
                        && comp.GetType() != typeof(WhenDamaged))
                        DestroyImmediate(comp);
                }
                
                // Tag the gameobject as a tree so we will know later to also remove its terrain tree instance if it is destroyed
                treeObj.AddTag(Tag.TerrainTree);
                
                // Save the tree object as a prefab
                string treeName = treeObj.name;
                string localPath = path + treeName + ".prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(treeObj, localPath, InteractionMode.AutomatedAction);
                DestroyImmediate(treeObj);
                
                // Load the new prefab from the Resources folder and add to the collection of destructible trees
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + treeName + ".prefab");
                destructibleTrees.Add(new DestructibleTree{prototypeIndex = i, Prefab = prefab});
            }

            Debug.Log(destructibleTrees.Count + " tree stand-ins updated to match prefabs.");
        }
#endif

        // Determines whether a tree prototype game object represents a SpeedTree tree.
        private bool IsSpeedTree(GameObject treeObj)
        {
            MeshRenderer[] meshes = treeObj.gameObject.GetComponentsInChildren<MeshRenderer>();
            if (meshes == null || meshes.Length <= 0) return false;
            for (int j = 0; j < meshes.Length; j++)
            {
                Material[] mats = meshes[j].sharedMaterials;
                for (int k = 0; k < mats.Length; k++)
                {
                    if (mats[k].shader.name.Contains("SpeedTree"))
                        return true;
                }
            }
            return false;
        }

        public void DestroyTreeAt(Vector3 worldPoint)
        {
            TerrainTree tree = terrain.ClosestTreeToPoint(worldPoint);
            if (tree == null) return;

            DestroyTree(tree);
        }

        private void DestroyTree(TerrainTree tree)
        {
            // Hide the tree on the terrain by scaling it down to 0,0,0.
            TreeInstance ti = currentTreeInstances[tree.Index];
            ti.heightScale = 0f;
            ti.widthScale = 0f;
            currentTreeInstances[tree.Index] = ti;

            // Assign the tree instances back to the terrain data.
            terrain.terrainData.treeInstances = currentTreeInstances.ToArray();
        }

        /// <summary>Restores the original trees back to the Terrain data on a CLEAN exit or scene change.</summary>
        public void RestoreTrees()
        {
#if UNITY_EDITOR
            if (isTerrainDataDirty)
            {
                // Don't modify the terrainData if there is a backup that hasn't been resolved yet.
                Debug.LogWarning("TerrainData is dirty (there is a backup that has not been resolved). Exiting restore process to prevent overwriting.");
                return;
            }
#endif

            if (originalTreeInstances == null)
            {
                //Debug.Log("No original tree instances to restore. Exiting.");
                return;
            }

            if (terrain == null)
            {
                //Debug.Log("No terrain, therefore no trees to restore on TerrainData.");
                return;
            }

            if (terrain.terrainData == null)
            {
                //Debug.Log("No TerrainData, therefore no trees to restore on TerrainData.");
                return;
            }

            if (terrain.terrainData.treeInstances == null)
            {
                //Debug.Log("No tree instances on the terrain. Therefore, nothing to restore.");
                return;
            }

            terrain.terrainData.treeInstances = originalTreeInstances;
#if UNITY_EDITOR
            // Delete the backup
            string terrainDataPath = AssetDatabase.GetAssetPath(terrain.terrainData);
            string terrainDataBkpPath = terrainDataPath.Replace(".asset", "") + "_bkp.asset";
            TerrainData terrainDataBkp = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataBkpPath);
            if (terrainDataBkp != null)
            {
                AssetDatabase.DeleteAsset(terrainDataBkpPath);
                AssetDatabase.Refresh();
                //Debug.Log("TerrainData restored, deleted backup file.");
            }
#endif
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            RestoreTrees();
        }
        
        private void OnApplicationQuit()
        {
            RestoreTrees();
        }

        private void OnDestroy()
        {
            RestoreTrees();
        }
        
        private string GetMD5Hash(string input, int length)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = md5.ComputeHash(bytes);

            int len = length <= hash.Length ? length : hash.Length;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < len; i++)
                sb.Append(hash[i].ToString("x2"));

            return sb.ToString();
        }
    }
}