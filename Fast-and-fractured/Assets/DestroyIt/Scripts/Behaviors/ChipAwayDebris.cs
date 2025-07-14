using UnityEngine;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace DestroyIt
{
    /// <summary>
    /// This script is attached to a debris piece at runtime. Your projectiles should check for collisions with 
    /// ChipAwayDebris and fire the BreakOff() method when impact occurs.
    /// </summary>
    public class ChipAwayDebris : MonoBehaviour
    {
        public float debrisMass = 1f;
        public float debrisDrag = 0f;
        public float debrisAngularDrag = 0.05f;
        private Renderer _rend;

        public void BreakOff(Vector3 force, Vector3 point)
        {
            if (!CheckCanBreakOff()) return;

            // Check whether a rigidbody already exists
            Rigidbody existingRBody = _rend.gameObject.GetComponent<Rigidbody>();
            Rigidbody rbody;

            // Make the debris fall away by attaching a rigidbody to it.
            rbody = existingRBody == null ? _rend.gameObject.AddComponent<Rigidbody>() : existingRBody;
            rbody.mass = debrisMass;
            rbody.drag = debrisDrag;
            rbody.angularDrag = debrisAngularDrag;
            rbody.AddForceAtPosition(force, point, ForceMode.Impulse);
            rbody.gameObject.transform.SetParent(null);

            Destroy(this);
        }

        public void BreakOff(float blastForce, float explosionRadius, float upwardsModifier)
        {
            if (!CheckCanBreakOff()) return;

            //Debug.Log("Breaking off");
            // Make the debris fall away by attaching a rigidbody to it.
            Rigidbody rbody = _rend.gameObject.AddComponent<Rigidbody>();
            rbody.mass = debrisMass;
            rbody.drag = debrisDrag;
            rbody.angularDrag = debrisAngularDrag;
            rbody.AddExplosionForce(blastForce, transform.position, explosionRadius, upwardsModifier);

            Destroy(this);
        }

        private bool CheckCanBreakOff()
        {
            // If no collider is on this object, then something weird happened. Exit and destroy self.
            if (GetComponent<Collider>() == null)
            {
                Destroy(this);
                return false;
            }

            _rend = gameObject.GetComponentInParent<Renderer>();
            if (_rend == null)
            {
                Destroy(this);
                return false;
            }

            // If a rigidbody already exists on this debris piece, remove this script and exit.
            if (GetComponent<Collider>().attachedRigidbody != null)
            {
                Destroy(this);
                return false;
            }
            return true;
        }
    }
}