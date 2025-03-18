using UnityEngine;
using Game;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsUniqueAbilityBlockFinishedDecision), menuName = "PlayerStateMachine/Decisions/IsUniqueAbilityBlockFinishedDecision")]
    public class IsUniqueAbilityBlockFinishedDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            // to do : check if the unique ability block is finished
            return false;
        }
    }
}