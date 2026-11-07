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

        public void PlayPickupAudio(StatsController recipient)
        {
            if (!GameplayAudio.IsLocal(recipient)) return;
            if (this is StatsBoostInteractable boost && boost.BoostList != null)
            {
                var cue = GameAudioCues.CommonBuffPickup;
                foreach (var entry in boost.BoostList)
                {
                    if (entry.StatToBoost == Enums.Stats.ENDURANCE && entry.BoostValue > 0)
                    {
                        cue = GameAudioCues.CommonHeal;
                        break;
                    }
                    if (entry.StatToBoost == Enums.Stats.COOLDOWN_SPEED) cue = GameAudioCues.CommonCoolingComplete;
                }
                GameplayAudio.PlayPersonal(cue, recipient);
            }
            else if (!pickUpSound.IsNull && SoundManager.ExistingInstance != null)
                SoundManager.ExistingInstance.PlayOneShot(pickUpSound, transform.position);
        }

        public virtual void OnInteract(GameObject interactionFrom, GameObject interactionTo)
        {

            onInteractEmpty?.Invoke();
            onInteract?.Invoke(interactionFrom, interactionTo);

            PlayPickupAudio(interactionFrom.GetComponentInParent<StatsController>());
            
            if (disableGameObjectOnInteract)
            {
                gameObject.SetActive(false);
            }

        }
    }
}
