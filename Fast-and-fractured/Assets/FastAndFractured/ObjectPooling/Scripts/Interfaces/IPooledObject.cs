using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game {
    public interface IPooledObject
    {
        public Pooltype Pooltype { get; set; }
        //private Pooltype _pooltype;
        public delegate void EndAction(GameObject instance, Pooltype type);
        public EndAction OnEndAction { get; set; }
        //public IPooledObject.EndAction onEndaction;
    }
}
