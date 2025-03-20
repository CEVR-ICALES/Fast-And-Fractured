using System;
using System.Collections;
using System.Collections.Generic;
using StateMachine;
using UnityEngine;

public class AIDebugStateChanger : MonoBehaviour
{
    [SerializeField] private State[] states;
    [SerializeField] Controller controller;
    private int _currentStateIndex = 0;


    private void Awake()
    {
        if (!controller)
        {
            controller = GetComponent<Controller>();
        }
    }
    [ContextMenu(nameof(NextState))]
    public void NextState()
    {
        _currentStateIndex++;
        if (_currentStateIndex >= states.Length)
        {
            _currentStateIndex = 0;
        }

        controller.ForceState(GetCurrentState());
    }

    private State GetCurrentState()
    {
        return states[_currentStateIndex];
    }
}