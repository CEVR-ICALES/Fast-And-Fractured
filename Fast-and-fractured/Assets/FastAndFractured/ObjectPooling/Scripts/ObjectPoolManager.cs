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
        //Function in customstart
        private void Awake()
        {
            if (_instance != null)
                Destroy(gameObject);
            else
            {
                DontDestroyOnLoad(gameObject);
                _instance = this;
            }
        }

        private List<ObjectPool> _objectPools = new List<ObjectPool>();
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void CreateObjectPool(ObjectPoolSO objectPoolSO)
        {
            var poolGameObject = new GameObject(objectPoolSO.PoolName);
            GameObject[] pooledGameObjectList = InstanceObjectPoolGameObjects(objectPoolSO.Prefab, objectPoolSO.PoolNum,poolGameObject.transform);
            _objectPools.Add(new ObjectPool(objectPoolSO.Pooltype,objectPoolSO.PoolNum));
            foreach (GameObject pooledGameObject in pooledGameObjectList)
            {
                _objectPools[^1].AddObject(pooledGameObject);
            }
        }

        private GameObject[] InstanceObjectPoolGameObjects(GameObject gameobjectToPool, int num, Transform parent)
        {
            GameObject[] gameObjectsPooled = new GameObject[num];
            for (int i = 0; i < num; i++) {
                var gameObjectPooled = Instantiate(gameobjectToPool);
                gameObjectPooled.transform.parent = parent;
                gameObjectPooled.SetActive(false);
                gameObjectsPooled[i] = gameObjectPooled;
            }
            return gameObjectsPooled;
        }

        public GameObject GivePooledObject(Pooltype pooltype)
        {
            var objectPool = FindObjectPoolInList(pooltype);
            if (!objectPool.IsNextObjectActive()) {
                objectPool.NextIndex();
                GameObject objectPooled = objectPool.GetCurrentObject(out var IpooledObject);
                if (IpooledObject!=null)
                {
                    if (!objectPooled.activeSelf)
                    {
                        objectPooled.SetActive(true);
                        return objectPooled;
                    }
                }
            }
            return null;
        }

        public void DesactivePooledObject(IPooledObject pooledObject,GameObject instance)
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
            Debug.LogError("ObjectPool list don't exist or it's type is wrong");
            return null;
        }
    }
}
