using StateMachine;
using UnityEngine;
using Game;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsMovingDecision), menuName = "PlayerStateMachine/Decisions/IsMovingDecision")]
    public class IsMovingDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<PhysicsBehaviour>().IsVehicleMoving();
        }
    }
}
