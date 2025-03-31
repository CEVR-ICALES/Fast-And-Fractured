using FastAndFractured;
using UnityEngine;
using Enums;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ChangeHUDSpriteAction", menuName = "PlayerShootingStateMachine/Actions/ChangeHUDSpriteAction")]

    public class ChangeHUDSpriteAction : Action
    {
        [SerializeField] private UIElementType target;
        [SerializeField] private Sprite newSprite;

        public override void Act(Controller controller)
        {
            HUDManager.Instance.UpdateUIElement(target, newSprite);
        }
    }
}