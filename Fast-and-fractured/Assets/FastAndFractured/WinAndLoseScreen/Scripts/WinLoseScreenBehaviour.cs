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
        [SerializeField] private GameObject container;
        private GameObject _player;
        private string _playerName;
        private GameObject objectToSpawn;
        public GameObject spawnPoint;

        void OnEnable()
        {
            _player = LevelController.Instance.playerReference;
            _playerName = LevelController.Instance.InGameCharactersNameCodes[0];
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if(LevelController.Instance.HasPlayerWon)
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = "Menu.Win";
                objectToSpawn = _player.GetComponent<StatsController>().GetWinObjectByString(_playerName);
            }
            else
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = "Menu.Lose";
                objectToSpawn = _player.GetComponent<StatsController>().GetLoseObjectByString(_playerName);
            }
            if (objectToSpawn != null)
            {
                GameObject spawnedObject = Instantiate(objectToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
            }
            SetFinalStats();
            _player.SetActive(false);
        }
        public void ShowMenu()
        {
            container.SetActive(true);
        }
        private void SetFinalStats()
        {
            totalDamageDealtText.GetComponent<TextMeshProUGUI>().text = _player.GetComponent<StatsController>().totalDamageDealt.ToString();
            totalDamageTakenText.GetComponent<TextMeshProUGUI>().text = _player.GetComponent<StatsController>().totalDamageTaken.ToString();
            float distance = _player.GetComponent<StatsController>().totalDistanceDriven;
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
