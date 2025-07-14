using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Place this script on a rigidbody parent that has one or more compound collider Destructible children under it. 
    /// Example: the SUV in the showcase demo scene. It is a rigidbody parent but not Destructible itself. But it has many child colliders that are destructible objects.
    /// </summary>
    public class DestructibleParent : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contacts.Length == 0) return;

            Destructible destructibleObj = collision.contacts[0].thisCollider.gameObject.GetComponentInParent<Destructible>();
            if (destructibleObj != null)
            {
                Rigidbody otherRbody = collision.contacts[0].otherCollider.attachedRigidbody;
                if (otherRbody != null)
                    destructibleObj.ProcessDestructibleCollision(collision, otherRbody);
                else
                    destructibleObj.ProcessDestructibleCollision(collision, this.GetComponent<Rigidbody>());
            }
        }
    }
}