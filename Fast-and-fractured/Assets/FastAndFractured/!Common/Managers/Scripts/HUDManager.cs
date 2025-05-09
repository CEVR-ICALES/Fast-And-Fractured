using Enums;
using UnityEngine;
using System.Collections.Generic;
using Utilities;
using UnityEngine.UI;

namespace FastAndFractured
{
    public class HUDManager : AbstractSingleton<HUDManager>
    {
        #region Public Fields

        public Dictionary<UIElementType, UIDynamicElement> UIElements => _uiElements;

        #endregion

        #region Private Fields

        private Dictionary<UIElementType, UIDynamicElement> _uiElements = new Dictionary<UIElementType, UIDynamicElement>();

        private Image[] _goodEffects;
        private Image[] _normalEffects;
        private Image[] _badEffects;
        private Image[] _bulletEffects;

        #endregion

        #region Unity Methods

        protected override void Awake()
        {
            base.Awake();
            RegisterUIElements();
        }

        private void Start()
        {

        }

        #endregion

        #region UI Elements Registration

        void RegisterUIElements()
        {
            foreach (UIDynamicElement element in FindObjectsOfType<UIDynamicElement>(true))
            {
                _uiElements[element.elementType] = element;
            }

            _goodEffects = GetUIElement(UIElementType.GOOD_EFFECTS).gameObject.GetComponentsInChildren<Image>(true);
            _normalEffects = GetUIElement(UIElementType.NORMAL_EFFECTS).gameObject.GetComponentsInChildren<Image>(true);
            _badEffects = GetUIElement(UIElementType.BAD_EFFECTS).gameObject.GetComponentsInChildren<Image>(true);
            _bulletEffects = GetUIElement(UIElementType.BULLET_EFFECT).gameObject.GetComponentsInChildren<Image>(true);
        }

        #endregion

        #region UI Modifications Methods

        /// <summary>
        /// Updates the specified UI element.
        /// <para>Accepts New Text, Sprite, or current and max value for Fill Amount.</para>
        /// </summary>
        public void UpdateUIElement(UIElementType type, string newText)
        {
            if (TryGetUIElement(type, out UIDynamicElement element) && element.textReference != null)
            {
                element.textReference.text = newText;
            }
        }

        public void UpdateUIElement(UIElementType type, Sprite newSprite)
        {
            if (TryGetUIElement(type, out UIDynamicElement element) && element.imageReference != null)
            {
                element.imageReference.sprite = newSprite;
            }
        }

        public void UpdateUIElement(UIElementType type, float currentValue, float maxValue)
        {
            if (TryGetUIElement(type, out UIDynamicElement element) && element.imageReference != null)
            {
                float fillAmount = Mathf.Clamp01(currentValue / maxValue);

                if (type == UIElementType.HEALTH_BAR)
                {
                    fillAmount = 1f - Mathf.Clamp01(currentValue / maxValue);
                    element.imageReference.color = Color.Lerp(Color.yellow, Color.red, fillAmount);
                }
                
                element.imageReference.fillAmount = fillAmount;
            }
        }

        public void UpdateUIElement(UIElementType type, bool isActive)
        {
            if (TryGetUIElement(type, out UIDynamicElement element))
            {
                element.gameObject.SetActive(isActive);
            }
        }

        public void UpdateUIEffect(UIElementType type, Sprite newSprite, float timeInscreen){
            GameObject effectGameObj = null;
            
            switch (type)
            {
                case UIElementType.GOOD_EFFECTS:
                    effectGameObj = UpdateEffectSprites(_goodEffects, newSprite);
                    break;
                case UIElementType.NORMAL_EFFECTS:
                    effectGameObj = UpdateEffectSprites(_normalEffects, newSprite);
                    break;
                case UIElementType.BAD_EFFECTS:
                    effectGameObj = UpdateEffectSprites(_badEffects, newSprite);
                    break;
                case UIElementType.BULLET_EFFECT:
                    effectGameObj = UpdateEffectSprites(_bulletEffects, newSprite);
                    GameObject bulletContainer = GetUIElement(UIElementType.BULLET_EFFECT).gameObject;
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
            }

            if(timeInscreen > 0f)
            {
                GameObject newSpriteGameObject = effectGameObj;
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
        public UIDynamicElement GetUIElement(UIElementType type)
        {
            if (TryGetUIElement(type, out UIDynamicElement element))
            {
                return element;
            }
            else return null;
        }

        public GameObject GetEffectGameObject(Sprite sprite)
        {
            Debug.Log($"GetEffectGameObject: {sprite.name}");
            GameObject hudImage = FindEffectGameObject(_goodEffects, sprite);
            if (hudImage != null) return hudImage;

            hudImage = FindEffectGameObject(_normalEffects, sprite);
            if (hudImage != null) return hudImage;

            hudImage = FindEffectGameObject(_badEffects, sprite);
            return hudImage;
        }

        #endregion

        #region Helpers

        private bool TryGetUIElement(UIElementType type, out UIDynamicElement element)
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
