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
        public Pooltype Pooltype;
        public string PoolName;
        public GameObject Prefab;
        public int PoolNum;
    }
}
