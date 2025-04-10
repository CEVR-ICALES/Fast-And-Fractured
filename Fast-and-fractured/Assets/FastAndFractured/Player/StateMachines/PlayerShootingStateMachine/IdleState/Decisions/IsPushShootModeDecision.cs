using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsPushShootModeDecision), menuName = "PlayerShootingStateMachine/Decisions/IsPushShootModeDecision")]
    public class IsPushShootModeDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<PlayerInputController>().IsPushShootMode;
        }
    }
}