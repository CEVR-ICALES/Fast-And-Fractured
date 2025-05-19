using FMODUnity;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

namespace FastAndFractured
{
    public class GenericInteractable : MonoBehaviour, IInteractable
    {
        public UnityEvent<GameObject, GameObject> onInteract;
        public UnityEvent onInteractEmpty;
        public bool disableGameObjectOnInteract = false;
        [SerializeField] private EventReference pickUpSound;

        public virtual void OnInteract(GameObject interactionFrom, GameObject interactionTo)
        {

            onInteractEmpty?.Invoke();
            onInteract?.Invoke(interactionFrom, interactionTo);

            SoundManager.Instance.PlayOneShot(pickUpSound,this.transform.position);
            if (disableGameObjectOnInteract)
            {
                gameObject.SetActive(false);
            }

        }
    }
}
