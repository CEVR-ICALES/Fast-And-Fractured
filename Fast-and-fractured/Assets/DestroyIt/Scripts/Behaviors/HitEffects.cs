using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This script allows you to assign particle system hit effects for each weapon type.
    /// PlayEffect() will attempt to play the hit effect for the specified weapon type.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitEffects : MonoBehaviour
    {
        public List<HitEffect> effects;

        public void PlayEffect(HitBy weaponType, Vector3 hitPoint, Vector3 hitNormal)
        {
            foreach (var eff in effects)
            {
                if ((eff.hitBy & weaponType) > 0)
                {
                    if (eff.effect != null)
                        ObjectPool.Instance.Spawn(eff.effect, hitPoint, Quaternion.LookRotation(hitNormal));
                }
            }
        }
    }
}
