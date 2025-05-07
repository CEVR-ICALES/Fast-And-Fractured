using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
 
 

public class TrafficLightLamp : MonoBehaviour
{
    [Tooltip("El tipo de luz que representa este componente (Roja, Amarilla, Verde, etc.)")]
    public LampType lampType;
    [Tooltip("Renderer de la malla para esta luz espec?fica.")]
    public MeshRenderer lampRenderer;

    private Material _originalMaterial; // Para restaurar si es necesario, aunque cambiaremos entre On/Off
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
                Debug.LogError($"TrafficLightLamp en {gameObject.name} no tiene MeshRenderer asignado y no se encontr? uno.", gameObject);
                return;
            }
        }
        _originalMaterial = lampRenderer.sharedMaterial; // Guardar el material original por si acaso
        _lightOnMaterial = lightOnMaterial;
        _lightOffMaterial = lightOffMaterial;
        _isInitialized = true;
        TurnOff(); // Por defecto, apagada
    }

    public void TurnOn()
    {
        if (!_isInitialized || lampRenderer == null) return;
        if (_lightOnMaterial != null)
        {
            lampRenderer.material = _lightOnMaterial;
        }
        else
        {
            // Fallback si no hay material "On", podr?amos usar emisi?n en el original.
            // Pero basado en tu descripci?n, deber?as tener un material "On".
            Debug.LogWarning($"LightOnMaterial no asignado para {gameObject.name}. La luz no se encender? correctamente.");
        }
    }

    public void TurnOff()
    {
        if (!_isInitialized || lampRenderer == null) return;
        if (_lightOffMaterial != null)
        {
            lampRenderer.material = _lightOffMaterial;
        }
        else
        {
            // Fallback si no hay material "Off"
            lampRenderer.material = _originalMaterial;
            Debug.LogWarning($"LightOffMaterial no asignado para {gameObject.name}. Se usar? el material original.");
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
    PedestrianRed,    // O "Don't Walk"
    PedestrianGreen,  // O "Walk"
    Off // Un estado donde ninguna luz de la unidad est? activa, si es necesario en la secuencia
}