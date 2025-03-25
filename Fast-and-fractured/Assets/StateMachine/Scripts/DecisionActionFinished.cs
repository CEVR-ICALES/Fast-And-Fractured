using System;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "DecisionActionFinished", menuName = "StateMachine/DecisionActionFinished")]
    public class DecisionActionFinished : Decision
    {
        [SerializeField] StateMachine.Action actionToCheck;
        [SerializeField] bool useOnlyActionType = false;
        public override bool Decide(Controller controller)
        {
            State currentState = controller.GetCurrentState();
            foreach (var action in currentState.actions)
            {


                if ((action.GetType() == actionToCheck.GetType() && useOnlyActionType) || action.name.Replace("(Clone)", " ") == actionToCheck.name && !useOnlyActionType)
                {

                    return action.IsFinished();

                }
            }
            return false;
        }
    }
}
