using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class SkinUnlockHandler : AbstractSingleton<SkinUnlockHandler>
{
    private string _playerSelected = "";
    private const int MAX_SKINS_TO_SPAWN = 2;
    private const int PIECES_TO_UNLOCK_SKIN = 5;

    private void OnEnable()
    {
        InteractableHandler.Instance.onPoolInitialize.AddListener(CheckDespawnSkinInteractables);
    }
    private void OnDisable()
    {
        InteractableHandler.Instance.onPoolInitialize.RemoveListener(CheckDespawnSkinInteractables);
        StopAllCoroutines();
    }
 
    public void Start()
    {
        _playerSelected = PlayerPrefs.GetString("Selected_Player").Split("_")[0];
    }

    private List<string> GetPlayerUnlockableSkins()
    {
        List<string> unlockableSkins = new List<string>();

        int numOfSkins = LevelController.Instance.playerReference.GetComponentInChildren<StatsController>().SkinCount;

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
        List<GameObject> skinInteractables = InteractableHandler.Instance.ShuffledActivePool.FindAll(s => s.GetComponentInChildren<SkinUnlockerInteractable>() != null);

        List<string> skinsToUnlock = GetPlayerUnlockableSkins();

        //Random starts at 1 because base skin is 0 and is unlocked by default
        //+1 is added because maximum on int is exclusive
        int skinSelected = UnityEngine.Random.Range(1, skinsToUnlock.Count + 1);
        string playerSkinId = _playerSelected + "_" + skinSelected;

        int unlockedPieces = PlayerPrefs.GetInt(playerSkinId, 0);
        //Calculate how many pieces are needed to unlock the skin
        int interactablesToDespawn = unlockedPieces - PIECES_TO_UNLOCK_SKIN + MAX_SKINS_TO_SPAWN;
        if (!HasSkinsToUnlock())
        {
            interactablesToDespawn = MAX_SKINS_TO_SPAWN;
        }
        interactablesToDespawn = MAX_SKINS_TO_SPAWN - 1;

        for (int i = 0; i < skinInteractables.Count; i++)
        {
            if (i < interactablesToDespawn)
            {
                InteractableHandler.Instance.DestroySkinInteractable(skinInteractables[i]);
            }
            else
            {
                skinInteractables[i].GetComponentInChildren<SkinUnlockerInteractable>().SkinToUnlock = playerSkinId;
            }
        }
    }

}
