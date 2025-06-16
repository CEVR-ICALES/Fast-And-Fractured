using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Utilities;

public class VolumeManager : AbstractSingleton<VolumeManager>
{
    private const float FOG_VALUE = 500f;
    [SerializeField] private List<GameObject> volumeList;
    [SerializeField] private bool randomStart = true;
    [SerializeField] private GameObject currentVolumeObject;

    public List<GameObject> VolumeList { get => volumeList; set => volumeList = value; }
    public GameObject CurrentVolumeGameObject => currentVolumeObject;
    public Volume CurrentVolumeComponent => currentVolumeObject.GetComponentInChildren<Volume>();

    protected override void Construct()
    {
        base.Construct();
        if (volumeList.Count == 0) return;

        foreach (GameObject vol in volumeList)
        {
            vol.SetActive(false);
        }

        ChangeCurrentVolume(volumeList.First());
    }
    protected override void Initialize()
    {
        if (randomStart)
        {
            int selected = Random.Range(0, volumeList.Count);
            ChangeCurrentVolume(volumeList[selected]);
        }
    }

    private void ChangeCurrentVolume(GameObject newVolume)
    {
        if (CurrentVolumeGameObject)
        {
            CurrentVolumeGameObject.gameObject.SetActive(false);
        }
        currentVolumeObject = newVolume;
        currentVolumeObject?.SetActive(true);
        if (CurrentVolumeComponent.profile.TryGet<Fog>(out Fog fogComponent))
        {
             fogComponent.active = true;
        //    fogComponent.SetAllOverridesTo(true);
            fogComponent.enableVolumetricFog.overrideState = true;
            fogComponent.enableVolumetricFog.value = true;
            fogComponent.enableVolumetricFog.Override(true);
           // fogComponent.depthExtent.Override( FOG_VALUE);
            fogComponent.enableVolumetricFog.overrideState = true; // Enable the override for this parameter
            fogComponent.enableVolumetricFog.value = true;         // Set the value

        

            // If you want to ensure fog itself is "on" (though 'active = true' usually handles this for the override)
            fogComponent.enabled.overrideState = true; // This refers to the general on/off toggle for the fog effect
            fogComponent.enabled.value = true;
            fogComponent.Override(fogComponent, 0);

        }
    }
    public void ChangeCurrentVolume(int volumeIndex)
    {
        ChangeCurrentVolume(VolumeList.ElementAtOrDefault(volumeIndex));
    }
    public List<string> GetVolumeNames()
    {
        List<string> result = new List<string>();
        foreach (var volumeGameobject in VolumeList)
        {
            result.Add(volumeGameobject.gameObject.name);
        }
        return result;
    }
}
