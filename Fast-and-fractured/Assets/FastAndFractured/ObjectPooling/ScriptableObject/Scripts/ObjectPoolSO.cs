using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public enum Pooltype
    {
        BULLET,
        INTERACTOR
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
