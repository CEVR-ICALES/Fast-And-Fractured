using Enums;
using NRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
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
        public List<GameObject> ShuffledActivePool => _shuffledActivePool;
        List<GameObject> _interactablesOnCooldown = new();
        public UnityEvent onPoolInitialize;


        private void OnEnable()
        {
            LevelControllerButBetter.Instance.charactersCustomStart.AddListener(MakeInitialPool);
        }

        private void OnDisable()
        {
            LevelControllerButBetter.Instance.charactersCustomStart.RemoveListener(MakeInitialPool);
            
        }
        protected override void Construct()
        {
            base.Construct();
            foreach (var item in interactablesToToggle)
            {
                GenericInteractable interactable = item.GetComponentInParent<GenericInteractable>();
                if (!interactable) continue;
                interactable.disableGameObjectOnInteract = true;
                interactable.onInteract.AddListener(RemoveInteractableFromPool);
            }
        }


        void MakeInitialPool()
        {
            _shuffledActivePool = interactablesToToggle.OrderBy(_ => DeterministicRandom.Instance.NextInt(0, interactablesToToggle.Length)).ToList();
            onPoolInitialize?.Invoke();
            UpdateVisibleInteractableList();
        }

        
        void UpdateVisibleInteractableList(int index = 0)
        {
            for (int i = index; i < _shuffledActivePool.Count && i <= numberOfItemsActiveAtSameTime; i++)
            {
                GameObject interactable = _shuffledActivePool[i];
                interactable.SetActive(true);
                if (interactable.TryGetComponent(out SkinUnlockerInteractable skinUnlocker))
                    SkinUnlockHandler.Instance.CheckDespawnSkinInteractables();    
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

        public List<StatsBoostInteractable> GetStatBoostItems()
        {
            List<StatsBoostInteractable> statsBoostInteractables = new List<StatsBoostInteractable>();
            foreach (GameObject item in _shuffledActivePool)
            {
                StatsBoostInteractable statItem = item.GetComponentInParent<StatsBoostInteractable>();
                if (statItem&&statItem.isActiveAndEnabled)
                {
                    statsBoostInteractables.Add(statItem);
                }
            }

            return statsBoostInteractables;
        }

        public List<GameObject> GetCurrentItemsInActivePool()
        {
            return _shuffledActivePool;
        }
        #region Helpers
        #region AI
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
        #endregion
        #region Skins
        public void DestroySkinInteractable(GameObject skinInteractable)
        {
            _shuffledActivePool.Remove(skinInteractable);
            skinInteractable.SetActive(false);
        }
        #endregion
        #endregion
    }
}


