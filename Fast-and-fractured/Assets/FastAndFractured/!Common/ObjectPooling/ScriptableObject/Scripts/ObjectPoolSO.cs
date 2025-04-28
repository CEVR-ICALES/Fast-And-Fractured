using Enums;
using UnityEngine;

namespace Utilities
{
    [CreateAssetMenu(fileName = "ObjectPool.asset", menuName = "ObjectPool")]
    public class ObjectPoolSO : ScriptableObject
    {
        public Pooltype pooltype;
        public string poolName;
        public GameObject prefab;
        public GameObject[] prefabVariants;
        public int poolNum;
    }
}
