using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = "DecisionAlwaysTrue.asset", menuName = "StateMachine/DecisionAlwaysTrue")]
public   class DecisionAlwaysTrue : Decision
{
    public override bool Decide(Controller controller)
    {
        return true;
    }
}