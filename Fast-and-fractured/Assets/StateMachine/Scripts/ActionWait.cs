using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ActionWait", menuName = "StateMachine/ActionWait")]
    public class ActionWait : Action
    {
        public float timeToWait;
        float timeCounter;
        public override void Act(Controller controller)
        {

            timeCounter += Time.deltaTime;

            if (timeCounter >= timeToWait)
            {
                FinishAction();
            }

        }
    }
}
