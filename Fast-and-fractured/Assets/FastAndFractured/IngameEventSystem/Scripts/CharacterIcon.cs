using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities;
using Enums;
namespace FastAndFractured
{
    public class CharacterIcon : MonoBehaviour
    {
        private GameObject _character;
        private bool _isCharacterReceived = false;
        public void SetCharacterIcon(GameObject characterReceived, string characterIconName)
        {
            _character = characterReceived;
            if(_character != null)
            {
                GetComponent<Image>().sprite = ResourcesManager.Instance.GetResourcesSprite(characterIconName);
                _isCharacterReceived = true;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if(_isCharacterReceived)
            {
                
            }
        }
    }
}