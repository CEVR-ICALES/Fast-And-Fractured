using Enums;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace FastAndFractured
{
    public class HUDManager : MonoBehaviour
    {
        #region Singleton

        public static HUDManager Instance { get; private set; }

        #endregion

        #region Private Fields

        private Dictionary<UIElementType, UIElement> _uiElements = new Dictionary<UIElementType, UIElement>();

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Register all UI elements
            RegisterUIElements();
        }

        private void OnEnable()
        {
            // Subscribe to events if necessary
            
            // Example
            // EventManager.OnHealthUpdate += UpdateHealthBar;
            // EventManager.OnCooldownUpdate += UpdateCooldown;
            // EventManager.OnTimerUpdate += UpdateTimer;
            // EventManager.OnEventTitleUpdate += UpdateEventTitle;
        }

        private void OnDisable()
        {
            // Unsubscribe from events if necessary
            
            // Example
            // EventManager.OnHealthUpdate -= UpdateHealthBar;
            // EventManager.OnCooldownUpdate -= UpdateCooldown;
            // EventManager.OnTimerUpdate -= UpdateTimer;
            // EventManager.OnEventTitleUpdate -= UpdateEventTitle;
        }

        #endregion

        #region Private Methods

        void RegisterUIElements()
        {
            foreach (UIElement element in FindObjectsOfType<UIElement>(true))
            {
                _uiElements[element.elementType] = element;
            }
        }

        private bool TryGetUIElement(UIElementType type, out UIElement element)
        {
            if (!_uiElements.TryGetValue(type, out element))
            {
                Debug.LogWarning($"UI Element not found: {type}");
                return false;
            }
            return true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the specified UI element.
        /// <para>Accepts New Text, Sprite, or current and max value for Fill Amount.</para>
        /// </summary>
        public void UpdateUIElement(UIElementType type, string newText)
        {
            if (TryGetUIElement(type, out UIElement element) && element.textReference != null)
            {
                element.textReference.text = newText;
            }
        }

        public void UpdateUIElement(UIElementType type, Sprite newSprite)
        {
            if (TryGetUIElement(type, out UIElement element) && element.imageReference != null)
            {
                element.imageReference.sprite = newSprite;
            }
        }

        public void UpdateUIElement(UIElementType type, float currentValue, float maxValue)
        {
            if (TryGetUIElement(type, out UIElement element) && element.imageReference != null)
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
            if (TryGetUIElement(type, out UIElement element))
            {
                element.gameObject.SetActive(isActive);
            }
        }

        public UIElement GetUIElement(UIElementType type)
        {
            if (TryGetUIElement(type, out UIElement element))
            {
                return element;
            }
            else return null;
        }

        #endregion

        #region Event Handlers

        // Placeholder methods for future event handling

        // private void UpdateHealthBar()
        // {
        //     UpdateUIImageFillAmount(UIElementType.HealthBar, Player.currentHealth, Player.maxHealth);
        // }

        // private void UpdateCooldown(UIElementType type, float currentCooldown, float maxCooldown)
        // {
        //     UpdateUIImageFillAmount(type, currentCooldown, maxCooldown);
        // }

        // private void UpdateTimer(float time)
        // {
        //     UpdateUITextString(UIElementType.TimerText, time.ToString("F2"));
        // }

        // private void UpdateEventTitle(string title)
        // {
        //     UpdateUITextString(UIElementType.EventText, title);
        // }

        #endregion
    }
}
