using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine.UI;
using Utilities;
namespace FastAndFractured
{
    public class WinLoseScreenBehaviour : MonoBehaviour
    {
        [SerializeField] private GameObject resultText;
        [SerializeField] private GameObject totalDamageDealtText;
        [SerializeField] private GameObject totalDamageTakenText;
        [SerializeField] private GameObject totalDistanceText;

        private GameObject _player;

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if(LevelController.Instance.hasPlayerWinned)
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = "Menu.Win";
            }
            else
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = "Menu.Lose";
            }
            _player = LevelController.Instance.playerReference;
            SetFinalStats();
        }
        private void SetFinalStats()
        {
            totalDamageDealtText.GetComponent<TextMeshProUGUI>().text = _player.GetComponent<StatsController>().totalDamageDealt.ToString();
            totalDamageTakenText.GetComponent<TextMeshProUGUI>().text = _player.GetComponent<StatsController>().totalDamageTaken.ToString();
            float distance = _player.GetComponent<StatsController>().totalDistanceDriven;
            //quiero que si es menor de 1000 se redondee a 0 decimales y en metros y sino que se redondee a 1 decimal y en km
            if (distance < 1000)
            {
                distance = Mathf.Round(distance);
                totalDistanceText.GetComponent<TextMeshProUGUI>().text = distance.ToString() + " m";
            }
            else
            {
                distance = Mathf.Round(distance / 1000f * 10) / 10;
                totalDistanceText.GetComponent<TextMeshProUGUI>().text = distance.ToString() + " km";
            }
        }
    }
}
