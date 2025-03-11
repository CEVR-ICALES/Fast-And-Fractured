using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public class ObjectPoolManager : MonoBehaviour
    {
        private static ObjectPoolManager _instance;

        public static ObjectPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("ObjectPoolManager is NULL");
                }
                return _instance;
            }
        }
        //Function in custom start
        private void Awake()
        {
            if (_instance != null)
                Destroy(gameObject);
            else
            {
                _instance = this;
            }
        }

        private List<ObjectPool> _objectPools = new List<ObjectPool>();
        private Transform _parentGameObjectOfPools;
        [SerializeField]
        private string parentGameObjectOfPoolsName = "ObjectPools";

        public void CustomStart()
        {
            _parentGameObjectOfPools = new GameObject(parentGameObjectOfPoolsName).transform;
        }

        //Method called in Level Controller or Game Manager. One of them will handle a list of ScriptableObjects with the differents pools
        public void CreateObjectPool(ObjectPoolSO objectPoolSO)
        {

            var poolGameObject = new GameObject(objectPoolSO.poolName);
            poolGameObject.transform.parent = _parentGameObjectOfPools;
            if (objectPoolSO.prefab != null)
            {
                ObjectPool newObjectPool = new ObjectPool(objectPoolSO.pooltype, objectPoolSO.poolNum, objectPoolSO.poolNum);
                var pooledGameObjectList = InstanceObjectPoolGameObjects(objectPoolSO.prefab, objectPoolSO.poolNum, poolGameObject.transform);
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
        private GameObject[] InstanceObjectPoolGameObjects(GameObject gameobjectToPool, int num, Transform parent)
        {
            GameObject[] gameObjectsPooled = new GameObject[num];
            for (int i = 0; i < num; i++) {
                var gameObjectPooled = Instantiate(gameobjectToPool, parent);
                gameObjectPooled.SetActive(false);
                gameObjectsPooled[i] = gameObjectPooled;
            }
            return gameObjectsPooled;
        }

        //Give a Pooled Object to a IRequestPool if the current is not active. 
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
