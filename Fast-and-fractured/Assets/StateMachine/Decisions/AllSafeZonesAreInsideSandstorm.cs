using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(AllSafeZonesAreInsideSandstorm),menuName = "EnemyStateMachine/Decisions/AllSafeZonesAreInsideSandstorm")]
    public class AllSafeZonesAreInsideSandstorm : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<EnemyAIBrain>().AreAllSafeZonesInsideSandstorm();
        }
    }
}
