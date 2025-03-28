using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ActionDebug", menuName = "StateMachine/ActionDebug")]
    public class ActionDebug : Action
    {
        public string messageToDebug;
        public override void Act(Controller controller)
        {
            Debug.Log($"{messageToDebug}");
            FinishAction();
        }
    }
}