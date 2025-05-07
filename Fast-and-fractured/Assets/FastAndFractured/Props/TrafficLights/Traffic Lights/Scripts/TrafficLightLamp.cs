using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;



public class TrafficLightLamp : MonoBehaviour
{
    public LampType lampType;
    public MeshRenderer lampRenderer;

    private Material _originalMaterial;
    private Material _lightOnMaterial;
    private Material _lightOffMaterial;
    private bool _isInitialized = false;

    public void Initialize(Material lightOnMaterial, Material lightOffMaterial)
    {
        if (lampRenderer == null)
        {
            lampRenderer = GetComponent<MeshRenderer>();
            if (lampRenderer == null)
            {
                Debug.LogError($"TrafficLightLamp   {gameObject.name} does not have meshrenderer", gameObject);
                return;
            }
        }
        _originalMaterial = lampRenderer.sharedMaterial;
        _lightOnMaterial = lightOnMaterial;
        _lightOffMaterial = lightOffMaterial;
        _isInitialized = true;
        TurnOff();
    }

    public void TurnOn()
    {
        if (!_isInitialized || lampRenderer == null) return;
        if (_lightOnMaterial != null)
        {
            lampRenderer.material = _lightOnMaterial;
        }

    }

    public void TurnOff()
    {
        if (!_isInitialized || lampRenderer == null) return;
        if (_lightOffMaterial != null)
        {
            lampRenderer.material = _lightOffMaterial;
        }

    }

    void OnValidate()
    {
        if (lampRenderer == null)
        {
            lampRenderer = GetComponent<MeshRenderer>();
        }
    }
}
public enum PoleMaterialType
{
    Green,
    Grey
}

public enum LampType
{
    Red,
    Yellow,
    Green,
    PedestrianRed,
    PedestrianGreen,
    Off
}