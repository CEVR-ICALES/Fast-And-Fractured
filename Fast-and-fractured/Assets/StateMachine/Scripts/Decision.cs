//[CreateAssetMenu(fileName = "Decision.decision", menuName = "StateMachine/Decision")]
using UnityEngine;

namespace StateMachine
{
    public abstract class Decision : ScriptableObject
    {
        public abstract bool Decide(Controller controller);
    }
}