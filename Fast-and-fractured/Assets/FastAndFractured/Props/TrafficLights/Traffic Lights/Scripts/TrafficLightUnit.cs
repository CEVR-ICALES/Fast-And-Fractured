using UnityEngine;
using System.Collections.Generic;
using System;
using Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
[RequireComponent(typeof(TrafficLightLampCollector))]
public class TrafficLightUnit : MonoBehaviour
{
    [Serializable]
    public struct LightStateDefinition
    {
        public LampType activeLamp;
        public float duration;
        public bool isFlashing;
        public float flashOnTime;
        public float flashOffTime;
    }

    public List<LightStateDefinition> stateSequence = new List<LightStateDefinition>();
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
            Debug.LogWarning($"There are no lightlmps use 'Auto-Collect Lamps' in TrafficLightPole. to assign them or make it manually", gameObject);
        }

        foreach (var lamp in lamps)
        {
            if (lamp != null)
            {
                lamp.Initialize(_lightOnMaterial, _lightOffMaterial);
            }
        }
        _currentStateIndex = -1;
    }

    public void ActivateUnit()
    {
        if (stateSequence.Count == 0)
        {
            Debug.LogWarning($"TrafficLightUnit does not have states", gameObject);
            TurnAllLampsOff();
            return;
        }
        _isUnitActive = true;
        _currentStateIndex = -1;
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

        StopCurrentTimers();

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
        TurnAllLampsOff();

        _currentFlashingLamp = null;

        if (stateDef.activeLamp != LampType.OFF)
        {
            TrafficLightLamp lampToActivate = FindLamp(stateDef.activeLamp);
            if (lampToActivate != null)
            {
                if (stateDef.isFlashing)
                {
                    _currentFlashingLamp = lampToActivate;
                    _isFlashingLampOn = false;
                    StartFlashing();
                }
                else
                {
                    lampToActivate.TurnOn();
                }
            }
             
        }
    }

    private void StartFlashing()
    {
        if (_currentFlashingLamp == null || !stateSequence[_currentStateIndex].isFlashing) return;

        LightStateDefinition currentState = stateSequence[_currentStateIndex];
        _isFlashingLampOn = !_isFlashingLampOn;

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


    public void Editor_CollectLamps()
    {
        lamps.Clear();

        TrafficLightLamp[] existingLamps = GetComponentsInChildren<TrafficLightLamp>(true);
        foreach (var lamp in existingLamps)
        {
            if (!lamps.Contains(lamp))
            {
                lamps.Add(lamp);
            }
        }

        if (lamps.Count == 0)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {

                if (r.gameObject == this.gameObject) continue;

                TrafficLightLamp lampComponent = r.gameObject.GetComponent<TrafficLightLamp>();
                if (lampComponent == null)
                {
                    lampComponent = r.gameObject.AddComponent<TrafficLightLamp>();
                    lampComponent.lampRenderer = r;
                    string nameLower = r.gameObject.name.ToLower();
                    if (nameLower.Contains("pedestrian") || nameLower.Contains("peaton"))
                    {
                        if (nameLower.Contains("red") || nameLower.Contains("rojo")) lampComponent.lampType = LampType.PEDESTRIAN_RED;
                        else if (nameLower.Contains("green") || nameLower.Contains("walk") || nameLower.Contains("verde")) lampComponent.lampType = LampType.PEDESTRIAN_GREEN;
                    }
                    else
                    {
                        if (nameLower.Contains("red") || nameLower.Contains("rojo")) lampComponent.lampType = LampType.RED;
                        else if (nameLower.Contains("yellow") || nameLower.Contains("amber") || nameLower.Contains("amarillo")) lampComponent.lampType = LampType.YELLOW;
                        else if (nameLower.Contains("green") || nameLower.Contains("verde")) lampComponent.lampType = LampType.GREEN;
                    }
                }
                if (!lamps.Contains(lampComponent))
                {
                    lamps.Add(lampComponent);
                }
            }
        }
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        foreach (var lamp in lamps) if (lamp != null) EditorUtility.SetDirty(lamp);
#endif
    }
}
[Obsolete ("Not needed anymore")]
public class TrafficLightLampCollector : MonoBehaviour { }