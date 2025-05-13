using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class ResetDeviceBindings : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string targetControlScheme;

    public void ResetALlBindings()
    {
        foreach (InputActionMap map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }
    }

    public void ResetControlSchemeBinding()
    {
        foreach (InputActionMap map in inputActions.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                action.RemoveBindingOverride(InputBinding.MaskByGroup(targetControlScheme));
            }
        }
    }
}
