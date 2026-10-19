using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;

namespace Utilities
{
    public class ObjectPoolManager : AbstractSingleton<ObjectPoolManager>
    {
       private Dictionary<ObjectPool, Transform> _poolParentList;

        private Dictionary<Pooltype, ObjectPool> _objectPools;
        private Transform _parentGameObjectOfPools;
        [SerializeField]
        private string parentGameObjectOfPoolsName = "ObjectPools";
        [SerializeField] private List<ObjectPoolSO> poolSOList;

        protected override void Construct()
        {
            base.Construct();
            _objectPools = new Dictionary<Pooltype, ObjectPool>();
            _parentGameObjectOfPools = new GameObject(parentGameObjectOfPoolsName).transform;
            _poolParentList = new Dictionary<ObjectPool, Transform>();

            foreach (var poolSO in poolSOList)
            {
                CreateObjectPool(poolSO);
            }
        }
        protected override void Initialize()
        {

        }

        //Method called in Level Controller or Game Manager. One of them will handle a list of ScriptableObjects with the differents pools
        public void CreateObjectPool(ObjectPoolSO objectPoolSO)
        {

            var poolGameObject = new GameObject(objectPoolSO.poolName);
            poolGameObject.transform.parent = _parentGameObjectOfPools;
            if (objectPoolSO.prefab != null)
            {
                ObjectPool newObjectPool = new ObjectPool(objectPoolSO.pooltype, objectPoolSO.poolNum, objectPoolSO.poolNum);
                var pooledGameObjectList = InstanceObjectPoolGameObjects(objectPoolSO.prefab, objectPoolSO.prefabVariants, objectPoolSO.poolNum, poolGameObject.transform);
                foreach (GameObject pooledGameObject in pooledGameObjectList)
                {
                    newObjectPool.AddObject(pooledGameObject);
                }
                _objectPools.TryAdd(objectPoolSO.pooltype, newObjectPool);
                _poolParentList.Add(newObjectPool, _parentGameObjectOfPools);
            }
            else
                Debug.LogError("ObjectPoolSO " + objectPoolSO.name + "have a empty prefab.");
        }

        //Create and prepare the GameObjects of a pool in scene
        private GameObject[] InstanceObjectPoolGameObjects(GameObject gameobjectToPool, GameObject[] prefabVariants, int num, Transform parent)
        {
            GameObject[] gameObjectsPooled = new GameObject[num];
            int variantCount = 0;
            int variantMaxCount = 0;
            if (prefabVariants != null)
            {
                variantMaxCount = prefabVariants.Length;
            }
            for (int i = 0; i < num; i++)
            {
                GameObject gameObjectPooled = null;
                // Index 0 of the cycle is the base object, 1..variantMaxCount the variants.
                int cycleSize = variantMaxCount + 1;
                int cycleIndex = i % cycleSize;

                if (cycleIndex != 0 && variantMaxCount > 0)
                {
                    gameObjectPooled = Instantiate(prefabVariants[cycleIndex - 1], parent);
                }
                else
                {
                    gameObjectPooled = Instantiate(gameobjectToPool, parent);
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
                if (variantCount >= variantMaxCount)
                    variantCount = 0;
            }
            return gameObjectsPooled;
        }

        public GameObject GivePooledObject(Pooltype pooltype, Vector3? position = null)
        {
            if (_objectPools.TryGetValue(pooltype, out var objectPool))
            {
                if (!objectPool.IsNextObjectActive())
                {
                    objectPool.NextIndex();
                    GameObject objectPooled = objectPool.GetCurrentObject(out var IpooledObject);
                    if (IpooledObject != null)
                    {
                        if (!objectPooled.activeSelf)
                        {
                            if (position != null)
                            {
                                objectPooled.transform.position = (Vector3)position;
                            }
                            objectPooled.SetActive(true);
                            return objectPooled;
                        }
                        return null;
                    }
                    return null;
                }
                return null;
            }
            Debug.LogError("ObjectPool list of type " + pooltype + " not exist.");
            return null;
        }

        public T GivePooledObjectOfType<T>(Pooltype pooltype) where T : IPooledObject
        {
            if (_objectPools.TryGetValue(pooltype, out var objectPool))
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
            Debug.LogError("ObjectPool list of type " + pooltype + " not exist.");
            return default;
        }

        public List<GameObject> GiveAllMyPooledObjects(Pooltype pooltype)
        {
            if (_objectPools.TryGetValue(pooltype, out var objectPool))
            {
                return objectPool.GetAllObjectsInPool();
            }
            Debug.LogError("ObjectPool list of type " + pooltype + " not exist.");
            return null;
        }

        public void DeactivatePooledObject(IPooledObject pooledObject, GameObject instance)
        {
            if (_objectPools.TryGetValue(pooledObject.Pooltype, out var objectPool))
            {
                if (objectPool.IsIntheList(instance))
                {
                    instance.SetActive(false);
                }
                if (_poolParentList.TryGetValue(objectPool, out Transform parent))
                {
                    instance.transform.parent = parent;
                }
            }
        }

        private ObjectPool FindObjectPoolInList(Pooltype type)
        {
            if (_objectPools.TryGetValue(type, out var objectPool))
                return objectPool;

            Debug.LogError("ObjectPool list of type " + type + " not exist.");
            return null;
        }
    }
}

