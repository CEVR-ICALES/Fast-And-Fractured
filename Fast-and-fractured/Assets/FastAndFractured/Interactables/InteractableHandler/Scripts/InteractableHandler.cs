using Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        string _playerSelected = "";

        const int MAX_SKINS_TO_SPAWN = 2;
        const int PIECES_TO_UNLOCK_SKIN = 5;
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
        }

        void Start()
        {
            //Split to get only the name of the character, regardless of the skin
            _playerSelected = PlayerPrefs.GetString("Selected_Player").Split("_")[0];
            MakeInitialPool();
        }

        void MakeInitialPool()
        {
            _shuffledActivePool = interactablesToToggle.OrderBy(_ => UnityEngine.Random.Range(0, interactablesToToggle.Length)).ToList();
            CheckDespawnSkinInteractables();
            UpdateVisibleInteractableList();
        }

        
        void UpdateVisibleInteractableList(int index = 0)
        {
            for (int i = index; i < _shuffledActivePool.Count && i <= numberOfItemsActiveAtSameTime; i++)
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
        private List<string> GetPlayerUnlockableSkins()
        {
            List<string> unlockableSkins = new List<string>();
            //Get the number of skins of the character
            //that the player is using
            int numOfSkins = LevelController.Instance.playerReference.GetComponentInChildren<StatsController>().SkinCount;

            //Return null if player selected returns empty
            if (_playerSelected == "")
            {
                Debug.LogError("No player selected to get skins to unlock. " +
                    "HELLO?????!!!\n" +
                    "is that even possible...?");
                return null;
            }

            for (int i = 0; i < numOfSkins; i++)
            {
                string skinToCheck = _playerSelected + "_" + i; 
                if (PlayerPrefs.GetInt(skinToCheck) < PIECES_TO_UNLOCK_SKIN)
                {
                    unlockableSkins.Add(skinToCheck);
                }
            }
            return unlockableSkins;

        }

        private bool HasSkinsToUnlock()
        {
            List<string> skinsToUnlock = GetPlayerUnlockableSkins();
            return skinsToUnlock != null && skinsToUnlock.Count > 0;
        }

        private void CheckDespawnSkinInteractables()
        {
            // If there are no skins to unlock
            //Remove them from the list and deactivate them
            if (!HasSkinsToUnlock())
            {
                List<GameObject> skinInteractables = _shuffledActivePool.FindAll(s => s.GetComponentInChildren<SkinUnlockerInteractable>() != null);
                foreach (GameObject interact in skinInteractables)
                {
                    if (interact.GetComponentInChildren<SkinUnlockerInteractable>())
                    {
                        _shuffledActivePool.Remove(interact);
                        interact.SetActive(false);
                    }
                }
                return;
            } 
            List<string> skinsToUnlock = GetPlayerUnlockableSkins();

            //Random to select a random skin to unlock
            //Random starts at 1 because base skin is 0 and is unlocked by default
            //+1 is added because maximum on int is exclusive
            int skinSelected = UnityEngine.Random.Range(1, skinsToUnlock.Count+1);
            string playerSkinId = _playerSelected + "_" + skinSelected;

            int unlockedPieces = PlayerPrefs.GetInt(playerSkinId, 0);
            //Calculate how many pieces are needed to unlock the skin
            //If it is less than MAX_SKINS_TO_SPAWN, remove the necessary interactables
            int interactablesToDespawn = unlockedPieces - PIECES_TO_UNLOCK_SKIN + MAX_SKINS_TO_SPAWN;
            //int interactablesToDespawn = 2;
            for (int i = 0; i < interactablesToDespawn; i++)
            {
                GameObject skin = _shuffledActivePool.Find(s => s.GetComponentInChildren<SkinUnlockerInteractable>() != null);
                _shuffledActivePool.Remove(skin);
                skin.SetActive(false);
            }
            foreach (GameObject interact in _shuffledActivePool)
            {
                if (interact.TryGetComponent(out SkinUnlockerInteractable skin))
                {
                    skin.SkinToUnlock = playerSkinId;
                }
            }
        }

        public void DestroySkinInteractable(GameObject skinInteractable)
        {
            _shuffledActivePool.Remove(skinInteractable);
            skinInteractable.SetActive(false);
        }
        #endregion
        #endregion
    }
}


