using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(NOTDecision), menuName = "StateMachine/Decision/NOTDecision")]
    public class NOTDecision : Decision
    {
        [SerializeField] Decision decisionToInvert;

        public override bool Decide(Controller controller)
        {
            return !decisionToInvert.Decide(controller);
        }
    }
}