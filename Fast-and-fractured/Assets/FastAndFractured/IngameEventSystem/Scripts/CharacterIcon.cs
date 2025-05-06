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

        [SerializeField] private Image _characterIconImg;
        [SerializeField] private GameObject _characterIconDeadEffect;
        [SerializeField] private GameObject _characterIconUltEffect;

        void Awake()
        {
            _characterIconImg = GetComponent<Image>();
            _characterIconDeadEffect = transform.GetChild(0).gameObject;
        }

        public void SetCharacterIcon(GameObject characterReceived, string characterIconName)
        {
            _character = characterReceived;
            if(_character != null)
            {
                _characterIconImg.sprite = ResourcesManager.Instance.GetResourcesSprite(characterIconName);
            }
        }

        public void SetPlayerDeadIconIsActive(bool isActive)
        {
            if(_characterIconDeadEffect != null && _character != null)
            {
                _characterIconDeadEffect.SetActive(isActive);
            }
        }

        public void SetPlayerUltIconIsActive(bool isActive, float timeInScreen)
        {
            if(_characterIconUltEffect != null && _character != null)
            {
                _characterIconUltEffect.SetActive(isActive);
            }

            if (timeInScreen > 0f)
            {
                TimerSystem.Instance.CreateTimer(timeInScreen, onTimerDecreaseComplete: () =>
                {
                    if(_characterIconUltEffect != null && _character != null)
                    {
                        _characterIconUltEffect.SetActive(!isActive);
                    }
                });
            }
        }
    }
}