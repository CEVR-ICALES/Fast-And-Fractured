using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Attach this to any rigidbody object that acts as a projectile and may collide with 
    /// Destructible objects. This script will play particle effects when the object hits 
    /// something, and will do damage to Destructible objects.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyProjectile : MonoBehaviour
    {
        public HitBy weaponType = HitBy.Cannonball;
        [Tooltip("Impact velocity must be at least this amount to be detected as a hit.")]
        public float minHitVelocity = 10f;

        private Rigidbody rbody;
        private Vector3 lastVelocity;

        public void OnEnable()
        {
            rbody = GetComponent<Rigidbody>();
        }

        public void FixedUpdate()
        {
            lastVelocity = rbody.velocity;
        }

        public void OnCollisionEnter(Collision collision)
        {
            // Check that the impact is forceful enough to cause damage
            if (collision.relativeVelocity.magnitude < minHitVelocity) return;

            if (collision.contacts.Length == 0) return;

            Collider other = collision.contacts[0].otherCollider;

            // Play hit effects
            HitEffects hitEffects = other.gameObject.GetComponentInParent<HitEffects>();
            if (hitEffects != null && hitEffects.effects.Count > 0)
                hitEffects.PlayEffect(weaponType, collision.contacts[0].point, collision.contacts[0].normal);

            // Apply impact damage to Destructible objects without rigidbodies
            // Only do this for the first active and enabled Destructible script found in parent objects
            // Special Note: Destructible scripts are turned off on terrain trees by default (to save resources), so we will make an exception for them and process the collision anyway
            Destructible[] destObjs = other.gameObject.GetComponentsInParent<Destructible>(false);
            foreach (Destructible destObj in destObjs)
            {
                if (!destObj.isActiveAndEnabled && !destObj.isTerrainTree) continue; // ignore any disabled destructible terrain trees
                if (destObj.GetComponentInParent<DestructibleParent>() != null) continue; // ignore any destructible objects that have DestructibleParent scripts above them, that system already handles rigidbody collisions

                if (other.attachedRigidbody == null || other.attachedRigidbody.GetComponent<Destructible>() == null)
                {
                    if (collision.relativeVelocity.magnitude >= destObj.ignoreCollisionsUnder)
                    {
                        destObj.ProcessDestructibleCollision(collision, gameObject.GetComponent<Rigidbody>());
                        rbody.velocity = lastVelocity;
                        break;
                    }
                }
            }

            // Check for Chip-Away Debris
            ChipAwayDebris chipAwayDebris = collision.contacts[0].otherCollider.gameObject.GetComponent<ChipAwayDebris>();
            if (chipAwayDebris != null) 
                chipAwayDebris.BreakOff(collision.relativeVelocity * -1, collision.contacts[0].point);
        }
    }
}