using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class GenericInputEvent : MonoBehaviour {
[SerializeField] private InputActionReference inputActionReference;
[SerializeField] private UnityEvent onInputPerformedEvent;

    private void OnEnable()
    {
        inputActionReference.action.performed += TriggerInputEvent;
    }private void OnDisable()
    {
        inputActionReference.action.performed -= TriggerInputEvent;
    }

    private void TriggerInputEvent(InputAction.CallbackContext context)
    {
        onInputPerformedEvent?.Invoke();   
    }
}

