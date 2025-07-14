using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DestroyIt
{
    public class SetupDestroyIt
    {
        [MenuItem("Window/DestroyIt/Setup - Minimal")]
        public static void SetupMinimalMenuOption()
        {
            GameObject destroyIt;
            DestructionManager destructionManager = Object.FindObjectOfType<DestructionManager>();
            if (destructionManager != null)
                destroyIt = destructionManager.gameObject;
            else
                destroyIt = new GameObject("DestroyIt"); 
            
            destroyIt.AddComponent<DestructionManager>();
            destroyIt.AddComponent<ParticleManager>();
            ObjectPool pool = destroyIt.AddComponent<ObjectPool>();

            DestructionTest destructionTest = Object.FindObjectOfType<DestructionTest>();
            if (destructionTest == null)
            {
                GameObject destroyItTest = new GameObject("DestroyIt-InputTest");
                destroyItTest.AddComponent<DestructionTest>();
            }

            if (pool != null)
            {
                GameObject defaultLargeParticle = Resources.Load<GameObject>("Default_Particles/DefaultLargeParticle");
                GameObject defaultSmallParticle = Resources.Load<GameObject>("Default_Particles/DefaultSmallParticle");
                pool.prefabsToPool = new List<PoolEntry>();
                pool.prefabsToPool.Add(new PoolEntry() {Count = 10, Prefab = defaultLargeParticle});
                pool.prefabsToPool.Add(new PoolEntry() {Count = 10, Prefab = defaultSmallParticle});
            }
        }
        
        [MenuItem("Window/DestroyIt/Setup - Destructible Trees")]
        public static void SetupDestructibleTreesMenuOption()
        {
            EditorUtility.DisplayDialog("A Note About Destructible Trees",
                "NOTE: You will need to uncheck Enable Tree Colliders on your terrain in order to use destructible trees.\n\n" + 
                "Once you've added your trees to the terrain, click the \"Update Trees\" button on the TreeManager, and DestroyIt will " + 
                "create game objects with colliders and place them over the terrain tree instances so they can be destroyed.", "Ok");
            
            DestructionManager destructionManager = Object.FindObjectOfType<DestructionManager>();
            if (destructionManager == null)
                SetupMinimalMenuOption();
            
            destructionManager = Object.FindObjectOfType<DestructionManager>();
            destructionManager.gameObject.AddComponent<TreeManager>();
        }
    }
}

