using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class SkinUnlockHandler : AbstractSingleton<SkinUnlockHandler>
{
    private string _playerSelected = "";
    private const int MAX_SKINS_TO_SPAWN = 5;
    private const int PIECES_TO_UNLOCK_SKIN = 5;

    private void OnEnable()
    {
        InteractableHandler.Instance.onPoolInitialize.AddListener(CheckDespawnSkinInteractables);
    }
    private void OnDisable()
    {
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

        for (int i = 0; i <= numOfSkins; i++)
        {
            string skinToCheck = _playerSelected + "_" + i;
            var piecesNeed = PlayerPrefs.GetInt(skinToCheck, 0);
            if (piecesNeed < PIECES_TO_UNLOCK_SKIN)
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
        List<GameObject> activeSkinInteractables = InteractableHandler.Instance.ShuffledActivePool.FindAll(s => s.GetComponentInChildren<SkinUnlockerInteractable>() != null);


        List<string> skinsPlayerCanUnlock = GetPlayerUnlockableSkins();

        if (skinsPlayerCanUnlock.Count == 0)
        {
            Debug.Log($"SkinUnlockHandler: Player {_playerSelected} has no skins to unlock. Despawning all {activeSkinInteractables.Count} active SkinInteractables.");
            DestroySkins(activeSkinInteractables);
            return;
        }
        int randomSkinIndex = UnityEngine.Random.Range(0, skinsPlayerCanUnlock.Count);
        string targetSkinIDForInteractables = skinsPlayerCanUnlock[randomSkinIndex];
        int piecesPlayerHasForTargetSkin = PlayerPrefs.GetInt(targetSkinIDForInteractables, 0);
        int piecesNeededForTargetSkin = PIECES_TO_UNLOCK_SKIN - piecesPlayerHasForTargetSkin;

        if (piecesNeededForTargetSkin <= 0)
        {
            Debug.LogError("PlayerPrefs broken");
            DestroySkins(activeSkinInteractables);
        }
        int numberOfInteractablesToConfigure = Mathf.Min(piecesNeededForTargetSkin, MAX_SKINS_TO_SPAWN);

        for (int i = 0; i < activeSkinInteractables.Count; i++)
        {
            GameObject currentInteractableGO = activeSkinInteractables[i];
            if (currentInteractableGO == null) continue;  

            if (i < numberOfInteractablesToConfigure)
            {
                SkinUnlockerInteractable skinUnlockerComp = currentInteractableGO.GetComponentInChildren<SkinUnlockerInteractable>();
                if (skinUnlockerComp != null)
                {
                    skinUnlockerComp.ChangeVisual(targetSkinIDForInteractables);
                }
                else
                {
                    InteractableHandler.Instance.DestroySkinInteractable(currentInteractableGO);
                }
            }
            else
            {
                InteractableHandler.Instance.DestroySkinInteractable(currentInteractableGO); 
            }
        }
    }
    private void DestroySkins(List<GameObject> skinsToDestroy)
    {
        foreach (GameObject interactableGO in skinsToDestroy)
        {
            if (interactableGO != null)
                InteractableHandler.Instance.DestroySkinInteractable(interactableGO);
        }
    }

}
