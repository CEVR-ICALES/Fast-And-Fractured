using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class GenericInputEvent : MonoBehaviour
{
    private const string MISSING_ACTION_WARNING = "GenericInputEvent has no valid input action reference.";

    [SerializeField] private InputActionReference inputActionReference;
    [SerializeField] private UnityEvent onInputPerformedEvent;

    private static readonly Dictionary<InputAction, ActionUsage> _actionUsages = new Dictionary<InputAction, ActionUsage>();
    private InputAction _boundAction;

    private sealed class ActionUsage
    {
        public int users;
        public bool wasEnabled;
    }

    #region Lifecycle
    private void OnEnable()
    {
        Unbind();
        var action = inputActionReference != null ? inputActionReference.action : null;
        if (action == null)
        {
            Debug.LogWarning(MISSING_ACTION_WARNING, this);
            return;
        }

        if (!_actionUsages.TryGetValue(action, out var usage))
        {
            usage = new ActionUsage { wasEnabled = action.enabled };
            _actionUsages.Add(action, usage);
        }
        usage.users++;
        _boundAction = action;
        action.performed += TriggerInputEvent;
        action.Enable();
    }

    private void OnDisable() => Unbind();
    #endregion

    #region Binding
    private void Unbind()
    {
        var action = _boundAction;
        _boundAction = null;
        if (action == null) return;
        action.performed -= TriggerInputEvent;
        if (!_actionUsages.TryGetValue(action, out var usage)) return;
        if (--usage.users > 0) return;
        _actionUsages.Remove(action);
        if (!usage.wasEnabled) action.Disable();
    }

    private void TriggerInputEvent(InputAction.CallbackContext context)
    {
        onInputPerformedEvent?.Invoke();
    }
    #endregion
}
