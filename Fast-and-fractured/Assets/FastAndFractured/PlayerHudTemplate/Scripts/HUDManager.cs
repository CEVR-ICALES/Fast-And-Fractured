using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace Game
{
    public enum UIElementType
    {
        HealthBar,
        DashCooldown,
        UltCooldown,
        PushCooldown,
        ShootCooldown,
        EventText,
        TimerText,
        DashIcon,
        UltIcon,
        PushIcon,
        ShootIcon,
        DashBinding,
        UltBinding,
        PushBinding,
        ShootBinding,
        Player0,
        Player1,
        Player2,
        Player3,
        Player4,
        Player5,
        Player6,
        Player7,
        BadEffects,
        Effects,
        GoodEffects
    }

    public class HUDManager : MonoBehaviour
    {
        #region Singleton

        public static HUDManager Instance { get; private set; }

        #endregion

        #region Private Fields

        private Dictionary<UIElementType, Image> _images = new Dictionary<UIElementType, Image>();
        private Dictionary<UIElementType, TextMeshProUGUI> _texts = new Dictionary<UIElementType, TextMeshProUGUI>();

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
            UIElement[] uiElements = FindObjectsOfType<UIElement>();

            foreach (UIElement element in uiElements)
            {
                if (element.imageReference != null)
                    _images[element.elementType] = element.imageReference;

                if (element.textReference != null)
                    _texts[element.elementType] = element.textReference;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the fill amount of an Image UI element.
        /// </summary>
        /// <param name="type">The type of the UI element.</param>
        /// <param name="currentValue">The fill amount value.</param>
        /// <param name="maxValue">The maximum value for the fill amount.</param>
        public void UpdateUIImageFillAmount(UIElementType type, float currentValue, float maxValue)
        {
            if (_images.TryGetValue(type, out Image img))
            {
                float fillAmount = Mathf.Clamp01(currentValue / maxValue);
                img.fillAmount = fillAmount;

                if (type == UIElementType.HealthBar)
                {
                    img.color = Color.Lerp(Color.red, Color.green, fillAmount);
                }
            }
        }

        /// <summary>
        /// Updates the sprite of an Image UI element.
        /// </summary>
        /// <param name="type">The type of the UI element.</param>
        /// <param name="sprite">The new sprite.</param>
        public void UpdateUIImageSprite(UIElementType type, Sprite sprite)
        {
            if (_images.TryGetValue(type, out Image img))
            {
                img.sprite = sprite;
            }
        }

        /// <summary>
        /// Updates the text content of a Text UI element.
        /// </summary>
        /// <param name="type">The type of the UI element.</param>
        /// <param name="content">The new text content.</param>
        public void UpdateUITextString(UIElementType type, string content)
        {
            if (_texts.TryGetValue(type, out TextMeshProUGUI txt))
            {
                txt.text = content;
            }
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
