using UnityEngine;
using UnityEngine.Events;

public class GenericInteractable : MonoBehaviour, IInteractable
{
    public UnityEvent<GameObject> onInteract;
    public UnityEvent onInteractEmpty;
    public bool disableGameObjectOnInteract = false;
    public virtual void OnInteract(GameObject interactionFrom)
    {

        onInteractEmpty?.Invoke();
        onInteract?.Invoke(interactionFrom);
        if (disableGameObjectOnInteract)
        {
            gameObject.SetActive(false);    
        }

    }
}