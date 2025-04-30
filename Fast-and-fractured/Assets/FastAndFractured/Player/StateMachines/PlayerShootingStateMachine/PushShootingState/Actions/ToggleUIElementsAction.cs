using FastAndFractured;
using UnityEngine;
using Enums;
using System.Collections.Generic;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ToggleUIElementsAction", menuName = "PlayerShootingStateMachine/Actions/ToggleUIElementsAction")]

    public class ToggleUIElementsAction : Action
    {
        [SerializeField] private List<UIElementType> uiElementsType;

        public override void Act(Controller controller)
        {
            foreach (UIElementType uiElementType in uiElementsType)
            {
                UIDynamicElement uiElement = HUDManager.Instance.GetUIElement(uiElementType);
                uiElement.gameObject.SetActive(!uiElement.gameObject.activeSelf);
            }
        }
    }
}
