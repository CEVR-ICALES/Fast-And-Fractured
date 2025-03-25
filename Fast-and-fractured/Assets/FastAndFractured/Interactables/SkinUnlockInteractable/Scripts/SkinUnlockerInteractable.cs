using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SkinUnlockerInteractable : GenericInteractable
    {
        [SerializeField] private string skinToUnlock;

        public override void OnInteract(GameObject interactionFrom, GameObject interactionTo)
        {
            //TODO 
            //        PlayerPrefs.SetInt(skinToUnlock, 1);  when the int reach 5  (according to the document) the skin is fully unlocked
            Debug.Log("Skin unlocked");
        }
    }
}


