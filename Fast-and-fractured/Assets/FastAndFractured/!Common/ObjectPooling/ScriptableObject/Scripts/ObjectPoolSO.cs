using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utilities
{
    public enum Pooltype
    {
        BULLET,
        INTERACTOR,
        NORMAL_BULLET,
        PUSH_BULLET
    }
    [CreateAssetMenu(fileName = "ObjectPool.asset", menuName = "ObjectPool")]
    public class ObjectPoolSO : ScriptableObject
    {
        public Pooltype pooltype;
        public string poolName;
        public GameObject prefab;
        public int poolNum;
    }
}
