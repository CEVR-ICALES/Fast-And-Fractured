using FastAndFractured;
using UnityEngine;
using Enums;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ChangeHUDEffectAction", menuName = "PlayerShootingStateMachine/Actions/ChangeHUDEffectAction")]

    public class ChangeHUDEffectAction : Action
    {
        [SerializeField] private UIElementType target;
        [SerializeField] private ScreenEffects spriteKey;

        public override void Act(Controller controller)
        {
            HUDManager.Instance.UpdateUIElement(target, ResourcesManager.Instance.GetResourcesSprite(spriteKey));
        }
    }
}