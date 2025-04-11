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

        #endregion

        #region Unity Methods

        private void Start()
        {
            // Register all UI elements
            RegisterUIElements();
        }

        #endregion

        #region UI Elements Registration

        void RegisterUIElements()
        {
            foreach (UIDynamicElement element in FindObjectsOfType<UIDynamicElement>(true))
            {
                _uiElements[element.elementType] = element;
            }

            _goodEffects = GetUIElement(UIElementType.GOOD_EFFECTS).gameObject.GetComponentsInChildren<Image>();
            _normalEffects = GetUIElement(UIElementType.NORMAL_EFFECTS).gameObject.GetComponentsInChildren<Image>();
            _badEffects = GetUIElement(UIElementType.BAD_EFFECTS).gameObject.GetComponentsInChildren<Image>();
        }

        private bool TryGetUIElement(UIElementType type, out UIDynamicElement element)
        {
            if (!_uiElements.TryGetValue(type, out element))
            {
                Debug.LogWarning($"UI Element not found: {type}");
                return false;
            }
            return true;
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
                element.imageReference.fillAmount = fillAmount;

                if (type == UIElementType.HEALTH_BAR)
                {
                    element.imageReference.color = Color.Lerp(Color.red, Color.green, fillAmount);
                }
            }
        }

        public void UpdateUIElement(UIElementType type, bool isActive)
        {
            if (TryGetUIElement(type, out UIDynamicElement element))
            {
                element.gameObject.SetActive(isActive);
            }
        }

        public UIDynamicElement GetUIElement(UIElementType type)
        {
            if (TryGetUIElement(type, out UIDynamicElement element))
            {
                return element;
            }
            else return null;
        }

        #endregion
    }
}
