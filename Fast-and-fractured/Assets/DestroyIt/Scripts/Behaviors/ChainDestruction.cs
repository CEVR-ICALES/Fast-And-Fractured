using System.Collections.Generic;
using UnityEngine;
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace DestroyIt
{
    [RequireComponent(typeof(Destructible))]
    [RequireComponent(typeof(Rigidbody))]
    public class ChainDestruction : MonoBehaviour
    {
        [Tooltip("The amount of damage to apply per second to adjacent Destructible objects in the destructible chain. This will control how fast objects are destroyed.")]
        public float damagePerSecond = 125f;

        [Tooltip("If you would like to apply force on the debris pieces from a specific position point, you can assign a specific Transform location for that here. If you leave this empty, the gameObject's position will be used as the force origin point.")]
        public Transform forcePosition;

        [Tooltip("The amount of force to apply to the debris pieces (if any) when they are destroyed.")]
        public float forceAmount = 300f;
        
        [Tooltip("The size in game units (usually meters) of the force radius. A larger force radius will make debris pieces (if any) fly farther away from the force origin point.")]
        public float forceRadius = 5f;

        [Tooltip("The amount of upward push exerted on the debris pieces (if any). More upward push can make the force look more interesting or cinematic, but too much (say, over 2) can be unrealistic.")]
        public float forceUpwardModifier;

        [HideInInspector]
        public List<Destructible> adjacentDestructibles; // This is a list of adjacent Destructible objects, ones overlapping the trigger collider of this object.
        
        [Tooltip("Set to TRUE to cause this Destructible object to start taking damage at the predefined damage rate (Damage Per Second).")]
        public bool destroySelf;
        
        private Destructible _destObj; // Reference to the Destructible component on this gameObject.

        private void Start()
        {
            adjacentDestructibles = new List<Destructible>();
            
            // Attempt to get the Destructible script on the object. If found, attach the OnDestroyed event listener to the DestroyedEvent.
            _destObj = gameObject.GetComponent<Destructible>();
            if (_destObj != null)
                _destObj.DestroyedEvent += OnDestroyed;
            
            if (!HasTriggerCollider())
                Debug.LogWarning("No trigger collider found on ChainDestruction gameObject. You need a trigger collider for this script to work properly.");
        }

        private void Update()
        {
            if (!destroySelf) return;

            if (damagePerSecond > 0f)
            {
                // If you don't care about adding force to the debris pieces, uncomment this code to use a simpler method of applying damage.
                //destObj.ApplyDamage(damagePerSecond * Time.fixedDeltaTime);
                //return;

                // Apply damage with force on the debris pieces.
                Damage damage = new ExplosiveDamage()
                {
                    DamageAmount = damagePerSecond * Time.deltaTime,
                    BlastForce = forceAmount,
                    Position = forcePosition != null ? forcePosition.position : transform.position,
                    Radius = forceRadius,
                    UpwardModifier = forceUpwardModifier
                };

                _destObj.ApplyDamage(damage);
            }
        }

        private void OnDisable()
        {
            // Unregister the event listener when disabled/destroyed. Very important to prevent memory leaks due to orphaned event listeners!
            if (_destObj == null) return;
            _destObj.DestroyedEvent -= OnDestroyed;
        }

        /// <summary>When this Destructible object is destroyed, the code in this method will run.</summary>
        private void OnDestroyed()
        {
            if (adjacentDestructibles == null || adjacentDestructibles.Count == 0) return;

            // For each adjacent Destructible object, set DestroySelf to true for its ChainDestruction component, so it will start taking damage.
            for (int i = 0; i < adjacentDestructibles.Count; i++)
            {
                Destructible adjacentDest = adjacentDestructibles[i];
                if (adjacentDest == null) continue;
                ChainDestruction chainDest = adjacentDest.gameObject.GetComponent<ChainDestruction>();
                if (chainDest == null) continue;
                chainDest.destroySelf = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Add the nearby item to the list of adjacent Destructibles.
            var otherDestObj = other.gameObject.GetComponentInParent<Destructible>();
            if (otherDestObj != null && !adjacentDestructibles.Contains(otherDestObj))
                adjacentDestructibles.Add(otherDestObj);
        }
        
        private void OnTriggerExit(Collider other)
        {
            // Remove the item that is no longer nearby from the list of adjacent Destructibles.
            var otherDestObj = other.gameObject.GetComponentInParent<Destructible>();
            if (otherDestObj != null && adjacentDestructibles.Contains(otherDestObj))
                adjacentDestructibles.Remove(otherDestObj);
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