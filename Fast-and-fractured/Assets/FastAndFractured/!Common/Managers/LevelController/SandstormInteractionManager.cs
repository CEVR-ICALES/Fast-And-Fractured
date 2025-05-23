using Enums;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SandstormInteractionManager
    {
        private readonly SandstormController _sandStormController;
        private readonly List<GameObject> _safeZones;
        private readonly bool _isStormTimerActive;  

        public SandstormInteractionManager(SandstormController sandstormController, List<GameObject> safeZones, bool isStormTimerActiveInitially)
        {
            _sandStormController = sandstormController;
            _safeZones = safeZones ?? new List<GameObject>();
            _isStormTimerActive = !isStormTimerActiveInitially; 
        }
        
        

        public bool IsInsideSandstorm(GameObject target, float marginError = 0f)
        {
            if (_sandStormController == null) return false; 
            return _sandStormController.IsInsideStormCollider(target, marginError);
        }

        public List<GameObject> GetSafeZonesOutsideSandstorm(bool isStormCurrentlyActive)
        {
            var safeZonesOutside = new List<GameObject>();
            if (!isStormCurrentlyActive || _sandStormController == null) return safeZonesOutside;  

            foreach (var safeZone in _safeZones)
            {
                if (safeZone != null && !IsInsideSandstorm(safeZone))
                {
                    safeZonesOutside.Add(safeZone);
                }
            }
            return safeZonesOutside;
        }

        public bool AreAllGameElementsInsideSandstorm(GameElement gameElement, List<GameObject> gameCharacters, bool isStormCurrentlyActive)
        {
            if (!isStormCurrentlyActive || _sandStormController == null) return false;

            List<GameObject> elementsToCheck = new List<GameObject>();
            List<GameObject> elementsInsideStormList = null;

            if (gameElement == GameElement.CHARACTER)
            {
                elementsToCheck = gameCharacters;  
                elementsInsideStormList = _sandStormController.CharactersInsideSandstorm; 
            }
            else if (gameElement == GameElement.INTERACTABLE)
            {
                if (InteractableHandler.Instance != null) 
                {
                    foreach (var item in InteractableHandler.Instance.GetStatBoostItems())
                    {
                        elementsToCheck.Add(item.gameObject);
                    }
                }
                elementsInsideStormList = _sandStormController.ItemsInsideSandstorm;  
            }
            else if (gameElement == GameElement.SAFE_ZONES)
            {
                elementsToCheck = _safeZones;
                elementsInsideStormList = _sandStormController.SafeZonesInsideSandstorm;  
            }
            
            if (elementsToCheck.Count == 0) return false;  
            return LevelUtilities.CheckIfList1ElementsAreInList2(elementsToCheck, elementsInsideStormList);
        }
    }
}