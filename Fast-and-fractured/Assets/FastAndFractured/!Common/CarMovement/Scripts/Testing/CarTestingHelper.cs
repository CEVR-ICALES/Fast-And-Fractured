using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FastAndFractured;

public class CarTestingHelper : MonoBehaviour
{
    public GameObject window;
    public GameObject openButton;

    public Transform[] zones;
    public GameObject[] staticObjects;
    public GameObject[] carSimulationObjects;
    public Transform objectSpawnPoint;

    public Transform player;
    public Transform spawnObjectsParent;

    public TMP_Dropdown objectDropdown;
    public Slider massSlider;
    public TMP_InputField maxEnduranceInputField;
    public Slider enduranceSlider;
    public Button updateEnduranceButton;
    public TextMeshProUGUI massText;
    public TextMeshProUGUI enduranceText;

    private bool isWindowOpen = false;
    private float currentMaxEndurance = 0f;
    private List<GameObject> spawnedObjectsList = new List<GameObject>();

    private void Start()
    {
        InitializeDropdown();

        massSlider.minValue = 0;
        massSlider.maxValue = 4000;

        enduranceSlider.minValue = 0;
        enduranceSlider.maxValue = currentMaxEndurance;

        updateEnduranceButton.onClick.AddListener(UpdateEnduranceSlider);
    }

    private void Update()
    {
        massText.text = massSlider.value.ToString();
        enduranceText.text = enduranceSlider.value.ToString();  
    }

    private void InitializeDropdown()
    {
        objectDropdown.ClearOptions();

        foreach (GameObject obj in staticObjects)
        {
            objectDropdown.options.Add(new TMP_Dropdown.OptionData(obj.name));
        }

        foreach (GameObject obj in carSimulationObjects)
        {
            objectDropdown.options.Add(new TMP_Dropdown.OptionData(obj.name));
        }

        objectDropdown.RefreshShownValue();
    }

    public void SpawnObject()
    {
        string selectedObjectName = objectDropdown.options[objectDropdown.value].text;

        GameObject objectToSpawn = FindObjectByName(selectedObjectName);

        if (objectToSpawn == null)
        {
            Debug.LogWarning("Selected object not found!");
            return;
        }

        GameObject spawnedObject = Instantiate(
            objectToSpawn,
            objectSpawnPoint.position,
            objectSpawnPoint.rotation
        );

        spawnedObjectsList.Add(spawnedObject);

        ConfigureObjectAttributes(spawnedObject);

    }

    private GameObject FindObjectByName(string objectName)
    {
        foreach (GameObject obj in staticObjects)
        {
            if (obj.name == objectName)
            {
                return obj;
            }
        }

        foreach (GameObject obj in carSimulationObjects)
        {
            if (obj.name == objectName)
            {
                return obj;
            }
        }

        return null;
    }

    private void ConfigureObjectAttributes(GameObject spawnedObject)
    {
        Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Set mass from the slider
            rb.mass = massSlider.value;
        }

        // Check if the object is a PlayerOne (has PhysicsBehaviour component)
        PhysicsBehaviour physicsBehaviour = spawnedObject.GetComponent<PhysicsBehaviour>();
        if (physicsBehaviour != null)
        {
            //physicsBehaviour._maxEndurance = currentMaxEndurance;
            //physicsBehaviour._endurance = enduranceSlider.value;
          
        }
    }

    private void UpdateEnduranceSlider()
    {
        if (float.TryParse(maxEnduranceInputField.text, out float newMaxEndurance))
        {
            currentMaxEndurance = newMaxEndurance;
            enduranceSlider.maxValue = currentMaxEndurance;
        }
        else
        {
            Debug.LogWarning("Invalid max endurance input!");
        }
    }

    public void GoToZone(int zoneIndex)
    {
        player.position = zones[zoneIndex].position;
        player.rotation = zones[zoneIndex].rotation;

        player.gameObject.GetComponent<CarMovementController>().StopAllCarMovement();
        player.gameObject.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
    }

    public void WindowClicked()
    {
        if(!isWindowOpen)
        {
            openButton.SetActive(false);
            window.SetActive(true);
        } else
        {
            window.SetActive(false);
            openButton.SetActive(true);
        }

        isWindowOpen = !isWindowOpen;
    }
}
