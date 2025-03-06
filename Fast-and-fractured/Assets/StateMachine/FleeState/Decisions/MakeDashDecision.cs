using StateMachine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(MakeDashDecision), menuName = "EnemyStateMachine/Decisions/MakeDashDecision")]

public class MakeDashDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        return brain.IsDashAvailable();
     }
}
