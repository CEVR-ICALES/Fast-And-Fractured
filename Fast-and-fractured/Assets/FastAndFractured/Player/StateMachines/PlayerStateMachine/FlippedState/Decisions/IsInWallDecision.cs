using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachine {
    [CreateAssetMenu(fileName = nameof(IsInWallDecision), menuName = "PlayerStateMachine/Decisions/IsInWallDecision")]
    public class IsInWallDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            throw new System.NotImplementedException();
        }
    }
}
