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
        [SerializeField] private float timeInScreen = 0f;

        public override void Act(Controller controller)
        {
            HUDManager.Instance.UpdateUIEffect(target, ResourcesManager.Instance.GetResourcesSprite(spriteKey), timeInScreen);
        }
    }
}