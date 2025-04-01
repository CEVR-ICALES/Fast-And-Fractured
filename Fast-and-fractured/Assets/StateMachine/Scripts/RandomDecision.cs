using System;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "RandomDecision", menuName = "StateMachine/RandomDecision")]
    public class DecisionRandomDecision : Decision
    {
        [SerializeField] int seed = -1;
        public override bool Decide(Controller controller)
        {

            if (seed != -1)
            {
                UnityEngine.Random.InitState(seed);

            }
            return UnityEngine.Random.Range(0, 2) == 1;
        }
    }
}
