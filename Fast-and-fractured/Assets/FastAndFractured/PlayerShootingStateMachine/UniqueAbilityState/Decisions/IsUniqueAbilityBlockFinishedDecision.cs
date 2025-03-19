using UnityEngine;
using Game;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsUniqueAbilityBlockFinishedDecision), menuName = "PlayerShootingStateMachine/Decisions/IsUniqueAbilityBlockFinishedDecision")]
    public class IsUniqueAbilityBlockFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<PlayerInputController>().IsAbilityFinished;
        }
    }
}