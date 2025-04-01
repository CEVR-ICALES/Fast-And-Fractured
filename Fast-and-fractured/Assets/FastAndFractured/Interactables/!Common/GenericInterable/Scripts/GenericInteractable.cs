using UnityEngine;
using UnityEngine.Events;

namespace FastAndFractured
{
    public class GenericInteractable : MonoBehaviour, IInteractable
    {
        public UnityEvent<GameObject, GameObject> onInteract;
        public UnityEvent onInteractEmpty;
        public bool disableGameObjectOnInteract = false;
        public virtual void OnInteract(GameObject interactionFrom, GameObject interactionTo)
        {

            onInteractEmpty?.Invoke();
            onInteract?.Invoke(interactionFrom, interactionTo);
            if (disableGameObjectOnInteract)
            {
                gameObject.SetActive(false);
            }

        }
    }
}
