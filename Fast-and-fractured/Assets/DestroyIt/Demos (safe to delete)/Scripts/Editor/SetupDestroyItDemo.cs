using UnityEditor;
using UnityEngine;

namespace DestroyIt
{
    public class SetupDestroyItDemo
    {   
        [MenuItem("Window/DestroyIt/Setup - First Person Controller")]
        public static void SetupFirstPersonControllerMenuOption()
        {
            DestructionManager destructionManager = Object.FindObjectOfType<DestructionManager>();
            if (destructionManager == null)
                SetupDestroyIt.SetupMinimalMenuOption();
            
            destructionManager = Object.FindObjectOfType<DestructionManager>();
            string fpControllerPath = "Assets/DestroyIt/Demos (safe to delete)/Prefabs/Character Controllers/First Person Controller.prefab";
            GameObject fpController = AssetDatabase.LoadAssetAtPath<GameObject>(fpControllerPath);

            if (fpController == null)
            {
                Debug.LogWarning("Could not find asset " + fpControllerPath);
                return;
            }
            // if there is already a Main camera in the scene, disable it.
            Camera cam = Camera.main;
            if (cam != null)
                cam.gameObject.SetActive(false);
            
            GameObject fpControllerObj = PrefabUtility.InstantiatePrefab(fpController) as GameObject;
            InputManager inputManager = fpControllerObj.GetComponent<InputManager>();
            ObjectPool pool = destructionManager.gameObject.GetComponent<ObjectPool>();
            pool.prefabsToPool.Add(new PoolEntry() {Count = 20, Prefab = inputManager.bulletPrefab});
            pool.prefabsToPool.Add(new PoolEntry() {Count = 5, Prefab = inputManager.rocketPrefab});
            pool.prefabsToPool.Add(new PoolEntry() {Count = 20, Prefab = inputManager.cannonballPrefab});
            pool.prefabsToPool.Add(new PoolEntry() {Count = 1, Prefab = inputManager.nukePrefab});
            pool.prefabsToPool.Add(new PoolEntry() {Count = 1, Prefab = inputManager.dustWallPrefab});

            string effectsPrefabPath = "Assets/DestroyIt/Demos (safe to delete)/Prefabs/Effects/";
            GameObject rocketSmokeTrailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(effectsPrefabPath + "Rocket Smoke Trail.prefab");
            pool.prefabsToPool.Add(new PoolEntry() {Count = 10, Prefab = rocketSmokeTrailPrefab});
            GameObject burstFlamePrefabPath = AssetDatabase.LoadAssetAtPath<GameObject>(effectsPrefabPath + "BurstFlame.prefab");
            pool.prefabsToPool.Add(new PoolEntry() {Count = 10, Prefab = burstFlamePrefabPath});
        }
    }
}

