using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(IsUniqueAbilityFinishedDecision), menuName = "EnemyStateMachine/Decisions/IsUniqueAbilityFinishedDecision")]
public class IsUniqueAbilityFinishedDecision : Decision
{ 
    public override bool Decide(Controller controller)
    {      
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        return  brain.IsUniqueAbilityFinished();
    }
}
