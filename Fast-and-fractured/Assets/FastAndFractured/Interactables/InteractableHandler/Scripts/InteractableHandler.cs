using Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Utilities;
using static FastAndFractured.StatsBoostInteractable;

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

        public List<StatsBoostInteractable> GetStatBoostItems()
        {
            List<StatsBoostInteractable> statsBoostInteractables = new List<StatsBoostInteractable>();
            foreach (GameObject item in _shuffledActivePool)
            {
                StatsBoostInteractable statItem = item.GetComponentInParent<StatsBoostInteractable>();
                if (statItem)
                {
                    statsBoostInteractables.Add(statItem);
                }
            }
            return statsBoostInteractables;
        }

        public bool CheckIfStatItemExists(Stats stat)
        {
            List<StatsBoostInteractable> statsBoostInteractables = GetStatBoostItems();
            foreach (StatsBoostInteractable item in statsBoostInteractables)
            {
                foreach (StatsBoost boost in item.BoostList)
                {
                    if (boost.StatToBoost == stat)
                    {
                        return true;
                    }
                }   
            }
            return false;
        }

        public List<StatsBoostInteractable> GetOnlyStatBoostItemsStat(Stats stat)
        {
            List<StatsBoostInteractable> statsBoostInteractables = GetStatBoostItems();
            for (int i = 0; i < statsBoostInteractables.Count; i++)
            {
                StatsBoostInteractable item = statsBoostInteractables[i];
                foreach (StatsBoost boost in item.BoostList)
                {
                    if (boost.StatToBoost != stat)
                    {
                        statsBoostInteractables.Remove(item);
                    }
                }
            }
            return statsBoostInteractables;
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


