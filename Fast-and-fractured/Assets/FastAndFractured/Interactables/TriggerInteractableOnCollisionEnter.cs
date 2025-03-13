using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerInteractableOnCollisionEnter : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
        {
            collision.gameObject.GetComponent<IInteractable>()?.OnInteract(collision.transform.root.gameObject);
        }
    }
}
