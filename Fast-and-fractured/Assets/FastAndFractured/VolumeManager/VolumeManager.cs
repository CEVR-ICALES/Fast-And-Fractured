using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Utilities;

public class VolumeManager : AbstractSingleton<VolumeManager>
{
    [SerializeField] private List<GameObject> volumeList;
    [SerializeField] private bool randomStart =true;
    [SerializeField] private GameObject currentVolumeObject;

    public List<GameObject> VolumeList { get => volumeList; set => volumeList = value; }
    public GameObject CurrentVolumeGameObject  => currentVolumeObject ;
    public Volume CurrentVolumeComponent => currentVolumeObject.GetComponentInChildren<Volume>();    

    protected override void Awake()
    {
        if (volumeList.Count == 0) return;
        ChangeCurrentVolume(volumeList.First());
        if (randomStart)
        {
            int selected =  Random.Range(0, volumeList.Count);
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
