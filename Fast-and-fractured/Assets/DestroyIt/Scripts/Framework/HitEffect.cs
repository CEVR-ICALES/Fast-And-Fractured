using System;
using UnityEngine;

namespace DestroyIt
{
    [Serializable]
    public class HitEffect
    {
        /// <summary>The type of weapon the object was hit by. (Bullet, Slashing, Cannonball, etc)</summary>
        public HitBy hitBy;
        /// <summary>The effect to play when hit by this type of weapon.</summary>
        public GameObject effect;
    }
}
