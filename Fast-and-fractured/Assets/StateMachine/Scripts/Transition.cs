
namespace StateMachine
{
    [System.Serializable]
    public class Transition
    {
        public Decision decision;
        public State trueState;
        public State falseState;
        public bool hasExitTime;

        public State CheckTransition(Controller controller)
        {
            return decision.Decide(controller) ? trueState : falseState;
        }
    }
}