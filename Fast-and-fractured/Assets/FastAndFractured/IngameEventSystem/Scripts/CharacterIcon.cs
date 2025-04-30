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
        public GameObject Character { get=> _character; }
        private GameObject _character;
        public void SetCharacterIcon(GameObject characterReceived, string characterIconName)
        {
            _character = characterReceived;
            if(_character != null)
            {
                GetComponent<Image>().sprite = ResourcesManager.Instance.GetResourcesSprite(characterIconName);
            }
        }
    }
}