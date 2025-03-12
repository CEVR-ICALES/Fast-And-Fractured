using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public interface IRequestPool
    {
        /// <summary>
        /// Request a GameObject in the pool of the same enum PoolType as the IRequestPool.
        /// 
        /// The function inherit must have "return ObjectPoolManager.Instance.GivePooledObject(PoolType);"
        /// </summary>
        /// <returns>The current pooled GameObject. It will return null if something happen. Consult the Console to see the problem</returns>
        public GameObject RequestPool(Pooltype pooltype); //Could be improven 
         //return ObjectPoolManager.Instance.GivePooledObject(PoolType);
         
    }
}
