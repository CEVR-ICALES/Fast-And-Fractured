using UnityEngine;

//[CreateAssetMenu(fileName = "Action.action", menuName = "StateMachine/Action")]
namespace StateMachine
{
    public abstract class Action : ScriptableObject
    {
        bool isFinished = false;
        public abstract void Act(Controller controller);

        public virtual void FinishAction()
        {
            isFinished = true;
        }

        public bool IsFinished()
        {
            return isFinished;
        }
    }
}
