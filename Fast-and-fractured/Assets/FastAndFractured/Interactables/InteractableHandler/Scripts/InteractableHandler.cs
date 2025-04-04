using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Utilities;

namespace FastAndFractured
{
    public class InteractableHandler : AbstractSingleton<InteractableHandler>
    {
        [SerializeField] GameObject[] interactablesToToggle;
        [SerializeField] private int numberOfItemsActiveAtSameTime = 8;
        [SerializeField] float itemCooldownAfterPick = 2f;

        List<GameObject> _shuffledActivePool = new();
        List<GameObject> _interactablesOnCooldown = new();
        protected override void Awake()
        {
            base.Awake();
            foreach (var item in interactablesToToggle)
            {
                GenericInteractable interactable = item.GetComponentInParent<GenericInteractable>();
                if (!interactable) continue;
                interactable.disableGameObjectOnInteract = true;
                interactable.onInteract.AddListener(RemoveInteractableFromPool);
            }
            MakeInitialPool();
        }


        void MakeInitialPool()
        {
            _shuffledActivePool = interactablesToToggle.OrderBy(_ => UnityEngine.Random.Range(0, interactablesToToggle.Length)).ToList();
            UpdateVisibleInteractableList();
        }
        void UpdateVisibleInteractableList(int index = 0)
        {
            for (int i = index; i < _shuffledActivePool.Count && i < numberOfItemsActiveAtSameTime; i++)
            {
                GameObject interactable = _shuffledActivePool[i];
                interactable.SetActive(true);
            }
            for (int i = numberOfItemsActiveAtSameTime; i < _shuffledActivePool.Count; i++)
            {
                GameObject interactable = _shuffledActivePool[i];
                interactable.SetActive(false);

            }
        }
        private void RemoveInteractableFromPool(GameObject interactionFrom, GameObject interactionTo)
        {
            _interactablesOnCooldown.Add(interactionTo);
            TimerSystem.Instance.CreateTimer(itemCooldownAfterPick, onTimerDecreaseComplete: () => ReAddInteractableFromCooldownToPool(interactionTo));
            _shuffledActivePool.Remove(interactionTo);
            interactionTo.gameObject.SetActive(false);
            UpdateVisibleInteractableList();
        }

        private void ReAddInteractableFromCooldownToPool(GameObject interactableOnCooldownToReAdd)
        {
            _interactablesOnCooldown.Remove(interactableOnCooldownToReAdd);
            _shuffledActivePool.Add(interactableOnCooldownToReAdd);
            UpdateVisibleInteractableList();
        }
    }
}


