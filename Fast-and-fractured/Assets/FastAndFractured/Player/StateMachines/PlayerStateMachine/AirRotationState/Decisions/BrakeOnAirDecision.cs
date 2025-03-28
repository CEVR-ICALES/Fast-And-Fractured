using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(BrakeOnAirDecision), menuName = "PlayerStateMachine/Decisions/BrakeOnAirDecision")]
public class BrakeOnAirDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        // to do
        return false;
    }
}
}
