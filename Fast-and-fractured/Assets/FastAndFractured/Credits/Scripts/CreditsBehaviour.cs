using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.SimpleLocalization.Scripts;
namespace FastAndFractured
{
    public class CreditsBehaviour : MonoBehaviour
    {
        [SerializeField] private CreditsListData creditsListData;
        [SerializeField] private GameObject namePrefab;
        [SerializeField] private GameObject titlePrefab;
        [SerializeField] private GameObject nameContainerPrefab;
        [SerializeField] private GameObject creditsListContainer;
        [SerializeField] private CanvasGroup creditsCanvasGroup;
        [SerializeField] private float scrollSpeed = 50f;
        [SerializeField] private float maxScrollSpeed = 200f;
        private bool _fastMode = false;
        
        void Start()
        {
            InsertCreditsData();
        }
        void OnDisable()
        {
            _fastMode = false;
            creditsListContainer.transform.position = new Vector3(creditsListContainer.transform.position.x, 0, creditsListContainer.transform.position.z);
        }
        private void InsertCreditsData()
        {
            foreach (var entry in creditsListData.creditsEntry)
            {
                GameObject titleObject = Instantiate(titlePrefab, creditsListContainer.transform);
                titleObject.transform.localScale = new Vector3(entry.sizeMultiplier, entry.sizeMultiplier, entry.sizeMultiplier);
                titleObject.GetComponent<LocalizedText>().LocalizationKey = entry.titleLocalizationKey;

                GameObject nameContainer = Instantiate(nameContainerPrefab, creditsListContainer.transform);

                foreach (var name in entry.names)
                {
                    GameObject nameObject = Instantiate(namePrefab, nameContainer.transform);
                    nameObject.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = name;
                }
            }
        }
        void Update()
        {
            if(creditsCanvasGroup.alpha==1f)
            {
                MoveCredits();
            }
        }
        private void MoveCredits()
        {
            float scrollAmount;
            float containerHeight = creditsListContainer.GetComponent<RectTransform>().rect.height;
            float canvasHeight = creditsCanvasGroup.GetComponent<RectTransform>().rect.height;
            float currentY = creditsListContainer.transform.localPosition.y;
            if (currentY >= containerHeight - canvasHeight)
            {
                return;
            }
            if (Input.anyKeyDown)
            {
                _fastMode = !_fastMode;
            }
            if(_fastMode){
                scrollAmount = maxScrollSpeed * Time.deltaTime;   
            }
            else
            {
                scrollAmount = scrollSpeed * Time.deltaTime;
            }
            creditsListContainer.transform.position += new Vector3(0, scrollAmount, 0);
        }
    }
}
