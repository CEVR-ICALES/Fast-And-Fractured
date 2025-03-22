using StateMachine;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DecisionAllActionsAreFinished", menuName = "StateMachine/DecisionAllActionsAreFinished")]
[Obsolete("Not needed anymore")]public class DecisionAllActionsAreFinished : Decision
{
    public override bool Decide(Controller controller)
    {
      State currentState = controller.GetCurrentState();
        bool allActionsEnded = true;
        for (int i = 0;i < currentState.actions.Length && allActionsEnded; i++ )
        {
            allActionsEnded = currentState.actions[i].IsFinished();
        }
        return allActionsEnded;
    }
}