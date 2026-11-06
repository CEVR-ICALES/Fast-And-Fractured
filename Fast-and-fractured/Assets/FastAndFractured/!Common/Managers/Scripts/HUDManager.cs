using Enums;
using UnityEngine;
using System.Collections.Generic;
using Utilities;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace FastAndFractured
{
    public class HUDManager : AbstractSingleton<HUDManager>
    {
        #region Public Fields

        public Dictionary<UIDynamicElementType, UIDynamicElement> UIElements => _uiElements;

        #endregion

        #region Private Fields

        private Dictionary<UIDynamicElementType, UIDynamicElement> _uiElements = new Dictionary<UIDynamicElementType, UIDynamicElement>();

        private Image[] _goodEffects;
        private Image[] _normalEffects;
        private Image[] _badEffects;
        private Image[] _bulletEffects;
        private Image[] _effectIcons;

        private string selectedPlayer;
        private string selectedPlayerHalfBody;
        private string selectedPlayerUA;
        private string selectedPlayerPush;
        private string selectedPlayerShoot;

        #endregion

        #region Unity Methods

        protected override void Construct()
        {
            base.Construct();
            RegisterUIElements();
            if (LevelController.HasInstance)
            {
                LevelController.Instance.onLevelPreStart.AddListener(RegisterSelectedPlayer);
            }
            RegisterSelectedPlayer();
        }
        protected override void Initialize()
        {
            base.Initialize();
            
        }

        void RegisterSelectedPlayer()
        {
            selectedPlayer = PlayerPrefs.GetString("Selected_Player");
            if (string.IsNullOrEmpty(selectedPlayer))
            {
                return;
            }
            selectedPlayerHalfBody = selectedPlayer + "_HALFBODY";
            string splittedPlayer = selectedPlayer.Split('_')[0];
            selectedPlayerUA = splittedPlayer + "_UA";
            selectedPlayerPush = splittedPlayer + "_PUSH";
            selectedPlayerShoot = splittedPlayer + "_SHOOT";
            SetPlayerStartingSprites();

        }

        #endregion

        #region UI Elements Registration and Set methods

        void RegisterUIElements()
        {
            foreach (UIDynamicElement element in GetComponentsInChildren<UIDynamicElement>(true))
            {
                _uiElements[element.elementType] = element;
            }
            
            var goodEffectsElement = GetUIElement(UIDynamicElementType.GOOD_EFFECTS);
            _goodEffects = goodEffectsElement != null ? goodEffectsElement.gameObject.GetComponentsInChildren<Image>(true) : new Image[0];
            
            var normalEffectsElement = GetUIElement(UIDynamicElementType.NORMAL_EFFECTS);
            _normalEffects = normalEffectsElement != null ? normalEffectsElement.gameObject.GetComponentsInChildren<Image>(true) : new Image[0];
            
            var badEffectsElement = GetUIElement(UIDynamicElementType.BAD_EFFECTS);
            _badEffects = badEffectsElement != null ? badEffectsElement.gameObject.GetComponentsInChildren<Image>(true) : new Image[0];
            
            var effectIconsElement = GetUIElement(UIDynamicElementType.EFFECT_ICONS_CONTAINER);
            _effectIcons = effectIconsElement != null ? effectIconsElement.gameObject.GetComponentsInChildren<Image>(true) : new Image[0];
            
            var bulletEffectElement = GetUIElement(UIDynamicElementType.BULLET_EFFECT);
            _bulletEffects = bulletEffectElement != null ? bulletEffectElement.gameObject.GetComponentsInChildren<Image>(true) : new Image[0];
		}

        void SetPlayerStartingSprites()
        {
            if (!ResourcesManager.HasInstance) return;
            UpdateUIElement(UIDynamicElementType.SHOOT_ICON, ResourcesManager.Instance.GetResourcesSprite(selectedPlayerShoot));
            UpdateUIElement(UIDynamicElementType.PUSH_ICON, ResourcesManager.Instance.GetResourcesSprite(selectedPlayerPush));
            UpdateUIElement(UIDynamicElementType.ULT_ICON, ResourcesManager.Instance.GetResourcesSprite(selectedPlayerUA));
            UpdateUIElement(UIDynamicElementType.SELECTED_PLAYER_ICON, ResourcesManager.Instance.GetResourcesSprite(selectedPlayerHalfBody));
        }

        #endregion

        #region UI Modifications Methods

        /// <summary>
        /// Updates the specified UI element.
        /// <para>Accepts New Text, Sprite, or current and max value for Fill Amount.</para>
        /// </summary>
        public void UpdateUIElement(UIDynamicElementType type, string newText)
        {
            if (TryGetUIElement(type, out UIDynamicElement element) && element.textReference != null)
            {
                element.textReference.text = newText;
            }
        }

        public void UpdateUIElement(UIDynamicElementType type, Sprite newSprite)
        {
            if (TryGetUIElement(type, out UIDynamicElement element) && element.imageReference != null)
            {
                element.imageReference.sprite = newSprite;
            }
        }

        public void UpdateUIElement(UIDynamicElementType type, float currentValue, float maxValue)
        {
            if (TryGetUIElement(type, out UIDynamicElement element) && element.imageReference != null)
            {
                float fillAmount = Mathf.Clamp01(currentValue / maxValue);

                if (type == UIDynamicElementType.HEALTH_BAR)
                {
                    fillAmount = 1f - Mathf.Clamp01(currentValue / maxValue);
                    element.imageReference.color = Color.Lerp(Color.yellow, Color.red, fillAmount);
                }
                
                element.imageReference.fillAmount = fillAmount;
            }
        }

        public void UpdateUIElement(UIDynamicElementType type, bool isActive)
        {
            if (TryGetUIElement(type, out UIDynamicElement element))
            {
                element.gameObject.SetActive(isActive);
            }
        }

        /// <summary>
        /// Updates the specified UI effect with the given sprite.
        /// <para>If timeInscreen is greater than 0, the effect will fade out after the given time.</para>
        /// </summary>
        public void UpdateUIEffect(UIDynamicElementType type, Sprite newSprite, float timeInscreen){
            GameObject effectGameObj = null;
            
            switch (type)
            {
                case UIDynamicElementType.GOOD_EFFECTS:
                    effectGameObj = UpdateEffectSprites(_goodEffects, newSprite);
                    break;
                case UIDynamicElementType.NORMAL_EFFECTS:
                    effectGameObj = UpdateEffectSprites(_normalEffects, newSprite);
                    break;
                case UIDynamicElementType.BAD_EFFECTS:
                    effectGameObj = UpdateEffectSprites(_badEffects, newSprite);
                    break;
                case UIDynamicElementType.BULLET_EFFECT:
                    effectGameObj = UpdateEffectSprites(_bulletEffects, newSprite);
                    var bulletElement = GetUIElement(UIDynamicElementType.BULLET_EFFECT);
                    GameObject bulletContainer = bulletElement != null ? bulletElement.gameObject : null;
                    if (bulletContainer != null && effectGameObj != null)
                    {
                        RectTransform containerRect = bulletContainer.GetComponent<RectTransform>();
                        RectTransform effectRect = effectGameObj.GetComponent<RectTransform>();

                        if (containerRect != null && effectRect != null)
                        {
                            float halfWidth = containerRect.rect.width / 2f;
                            float halfHeight = containerRect.rect.height / 2f;
                            float randomX = Random.Range(-halfWidth, halfWidth);
                            float randomY = Random.Range(-halfHeight, halfHeight);
                            effectRect.anchoredPosition = new Vector2(randomX, randomY);
                        }
                    }
                    break;
                case UIDynamicElementType.EFFECT_ICONS_CONTAINER:
                    effectGameObj = UpdateEffectSprites(_effectIcons, newSprite);
                    break;
            }

            if(timeInscreen > 0f)
            {
                GameObject newSpriteGameObject = effectGameObj;
                if (newSpriteGameObject == null || !TimerSystem.HasInstance)
                {
                    return;
                }
                newSpriteGameObject.SetActive(true);
                TimerSystem.Instance.CreateTimer(timeInscreen, onTimerDecreaseComplete: () =>
                {
                    FadeOutEffectSprites(newSpriteGameObject.GetComponent<Image>(), 0.3f);
                });
            }
        }

        /// <summary>
        /// Returns the UI element of the specified type.
        /// </summary>
        public UIDynamicElement GetUIElement(UIDynamicElementType type)
        {
            if (TryGetUIElement(type, out UIDynamicElement element))
            {
                return element;
            }
            else return null;
        }

        public GameObject GetEffectGameObject(Sprite sprite)
        {
            GameObject hudImage = FindEffectGameObject(_goodEffects, sprite);
            if (hudImage != null) return hudImage;

            hudImage = FindEffectGameObject(_normalEffects, sprite);
            if (hudImage != null) return hudImage;

            hudImage = FindEffectGameObject(_badEffects, sprite);
            if (hudImage != null) return hudImage;

            hudImage = FindEffectGameObject(_effectIcons, sprite);
            return hudImage;
        }

        #endregion

        #region Helpers

        private bool TryGetUIElement(UIDynamicElementType type, out UIDynamicElement element)
        {
            if (!_uiElements.TryGetValue(type, out element))
            {
                Debug.LogWarning($"UI Element not found: {type}");
                return false;
            }
            return true;
        }

        private GameObject UpdateEffectSprites(Image[] effects, Sprite newSprite)
        {
            GameObject effectGameObject = null;
            foreach (Image image in effects)
            {
                if (!image.gameObject.activeSelf)
                {
                    image.sprite = newSprite;
                    effectGameObject = image.gameObject;
                }
            }

            return effectGameObject;
        }

        private GameObject FindEffectGameObject(Image[] effects, Sprite sprite)
        {
            foreach (Image image in effects)
            {
                if (sprite == image.sprite)
                {
                    return image.gameObject;
                }
            }
            return null;
        }

        private void FadeOutEffectSprites(Image hudEffect, float fadeOutTime)
        {
            if (hudEffect != null)
            {
                hudEffect.CrossFadeAlpha(0f, fadeOutTime, true);
                TimerSystem.Instance.CreateTimer(fadeOutTime, onTimerDecreaseComplete: () =>
                {
                    hudEffect.gameObject.SetActive(false);
                    hudEffect.CrossFadeAlpha(1f, 0f, true);
                });
            }
        }
        #endregion

    }
}
