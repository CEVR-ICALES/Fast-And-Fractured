using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SkinUnlockerInteractable : GenericInteractable
    {
        //Is initialized in InteractableHandler
        private string _skinToUnlock;
        public string SkinToUnlock { get => _skinToUnlock; set => _skinToUnlock = value; }

        public override void OnInteract(GameObject interactionFrom, GameObject interactionTo)
        {
            //Get the character the player is using 
            PlayerPrefs.SetInt(_skinToUnlock, PlayerPrefs.GetInt(_skinToUnlock) + 1);
            InteractableHandler.Instance.DestroySkinInteractable(gameObject);
            //If the int of player prefs is 5, skin is unlocked
        }
    }
}


