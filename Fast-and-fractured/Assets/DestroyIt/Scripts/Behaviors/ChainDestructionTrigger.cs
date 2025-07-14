using UnityEngine;
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable IdentifierTypo
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable CommentTypo

namespace DestroyIt
{
    /// <summary>
    /// This script triggers a chain destruction sequence on one or more Destructible objects that also have the
    /// ChainDestruction component. Put this script on a trigger collider and assign one or more Destructible
    /// objects that have the ChainDestruction component to the ChainDestructions collection.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChainDestructionTrigger : MonoBehaviour
    {
        public ChainDestruction[] chainDestructions;

        private void Start()
        {
            if (!HasTriggerCollider())
                Debug.LogWarning("No trigger collider found on ChainDestructionTrigger gameObject. You need a trigger collider for this script to work properly.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (chainDestructions == null || chainDestructions.Length == 0) return;
            
            // For each ChainDestruction component to trigger, set its destroySelf flag to True.
            for (int i = 0; i < chainDestructions.Length; i++)
            {
                if (chainDestructions[i] == null) continue;

                chainDestructions[i].destroySelf = true;
            }
        }

        /// <summary>Returns True if there is a trigger collider on this game object. Otherwise, returns False.</summary>
        private bool HasTriggerCollider()
        {
            Collider[] colls = gameObject.GetComponents<Collider>();
            if (colls == null) return false;
            for (int i = 0; i < colls.Length; i++)
            {
                if (colls[i].isTrigger)
                    return true;
            }

            return false;
        }
    }
}