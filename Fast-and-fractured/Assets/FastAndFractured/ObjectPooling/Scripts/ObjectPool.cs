using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [System.Serializable]
    public class ObjectPool
    {
        private Pooltype _type;
        public Pooltype Type { get => _type; set => _type = value; }
        private Queue<GameObject> _objPoolQueue;
        private int _maxCapacity;
        public int MaxCapacity { get => _maxCapacity; }
        public int NumOfPooledObjects { get => _objPoolQueue.Count; }

        public ObjectPool(Pooltype type, int maxCapacity)
        {
            _type = type;
            _objPoolQueue = new Queue<GameObject>();
            _maxCapacity = maxCapacity;
        }

        public GameObject GetFirstObject()
        {
            if (_objPoolQueue.Count > 0)
            {
                GameObject gameObjectPooled = _objPoolQueue.Dequeue();
                if (gameObjectPooled == null)
                    Debug.LogError("ObjectPool of type " + _type + " queue is returning a null gameObject.");
                return gameObjectPooled;
            }
            else
            {
                Debug.LogError("ObjectPool of type " + _type + " queue doesn't have any gameObject.");
                return null;
            }
        }

        public void AddObject(GameObject obj)
        {
            if (obj != null)
            {
                _objPoolQueue.Enqueue(obj);
            }
            else
                Debug.LogError("The gameObject given to the ObjectPool queue of type " + _type + " is null.");
        }

        public bool isIntheQueue(GameObject obj)
        {
            if(obj != null)
            {
                return _objPoolQueue.Contains(obj);
            }
            return false;
        }
    }
}
