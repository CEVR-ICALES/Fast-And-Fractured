using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game {
    public interface IPooledObject
    {
        public Pooltype Pooltype { get; set; }
        //private Pooltype _pooltype;
        public bool InitValues { get;}
        /// <summary>
        /// Method called at the PooledObject creation.
        /// </summary>
        public abstract void InitializeValues();
    }
}
