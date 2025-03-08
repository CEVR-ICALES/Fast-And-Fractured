using UnityEngine;
using UnityEngine.UI;
using System;

namespace Game
{
    public class HUDController : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] private Image healthBar;

        [Header("Abilities")]
        [SerializeField] private Image dashIcon;
        [SerializeField] private Image ultIcon;
        [SerializeField] private Image pushIcon;
        [SerializeField] private Image shootIcon;

        [Header("Abilities Cooldowns")]
        [SerializeField] private Image dashCooldownImage;
        [SerializeField] private Image ultCooldownImage;
        [SerializeField] private Image pushCooldownImage;
        [SerializeField] private Image shootCooldownImage;

        [Header("Abilities Bindings")]
        [SerializeField] private Image dashBindingImage;
        [SerializeField] private Image ultBindingImage;
        [SerializeField] private Image pushBindingImage;
        [SerializeField] private Image shootBindingImage;

        [Header("Players")]
        [SerializeField] private Image player1Image;
        [SerializeField] private Image player2Image;
        [SerializeField] private Image player3Image;
        [SerializeField] private Image player4Image;
        [SerializeField] private Image player5Image;
        [SerializeField] private Image player6Image;
        [SerializeField] private Image player7Image;
        [SerializeField] private Image player8Image;

        [Header("Texts")]
        [SerializeField] private Text eventText;
        [SerializeField] private Text timerText;

        [Header("Screen Effects")]
        [SerializeField] private Image badEffectsImage;
        [SerializeField] private Image effectsImage;
        [SerializeField] private Image goodEffectsImage;

        void CustomStart()
        {
            // Custom initialization logic
        }

        private void Start()
        {
            // Subscribe to events
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
        }

        #region Health Management

        public void UpdateHealth(float currentHealth, float maxHealth)
        {
            healthBar.fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
        }

        #endregion

        #region Abilities
        public void SetDashIcon(Sprite icon)
        {
            dashIcon.sprite = icon;
        }
        
        public void SetUltIcon(Sprite icon)
        {
            ultIcon.sprite = icon;
        }

        public void SetPushIcon(Sprite icon)
        {
            pushIcon.sprite = icon;
        }

        public void SetShootIcon(Sprite icon)
        {
            shootIcon.sprite = icon;
        }
        
        #endregion

        #region Abilities Cooldowns

        public void UpdateDashCooldown(float currentTime, float maxTime)
        {
            dashCooldownImage.fillAmount = Mathf.Clamp01(currentTime / maxTime);
        }

        public void UpdateUltCooldown(float currentTime, float maxTime)
        {
            ultCooldownImage.fillAmount = Mathf.Clamp01(currentTime / maxTime);
        }

        public void UpdatePushAttackCooldown(float currentTime, float maxTime)
        {
            pushCooldownImage.fillAmount = Mathf.Clamp01(currentTime / maxTime);
        }

        public void UpdateShootAttackCooldown(float currentTime, float maxTime)
        {
            shootCooldownImage.fillAmount = Mathf.Clamp01(currentTime / maxTime);
        }

        #endregion

        #region Abilities Bindings

        public void SetDashBinding(Sprite icon)
        {
            dashBindingImage.sprite = icon;
        }

        public void SetUltBinding(Sprite icon)
        {
            ultBindingImage.sprite = icon;
        }

        public void SetPushBinding(Sprite icon)
        {
            pushBindingImage.sprite = icon;
        }

        public void SetShootBinding(Sprite icon)
        {
            shootBindingImage.sprite = icon;
        }

        #endregion

        #region Player Images

        public void SetPlayerImage(int playerIndex, Sprite playerSprite)
        {
            switch (playerIndex)
            {
                case 1: player1Image.sprite = playerSprite; break;
                case 2: player2Image.sprite = playerSprite; break;
                case 3: player3Image.sprite = playerSprite; break;
                case 4: player4Image.sprite = playerSprite; break;
                case 5: player5Image.sprite = playerSprite; break;
                case 6: player6Image.sprite = playerSprite; break;
                case 7: player7Image.sprite = playerSprite; break;
                case 8: player8Image.sprite = playerSprite; break;
                default: Debug.LogWarning("Invalid player index!!"); break;
            }
        }

        #endregion

        #region Text Management

        public void UpdateEventText(string text)
        {
            eventText.text = text;
        }

        public void UpdateTimerText(string text)
        {
            timerText.text = text;
        }

        #endregion

        #region Screen Effects

        public void SetScreenBadEffect(Sprite icon)
        {
            badEffectsImage.sprite = icon;
        }

        public void SetScreenEffect(Sprite icon)
        {
            effectsImage.sprite = icon;
        }

        public void SetScreenGoodEffect(Sprite icon)
        {
            goodEffectsImage.sprite = icon;
        }

        #endregion
    }
}