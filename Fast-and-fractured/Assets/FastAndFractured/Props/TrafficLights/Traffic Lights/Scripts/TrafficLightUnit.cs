using UnityEngine;
using System.Collections.Generic;
using System;
using Utilities;
using UnityEditor; // Para TimerSystem

[RequireComponent(typeof(TrafficLightLampCollector))] // Para ayudar a encontrar las luces
public class TrafficLightUnit : MonoBehaviour
{
    [Serializable]
    public struct LightStateDefinition
    {
        [Tooltip("El tipo de luz que estará activa en este estado (Roja, Verde, etc.).")]
        public LampType activeLamp;
        [Tooltip("Duración de este estado en segundos.")]
        public float duration;
        [Tooltip("¿Esta luz parpadea durante este estado?")]
        public bool isFlashing;
        [Tooltip("Tiempo encendida durante el parpadeo (si isFlashing es true).")]
        public float flashOnTime;
        [Tooltip("Tiempo apagada durante el parpadeo (si isFlashing es true).")]
        public float flashOffTime;
    }

    [Tooltip("Secuencia de estados por los que ciclará esta unidad de semáforo.")]
    public List<LightStateDefinition> stateSequence = new List<LightStateDefinition>();

    [Tooltip("Lista de lámparas gestionadas por esta unidad. Rellenar en el editor o usar 'Auto-Collect Lamps'.")]
    public List<TrafficLightLamp> lamps = new List<TrafficLightLamp>();

    private int _currentStateIndex = -1;
    private ITimer _stateTimer;
    private ITimer _flashTimer;
    private bool _isFlashingLampOn = false;
    private TrafficLightLamp _currentFlashingLamp = null;
    private bool _isUnitActive = false;

    private Material _lightOnMaterial;
    private Material _lightOffMaterial;

    public void Initialize(Material lightOnMaterial, Material lightOffMaterial)
    {
        _lightOnMaterial = lightOnMaterial;
        _lightOffMaterial = lightOffMaterial;

        if (lamps.Count == 0)
        {
            Debug.LogWarning($"TrafficLightUnit en {gameObject.name} no tiene lámparas asignadas. Intenta usar 'Auto-Collect Lamps' en el TrafficLightPole.", gameObject);
        }

        foreach (var lamp in lamps)
        {
            if (lamp != null)
            {
                lamp.Initialize(_lightOnMaterial, _lightOffMaterial);
            }
        }
        _currentStateIndex = -1; // Asegura que empiece desde el primer estado al activar
    }

    public void ActivateUnit()
    {
        if (stateSequence.Count == 0)
        {
            Debug.LogWarning($"TrafficLightUnit en {gameObject.name} no tiene secuencia de estados. No se puede activar.", gameObject);
            TurnAllLampsOff();
            return;
        }
        _isUnitActive = true;
        _currentStateIndex = -1; // Para que AdvanceState empiece en el 0
        AdvanceState();
    }

    public void DeactivateUnit()
    {
        _isUnitActive = false;
        StopCurrentTimers();
        TurnAllLampsOff();
    }

    public void SetInitialState(int stateIndex = 0)
    {
        if (stateSequence.Count == 0 || stateIndex < 0 || stateIndex >= stateSequence.Count)
        {
            TurnAllLampsOff();
            return;
        }
        _currentStateIndex = stateIndex;
        ApplyState(stateSequence[_currentStateIndex]);
    }


    private void StopCurrentTimers()
    {
        if (_stateTimer != null && TimerSystem.Instance.HasTimer(_stateTimer))
        {
            TimerSystem.Instance.StopTimer(_stateTimer.GetData().ID);
            _stateTimer = null;
        }
        if (_flashTimer != null && TimerSystem.Instance.HasTimer(_flashTimer))
        {
            TimerSystem.Instance.StopTimer(_flashTimer.GetData().ID);
            _flashTimer = null;
        }
    }

    private void AdvanceState()
    {
        if (!_isUnitActive || stateSequence.Count == 0) return;

        StopCurrentTimers(); // Detiene timers anteriores antes de empezar uno nuevo

        _currentStateIndex = (_currentStateIndex + 1) % stateSequence.Count;
        LightStateDefinition currentState = stateSequence[_currentStateIndex];

        ApplyState(currentState);

        if (currentState.duration > 0)
        {
            _stateTimer = TimerSystem.Instance.CreateTimer(currentState.duration, onTimerDecreaseComplete: AdvanceState);
        }
    }

