using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsChickenRotatingDecision), menuName = "MegaChickenStateMachine/Decisions/IsChickenRotatingDecision")]
    public class IsChickenRotatingDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<ChickenBrain>().IsRotating;
        }
    }
}
