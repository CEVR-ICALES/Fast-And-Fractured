using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    public class MeleeArea : MonoBehaviour
    {
        public int damageAmount = 30;
        public int repairAmount = 20;
        public float meleeRadius = 1.3f;
        public float additionalForceAmount = 150f;
        public float additionalForceRadius = 2f;
        public ParticleSystem repairEffect;

        public void OnMeleeDamage()
        {
            Collider[] objectsInRange = Physics.OverlapSphere(transform.position, meleeRadius);
            List<Destructible> damagedObjects = new List<Destructible>(); // Keep track of what objects have been damaged so we don't do damage multiple times per collider.
            bool hasPlayedHitEffect = false;

            foreach (Collider col in objectsInRange)
            {
                // Ignore terrain colliders
                if (col is TerrainCollider) continue;

                // Ignore trigger colliders
                if (col.isTrigger) continue;

                // Ignore the player's character controller (ie, don't allow hitting yourself)
                if (col is CharacterController && col.tag == "Player") continue;

                if (!hasPlayedHitEffect) // Only play the hit effect once per melee attack.
                {
                    // Play hit effects
                    HitEffects hitEffects = col.gameObject.GetComponentInParent<HitEffects>();
                    if (hitEffects != null && hitEffects.effects.Count > 0)
                        hitEffects.PlayEffect(HitBy.Axe, transform.position, transform.forward * -1);

                    hasPlayedHitEffect = true;
                }

                // Apply impact force to rigidbody hit
                Rigidbody rbody = col.attachedRigidbody;
                if (rbody != null)
                    rbody.AddForceAtPosition(transform.forward * 3f, transform.position, ForceMode.Impulse);

                // Apply damage if object hit was Destructible
                // Only do this for active and enabled Destructible scripts found in parent objects
                // Special Note: Destructible scripts are turned off on terrain trees by default (to save resources), so we will make an exception for them and process the hit anyway
                Destructible[] destObjs = col.gameObject.GetComponentsInParent<Destructible>(false);
                foreach (Destructible destObj in destObjs)
                {
                    if (damagedObjects.Contains(destObj)) continue;
                    if (!destObj.isActiveAndEnabled && !destObj.isTerrainTree) continue;

                    damagedObjects.Add(destObj);
                    ImpactDamage meleeImpact = new ImpactDamage() { DamageAmount = damageAmount, AdditionalForce = additionalForceAmount,
                        AdditionalForcePosition = transform.position, AdditionalForceRadius = additionalForceRadius };
                    destObj.ApplyDamage(meleeImpact);
                }
            }
        }

        private void OnMeleeRepair()
        {
            Collider[] objectsInRange = Physics.OverlapSphere(transform.position, meleeRadius);
            List<Destructible> repairedObjects = new List<Destructible>(); // Keep track of what objects have been repaired so we don't repair multiple times per collider.
            bool hasPlayedRepairEffect = false;

            // Repair items within range
            foreach (Collider col in objectsInRange)
            {
                // Ignore terrain colliders
                if (col is TerrainCollider) continue;

                // Ignore trigger colliders
                if (col.isTrigger) continue;

                // Ignore the player's character controller (ie, don't allow hitting yourself)
                if (col is CharacterController && col.tag == "Player") continue;

                // Repair object if it is a Destructible
                Destructible destObj = col.gameObject.GetComponentInParent<Destructible>();
                if (destObj != null && !repairedObjects.Contains(destObj) && destObj.CurrentHitPoints < destObj.TotalHitPoints && destObj.canBeRepaired)
                {
                    repairedObjects.Add(destObj);
                    destObj.RepairDamage(repairAmount);
                    // Play repair particle effect
                    if (repairEffect != null && !hasPlayedRepairEffect)
                    {
                        repairEffect.GetComponent<ParticleSystem>().Clear(true);
                        repairEffect.Play(true);
                        hasPlayedRepairEffect = true;
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position, meleeRadius);
        }
    }
}