    private void ApplyState(LightStateDefinition stateDef)
    {
        TurnAllLampsOff(); // Apaga todas primero

        _currentFlashingLamp = null; // Resetea la lámpara que parpadea

        if (stateDef.activeLamp != LampType.Off) // Si no es un estado 'Todo Apagado'
        {
            TrafficLightLamp lampToActivate = FindLamp(stateDef.activeLamp);
            if (lampToActivate != null)
            {
                if (stateDef.isFlashing)
                {
                    _currentFlashingLamp = lampToActivate;
                    _isFlashingLampOn = false; // Empezar apagado para el primer tick de flash
                    StartFlashing();
                }
                else
                {
                    lampToActivate.TurnOn();
                }
            }
            else
            {
                Debug.LogWarning($"No se encontró la lámpara de tipo {stateDef.activeLamp} en {gameObject.name} para el estado actual.", gameObject);
            }
        }
    }

    private void StartFlashing()
    {
        if (_currentFlashingLamp == null || !stateSequence[_currentStateIndex].isFlashing) return;

        LightStateDefinition currentState = stateSequence[_currentStateIndex];
        _isFlashingLampOn = !_isFlashingLampOn; // Invertir estado

        if (_isFlashingLampOn)
        {
            _currentFlashingLamp.TurnOn();
            if (currentState.flashOnTime > 0)
                _flashTimer = TimerSystem.Instance.CreateTimer(currentState.flashOnTime, onTimerDecreaseComplete: StartFlashing);
        }
        else
        {
            _currentFlashingLamp.TurnOff();
            if (currentState.flashOffTime > 0)
                _flashTimer = TimerSystem.Instance.CreateTimer(currentState.flashOffTime, onTimerDecreaseComplete: StartFlashing);
        }
    }


    private TrafficLightLamp FindLamp(LampType type)
    {
        foreach (var lamp in lamps)
        {
            if (lamp != null && lamp.lampType == type)
            {
                return lamp;
            }
        }
        return null;
    }

    private void TurnAllLampsOff()
    {
        foreach (var lamp in lamps)
        {
            if (lamp != null) lamp.TurnOff();
        }
    }

    // En TrafficLightUnit.cs

    public void Editor_CollectLamps()
    {
        lamps.Clear();

        // 1. Buscar TrafficLightLamp existentes en toda la jerarquía descendiente de ESTA UNIDAD.
        TrafficLightLamp[] existingLamps = GetComponentsInChildren<TrafficLightLamp>(true);
        foreach (var lamp in existingLamps)
        {
            // GetComponentsInChildren asegura que 'lamp' es un descendiente de este TrafficLightUnit.
            if (!lamps.Contains(lamp))
            {
                lamps.Add(lamp);
            }
        }

        // 2. Fallback: Si no se encontraron lámparas con el componente, buscar MeshRenderers
        //    bajo esta unidad y añadirles el componente TrafficLightLamp.
        if (lamps.Count == 0)
        {
            // Buscar todos los MeshRenderers descendientes de esta unidad.
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                // Asegurarse de que el GameObject con el MeshRenderer no sea la propia unidad
                // (las unidades no suelen tener su propio renderer, son contenedores).
                if (r.gameObject == this.gameObject) continue;

                TrafficLightLamp lampComponent = r.gameObject.GetComponent<TrafficLightLamp>();
                if (lampComponent == null) // Solo añadir si no existe ya
                {
                    lampComponent = r.gameObject.AddComponent<TrafficLightLamp>();
                    lampComponent.lampRenderer = r; // Asignar el renderer encontrado

                    // Intenta adivinar el tipo de lámpara por el nombre del GameObject.
                    string nameLower = r.gameObject.name.ToLower();
                    if (nameLower.Contains("pedestrian") || nameLower.Contains("peaton"))
                    {
                        if (nameLower.Contains("red") || nameLower.Contains("rojo")) lampComponent.lampType = LampType.PedestrianRed;
                        else if (nameLower.Contains("green") || nameLower.Contains("walk") || nameLower.Contains("verde")) lampComponent.lampType = LampType.PedestrianGreen;
                    }
                    else // Asumir semáforo de vehículos
                    {
                        if (nameLower.Contains("red") || nameLower.Contains("rojo")) lampComponent.lampType = LampType.Red;
                        else if (nameLower.Contains("yellow") || nameLower.Contains("amber") || nameLower.Contains("amarillo")) lampComponent.lampType = LampType.Yellow;
                        else if (nameLower.Contains("green") || nameLower.Contains("verde")) lampComponent.lampType = LampType.Green;
                    }
                }
                // Añadir a la lista (incluso si ya existía pero no fue recogido en el primer paso, aunque es improbable).
                if (!lamps.Contains(lampComponent))
                {
                    lamps.Add(lampComponent);
                }
            }
        }
        // Mark dirty para que los cambios se guarden
        EditorUtility.SetDirty(this);
        foreach (var lamp in lamps) if (lamp != null) EditorUtility.SetDirty(lamp);
    }
}

// Pequeño helper para el botón en el editor de TrafficLightUnit
public class TrafficLightLampCollector : MonoBehaviour { }