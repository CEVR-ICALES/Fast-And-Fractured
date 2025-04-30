using FastAndFractured;
using UnityEngine;
using Enums;
using System.Collections.Generic;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ToggleHUDEffectsAction", menuName = "PlayerShootingStateMachine/Actions/ToggleHUDEffectsAction")]

    public class ToggleHUDEffectsAction : Action
    {
        [SerializeField] private List<ScreenEffects> screenEffectsType;

        public override void Act(Controller controller)
        {
            foreach (ScreenEffects screenEffect in screenEffectsType)
            {
                GameObject hudEffect = HUDManager.Instance.GetEffectGameObject(ResourcesManager.Instance.GetResourcesSprite(screenEffect));
                hudEffect.SetActive(!hudEffect.activeSelf);
            }
        }
    }
}
