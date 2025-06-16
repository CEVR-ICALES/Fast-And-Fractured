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

        private const string CARME_NAME = "Carme";
        private const string JOSEFINO_NAME = "Josefino";
        private const string PEPE_NAME = "Pepe";
        private const string MARIA_ANTONIA_NAME = "Maria";
        [SerializeField] List<GameObject> visuals;

        public void ChangeVisual(string skinToUnlock)
        {
            DisableAllVisuals();
            _skinToUnlock = skinToUnlock;
  
            GameObject player = LevelControllerButBetter.Instance.LocalPlayer;
            if (player != null)
            {
                switch (player.name)
                {
                    case string name when name.Contains(JOSEFINO_NAME):
                        visuals[0].SetActive(true);
                        break;
                    case string name when name.Contains(PEPE_NAME):
                        visuals[1].SetActive(true);
                        break;

                    case string name when name.Contains(MARIA_ANTONIA_NAME):
                        visuals[2].SetActive(true);
                        break;

                    case string name when name.Contains(CARME_NAME):
                        visuals[3].SetActive(true);
                        break;
                    default:
                        visuals[0].SetActive(true);
                        break;
                }
            }
        }

        private void DisableAllVisuals()
        {
            foreach(GameObject obj in visuals)
            {
                obj.SetActive(false);
            }
        }
        public override void OnInteract(GameObject interactionFrom, GameObject interactionTo)
        {
            if(interactionFrom.TryGetComponent(out StatsController statsController))
            {
                if(statsController.IsPlayer)
                {
                    PlayerPrefs.SetInt(_skinToUnlock, PlayerPrefs.GetInt(_skinToUnlock) + 1);
                    InteractableHandler.Instance.DestroySkinInteractable(gameObject);
                    IngameEventsManager.Instance.CreateEvent("Events.SkinUnlock", 2f);
                    //If the int of player prefs is 5, skin is unlocked
                }
            }
        }

    }
}


