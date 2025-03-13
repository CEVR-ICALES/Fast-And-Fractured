using UnityEngine;
using UnityEngine.Events;

public class GenericInteractable : MonoBehaviour, IInteractable
{
    public UnityEvent<GameObject,GameObject> onInteract;
    public UnityEvent onInteractEmpty;
    public bool disableGameObjectOnInteract = false;
    public virtual void OnInteract(GameObject interactionFrom, GameObject intearactionTo)
    {

        onInteractEmpty?.Invoke();
        onInteract?.Invoke(interactionFrom,intearactionTo);
        if (disableGameObjectOnInteract)
        {
            gameObject.SetActive(false);    
        }

    }
}