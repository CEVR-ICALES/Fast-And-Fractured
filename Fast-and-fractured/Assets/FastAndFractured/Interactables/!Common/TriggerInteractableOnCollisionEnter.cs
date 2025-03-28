using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class TriggerInteractableOnCollisionEnter : MonoBehaviour
    {
        [SerializeField] CollisionType collisionType;
        private void OnCollisionEnter(Collision collision)
        {
            if (collisionType != CollisionType.COLLISION || collision == null) { return; }
            collision.gameObject.GetComponentInParent<IInteractable>()?.OnInteract(this.transform.gameObject, collision.gameObject.transform.root.gameObject);
        }
        private void OnTriggerEnter(Collider other)
        {
            if (collisionType != CollisionType.TRIGGER || other == null) { return; }
            other.gameObject.GetComponentInParent<IInteractable>()?.OnInteract(this.transform.gameObject, other.gameObject.transform.root.gameObject);
        }
    }
    public enum CollisionType
    {
        COLLISION,
        TRIGGER
    }
}

