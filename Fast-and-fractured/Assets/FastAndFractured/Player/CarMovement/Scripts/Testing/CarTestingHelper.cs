using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CarTestingHelper : MonoBehaviour
{
    public GameObject window;
    public GameObject openButton;

    public Transform[] zones;
    public GameObject[] staticObjects;
    public GameObject[] carSimulationObjects;
    public Transform pusheableSpawnPoint;
    public Transform carSpawnPoint;

    public Transform player;
    public Transform spawnObjectsParent;

    public Dropdown objectDropdown;
    public Slider massSlider;
    public InputField maxEnduranceInputField;
    public Slider enduranceInputField;
    public InputField enduranceFactorImporantceInputField;

    private int selectedObjectType;
    private int selectedObjectIndex;
    private bool isWindowOpen = false;
    

    public void GoToZone(int zoneIndex)
    {
        player.position = zones[zoneIndex].position;
        player.rotation = zones[zoneIndex].rotation;

        player.gameObject.GetComponent<CarMovementController>().StopAllCarMovement();
        player.gameObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
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

    public void SetObjectType(int type)
    {
        selectedObjectType = type;
    }

    public void SetObjectIndex(int index)
    {
        selectedObjectIndex = index;
    }

    public void SpawnObject()
    {

    }
}
