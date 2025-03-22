using UnityEngine;
namespace StateMachine
{
    [CreateAssetMenu(fileName = "State.state", menuName = "StateMachine/State")]

    public class State : ScriptableObject
    {
        [SerializeField] public Action[] enterActions;
        [SerializeField] public Action[] actions;
        [SerializeField] public Action[] exitActions;
        [SerializeField] public Transition[] transitions;
        public bool AreAllActionsFinished()
        {
            bool allActionsFinished = true;
            for (int i = 0; i < actions.Length && allActionsFinished; i++)
            {
                allActionsFinished = actions[i].IsFinished();

            }
            return allActionsFinished;
        }
    }
}
