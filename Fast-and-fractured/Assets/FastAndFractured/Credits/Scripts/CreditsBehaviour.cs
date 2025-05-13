using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.SimpleLocalization.Scripts;
namespace FastAndFractured
{
    public class CreditsBehaviour : MonoBehaviour
    {
        [SerializeField] private CreditsListData creditsListDataProgrammer;
        [SerializeField] private CreditsListData creditsListDataArtist;
        [SerializeField] private GameObject namePrefab;
        [SerializeField] private GameObject titlePrefab;
        [SerializeField] private GameObject nameContainerPrefab;
        [SerializeField] private GameObject creditsListContainer;
        [SerializeField] private GameObject creditsLeftContainer;
        [SerializeField] private GameObject creditsRightContainer;
        [SerializeField] private CanvasGroup creditsCanvasGroup;
        [SerializeField] private float scrollSpeed = 50f;
        [SerializeField] private float maxScrollSpeed = 200f;
        private float startYPosition;
        private bool _fastMode = false;
        
        void Start()
        {
            InsertCreditsData();
            startYPosition = creditsListContainer.transform.position.y;
        }
        void OnDisable()
        {
            _fastMode = false;
            creditsListContainer.transform.position = new Vector3(creditsListContainer.transform.position.x, startYPosition, creditsListContainer.transform.position.z);
        }
        private void InsertCreditsData()
        {
            foreach (var entry in creditsListDataProgrammer.creditsEntry)
            {
                GameObject titleObject = Instantiate(titlePrefab, creditsLeftContainer.transform);
                titleObject.transform.localScale = new Vector3(entry.sizeMultiplier, entry.sizeMultiplier, entry.sizeMultiplier);
                titleObject.GetComponent<LocalizedText>().LocalizationKey = entry.titleLocalizationKey;

                GameObject nameContainer = Instantiate(nameContainerPrefab, creditsLeftContainer.transform);

                foreach (var nameWithSize in entry.names)
                {
                    GameObject nameObject = Instantiate(namePrefab, nameContainer.transform);
                    nameObject.transform.localScale = new Vector3(nameWithSize.sizeMultiplier, nameWithSize.sizeMultiplier, nameWithSize.sizeMultiplier);
                    nameObject.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = nameWithSize.name;
                }
            }

            foreach (var entry in creditsListDataArtist.creditsEntry)
            {
                GameObject titleObject = Instantiate(titlePrefab, creditsRightContainer.transform);
                titleObject.transform.localScale = new Vector3(entry.sizeMultiplier, entry.sizeMultiplier, entry.sizeMultiplier);
                titleObject.GetComponent<LocalizedText>().LocalizationKey = entry.titleLocalizationKey;

                GameObject nameContainer = Instantiate(nameContainerPrefab, creditsRightContainer.transform);

                foreach (var nameWithSize in entry.names)
                {
                    GameObject nameObject = Instantiate(namePrefab, nameContainer.transform);
                    nameObject.transform.localScale = new Vector3(nameWithSize.sizeMultiplier, nameWithSize.sizeMultiplier, nameWithSize.sizeMultiplier);
                    nameObject.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = nameWithSize.name;
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
