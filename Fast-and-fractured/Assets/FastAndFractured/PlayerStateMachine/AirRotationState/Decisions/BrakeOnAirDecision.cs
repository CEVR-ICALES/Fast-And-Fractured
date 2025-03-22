using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(BrakeOnAirDecision), menuName = "PlayerStateMachine/Decisions/BrakeOnAirDecision")]
public class BrakeOnAirDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return true;
    }
}