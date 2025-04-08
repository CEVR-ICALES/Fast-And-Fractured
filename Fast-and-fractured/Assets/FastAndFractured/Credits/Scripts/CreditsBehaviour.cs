using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    public class CreditsBehaviour : MonoBehaviour
    {
        [SerializeField] private CreditsListData creditsListData;
        [SerializeField] private GameObject namePrefab;
        [SerializeField] private GameObject titlePrefab;
        [SerializeField] private GameObject nameContainerPrefab;
        [SerializeField] private GameObject creditsListContainer;
        [SerializeField] private float scrollSpeed = 50f;
        
        void Start()
        {
            UpdateCredits();
        }
        private void UpdateCredits()
        {
            foreach (var entry in creditsListData.creditsEntry)
            {
                GameObject titleObject = Instantiate(titlePrefab, creditsListContainer.transform);
                titleObject.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = entry.title;

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
            
        }
    }
}
