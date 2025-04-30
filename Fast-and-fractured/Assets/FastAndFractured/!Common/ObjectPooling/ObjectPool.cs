using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;

namespace Utilities
{
    [System.Serializable]
    public class ObjectPool
    {
        private Pooltype _type;
        public Pooltype Type { get => _type; set => _type = value; }
        private List<GameObject> _objPoolList;
        private List<IPooledObject> _IpooledObjectsList;
        private int _maxCapacity;
        private int _index;

        public ObjectPool(Pooltype type, int maxCapacity, int indexInitValue)
        {
            _type = type;
            _objPoolList = new List<GameObject>();
            _IpooledObjectsList = new List<IPooledObject>();
            _maxCapacity = maxCapacity;
            _index = indexInitValue;
        }

        public List<GameObject> GetAllObjectsInPool()
        {
            return _objPoolList;
        }

        public GameObject GetCurrentObject(out IPooledObject IpooledObject)
        {
            IpooledObject = null;
            if (_objPoolList.Count > 0)
            {
                GameObject gameObjectPooled = _objPoolList[_index];
                if (gameObjectPooled == null)
                    Debug.LogError("ObjectPool of type " + _type + " list is returning a null gameObject.");
                IpooledObject = _IpooledObjectsList[_index];
                return gameObjectPooled;
            }
            else
            {
                Debug.LogError("ObjectPool of type " + _type + " list can't return any gameObject because is empty.");
                return null;
            }
        }

        public void NextIndex()
        {
           _index++;
            if (_index >= _maxCapacity)
            {
                _index = 0;
            }
        }

        public bool IsNextObjectActive()
        {
            var nextIndex = _index + 1;
            if(nextIndex >= _maxCapacity)
            {
                nextIndex = 0;
            }
            if (_objPoolList.Count > 0)
            {
                return _objPoolList[nextIndex].activeSelf;
            }
            Debug.LogError("Object Pool List of type " + _type + " is empty");
            return false;
        }

        public void AddObject(GameObject obj)
        {
            if (obj != null)
            {
                if (_objPoolList.Count <= _maxCapacity)
                {
                    _objPoolList.Add(obj);
                    if (obj.TryGetComponent<IPooledObject>(out var IpooledObject))
                    {
                        IpooledObject.Pooltype = _type;
                        _IpooledObjectsList.Add(IpooledObject);
                    }
                }
            }
            else
                Debug.LogError("The gameObject given to the ObjectPool list of type " + _type + " is null.");
        }

        public bool IsIntheList(GameObject obj)
        {
            if(obj != null)
            {
                return _objPoolList.Contains(obj);
            }
            return false;
        }
    }
}
