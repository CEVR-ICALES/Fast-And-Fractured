using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class TrafficLightBehaviour : MonoBehaviour
{

    [Header("Prefab Settings")]
    [SerializeField] private MeshRenderer[] lightMeshRenderer;
    [SerializeField] private LightState[] lightState;

    [SerializeField] private int numberOfTrafficLights = 1;
    [SerializeField] private int numberOfLightsInTrafficLights = 1;
    
    [Header("EditableSettings")]
    [SerializeField] private BaseColorType baseColorType;
    [SerializeField] private Color LightEmissiveColor;
    [SerializeField] private Material greenMaterial;
    [SerializeField] private Material greyMaterial;
    [SerializeField] private bool invertLighting;
    [SerializeField] private bool lightingChanges;
    [SerializeField] private float onLightingTime;
    [SerializeField] private float offLightingTime;
 

    private void Start()
    {
        if (!lightingChanges) return;

        /*   TimerSystem.Instance.CreateTimer(lightingTime, onTimerDecreaseComplete: () =>
           {

           }
               );*/
    }
    public void WaitTime()
    {
 
    }

    public void UpdateLights()
    {
        lightMeshRenderer[TRAFFIC_LIGHT_BASE].material = baseColorType == BaseColorType.GREEN ? greenMaterial : greyMaterial;
        foreach (var light in lightState)
        {
            TrafficLighState newState = GetNextState(light.TrafficLightState);
            
            light.NextState();
        }
    }
    private const int TRAFFIC_LIGHT_BASE = 0;


    private enum BaseColorType
    {
        GREEN,
        GREY,
    }
    public void ChangeLightColor(Color color)
    {

    }

    public void UpdateLightState()
    {

    }
    TrafficLighState GetNextState(TrafficLighState previousState)
    {
        TrafficLighState nextPossibleState = previousState++;
        if(((int)nextPossibleState)> Enum.GetNames(typeof(TrafficLighState)).Length)
        {
            nextPossibleState = 0;
        }
        return nextPossibleState;
    }


}
[Serializable]
public class LightState
{
    [SerializeField] private TrafficLighState[] statesThatTheLightCanHave;
    [SerializeField] private TrafficLighState currentTrafficState;
    [SerializeField] MeshRenderer meshRenderer;

    internal TrafficLighState TrafficLightState { get => currentTrafficState; set => currentTrafficState = value; }

    internal void NextState(Material materialForState=null, TrafficLighState newState = null)
    {
        meshRenderer.material = materialForState;
        TrafficLightState = newState;

    }
    void NextState()
    {
        currentTrafficState++;
        if()
        if()
    }

}
internal enum TrafficLighState
{
    RED,
    YELLOW,
    GREEN,
    OFF
}