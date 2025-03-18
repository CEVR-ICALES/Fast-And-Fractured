using UnityEngine;
using Game;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsPushShootingFinishedDecision), menuName = "PlayerStateMachine/Decisions/IsPushShootingFinishedDecision")]
    public class IsPushShootingFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<PlayerInputController>().IsPushShooting;
        }
    }
}
