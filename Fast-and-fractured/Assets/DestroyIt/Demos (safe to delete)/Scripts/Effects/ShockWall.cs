using UnityEngine;

namespace DestroyIt
{
    public class ShockWall : MonoBehaviour
    {
        public float blastForce = 200f;
        public float damageAmount = 200f;
        public Vector3 origin;

        private void OnTriggerEnter(Collider col)
        {
            // If it has a rigidbody, apply force
            Rigidbody rbody = col.attachedRigidbody;
            if (rbody != null && !rbody.isKinematic)
                rbody.AddExplosionForce(blastForce, origin, 0f, 0.5f);

            // Check for Chip-Away Debris
            ChipAwayDebris chipAwayDebris = col.gameObject.GetComponent<ChipAwayDebris>();
            if (chipAwayDebris != null)
            {
                if (Random.Range(1, 100) > 50) // Do this about half the time...
                {
                    chipAwayDebris.BreakOff(blastForce, 0f, 0.5f);
                    return; //Skip the destructible check if the debris hasn't chipped away yet.
                }

                return;
            }

            // If it's a destructible object, apply damage
            // Only do this for the first active and enabled Destructible script found in parent objects
            // Special Note: Destructible scripts are turned off on terrain trees by default (to save resources), so we will make an exception for them and process the damage anyway
            Destructible[] destObjs = col.gameObject.GetComponentsInParent<Destructible>(false);
            foreach (Destructible destObj in destObjs)
            {
                if (!destObj.isActiveAndEnabled && !destObj.isTerrainTree) continue;
                ExplosiveDamage explosiveDamage = new ExplosiveDamage { DamageAmount = damageAmount, BlastForce = blastForce, Position = origin, Radius = 0f, UpwardModifier = 0.5f };
                destObj.ApplyDamage(explosiveDamage);
                break;
            }
        }
    }
}