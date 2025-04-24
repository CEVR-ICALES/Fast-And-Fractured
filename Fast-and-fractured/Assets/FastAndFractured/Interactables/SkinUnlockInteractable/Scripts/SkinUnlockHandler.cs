using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinUnlockHandler : MonoBehaviour
{
    private string _playerSelected = "";
    private const int MAX_SKINS_TO_SPAWN = 2;
    private const int PIECES_TO_UNLOCK_SKIN = 5;

    public void Init()
    {
        _playerSelected = PlayerPrefs.GetString("Selected_Player").Split("_")[0];
    }

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

    public void CheckDespawnSkinInteractables()
    {
        // If there are no skins to unlock
        //Remove them from the list and deactivate them
        if (!HasSkinsToUnlock())
        {
            List<GameObject> skinInteractables = InteractableHandler.Instance.ShuffledActivePool.FindAll(s => s.GetComponentInChildren<SkinUnlockerInteractable>() != null);
            foreach (GameObject interact in skinInteractables)
            {
                if (interact.GetComponentInChildren<SkinUnlockerInteractable>())
                {
                    InteractableHandler.Instance.DestroySkinInteractable(interact);
                }
            }
            return;
        }
        List<string> skinsToUnlock = GetPlayerUnlockableSkins();

        //Random to select a random skin to unlock
        //Random starts at 1 because base skin is 0 and is unlocked by default
        //+1 is added because maximum on int is exclusive
        int skinSelected = UnityEngine.Random.Range(1, skinsToUnlock.Count + 1);
        string playerSkinId = _playerSelected + "_" + skinSelected;

        int unlockedPieces = PlayerPrefs.GetInt(playerSkinId, 0);
        //Calculate how many pieces are needed to unlock the skin
        //If it is less than MAX_SKINS_TO_SPAWN, remove the necessary interactables
        int interactablesToDespawn = unlockedPieces - PIECES_TO_UNLOCK_SKIN + MAX_SKINS_TO_SPAWN;
        //int interactablesToDespawn = 2;
        List<GameObject> skinList = InteractableHandler.Instance.ShuffledActivePool.FindAll(s => s.GetComponentInChildren<SkinUnlockerInteractable>() != null);
        for (int i = 0; i < skinList.Count; i++)
        {
            if (i < interactablesToDespawn)
            {
                InteractableHandler.Instance.DestroySkinInteractable(skinList[i]);
            }
            else
            {
                skinList[i].GetComponentInChildren<SkinUnlockerInteractable>().SkinToUnlock = playerSkinId;
            }
        }
    }

}
