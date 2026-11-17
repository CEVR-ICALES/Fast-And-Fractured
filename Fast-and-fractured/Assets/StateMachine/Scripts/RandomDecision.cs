using NRandom;
using System;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "RandomDecision", menuName = "StateMachine/RandomDecision")]
    public class DecisionRandomDecision : Decision
    {
        [SerializeField] int seed = -1;
        public override bool Decide(Controller controller)
        {

          
            return DeterministicRandom.Instance.NextInt(0, 2) == 1;
        }
    }
}
