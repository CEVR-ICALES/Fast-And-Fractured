using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public interface IRequestPool
    {
        public Pooltype PoolType { get;}
        public GameObject RequestPool();
        //return ObjectPoolManager.Instance.GivePooledObject(PoolType);
    }
}
