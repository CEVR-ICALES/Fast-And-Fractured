using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;

namespace Utilities
{
    public class ObjectPoolManager : AbstractSingleton<ObjectPoolManager>
    {
        //Function in custom start
        protected override void Awake()
        {
            base.Awake();
            _objectPools = new List<ObjectPool>();
            _parentGameObjectOfPools = new GameObject(parentGameObjectOfPoolsName).transform;
            foreach (var poolSO in poolSOList)
            {
                CreateObjectPool(poolSO);
            }
        }

        private List<ObjectPool> _objectPools;
        private Transform _parentGameObjectOfPools;
        [SerializeField]
        private string parentGameObjectOfPoolsName = "ObjectPools";
        [SerializeField] private List<ObjectPoolSO> poolSOList;

        //Method called in Level Controller or Game Manager. One of them will handle a list of ScriptableObjects with the differents pools
        public void CreateObjectPool(ObjectPoolSO objectPoolSO)
        {

            var poolGameObject = new GameObject(objectPoolSO.poolName);
            poolGameObject.transform.parent = _parentGameObjectOfPools;
            if (objectPoolSO.prefab != null)
            {
                ObjectPool newObjectPool = new ObjectPool(objectPoolSO.pooltype, objectPoolSO.poolNum, objectPoolSO.poolNum);
                var pooledGameObjectList = InstanceObjectPoolGameObjects(objectPoolSO.prefab,objectPoolSO.prefabVariants, objectPoolSO.poolNum, poolGameObject.transform);
                foreach (GameObject pooledGameObject in pooledGameObjectList)
                {
                    newObjectPool.AddObject(pooledGameObject);
                }
                _objectPools.Add(newObjectPool);
            }
            else
                Debug.LogError("ObjectPoolSO " + objectPoolSO.name + "have a empty prefab.");
        }
        
        //Create and prepare the GameObjects of a pool in scene
        private GameObject[] InstanceObjectPoolGameObjects(GameObject gameobjectToPool, GameObject[] prefabVariants, int num, Transform parent)
        {
            GameObject[] gameObjectsPooled = new GameObject[num];
            int variantCount = 0;
            int variantMaxCount = prefabVariants.Length;
            for (int i = 0; i < num; i++) {
                GameObject gameObjectPooled = null;
                if (variantCount == 0)
                {
                   gameObjectPooled = Instantiate(gameobjectToPool, parent);
                }
                else
                {
                    gameObjectPooled = Instantiate(prefabVariants[i],parent);
                }
                if (gameObjectPooled.TryGetComponent<IPooledObject>(out var pooledObject))
                {
                    if (pooledObject.InitValues)
                    {
                        pooledObject.InitializeValues();
                    }
                }
                gameObjectPooled.SetActive(false);
                gameObjectsPooled[i] = gameObjectPooled;
                variantCount++;
                if(variantCount>=variantMaxCount)
                    variantCount = 0;
            }
            return gameObjectsPooled;
        }

        public GameObject GivePooledObject(Pooltype pooltype)
        {
            var objectPool = FindObjectPoolInList(pooltype);
            if (objectPool != null)
            {
                if (!objectPool.IsNextObjectActive())
                {
                    objectPool.NextIndex();
                    GameObject objectPooled = objectPool.GetCurrentObject(out var IpooledObject);
                    if (IpooledObject != null)
                    {
                        if (!objectPooled.activeSelf)
                        {
                            objectPooled.SetActive(true);
                            return objectPooled;
                        }
                        return null;
                    }
                    return null;
                }
                return null;
            }
            return null;
        }

        public T GivePooledObjectOfType<T>(Pooltype pooltype) where T : IPooledObject
        {
            var objectPool = FindObjectPoolInList(pooltype);
            if (objectPool != null)
            {
                if (!objectPool.IsNextObjectActive())
                {
                    objectPool.NextIndex();
                    GameObject objectPooled = objectPool.GetCurrentObject(out var IpooledObject);
                    if (IpooledObject != null)
                    {
                        if (!objectPooled.activeSelf)
                        {
                            objectPooled.SetActive(true);
                            return (T)IpooledObject;
                        }
                        return default;
                    }
                    return default;
                }
                return default;
            }
            return default;
        }

        public List<GameObject> GiveAllMyPooledObjects(Pooltype pooltype)
        {
            ObjectPool objectPool = FindObjectPoolInList(pooltype);
            if (objectPool != null) {
                return objectPool.GetAllObjectsInPool();
            }
            return null;
        }

        public void DesactivatePooledObject(IPooledObject pooledObject,GameObject instance)
        {
            ObjectPool objectPool = FindObjectPoolInList(pooledObject.Pooltype);
                if (objectPool.IsIntheList(instance))
                {
                    instance.SetActive(false);
                }
        }

        private ObjectPool FindObjectPoolInList(Pooltype type)
        {
            foreach (ObjectPool objectPool in _objectPools)
            {
                if (objectPool.Type == type)
                    return objectPool;
            }
            Debug.LogError("ObjectPool list of type " + type + " not exist.");
            return null;
        }
    }
}

