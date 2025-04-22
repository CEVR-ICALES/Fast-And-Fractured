using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using FastAndFractured;

[CreateAssetMenu(fileName = nameof(AboveRecoveryHealthPercentageDecision), menuName = "EnemyStateMachine/Decisions/IsAboveRecoveryHealthPercentage")]
public class AboveRecoveryHealthPercentageDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        StatsController statsController = controller.GetBehaviour<StatsController>();

        return statsController.Endurance >= brain.RecoveryThresholdPercentageForSearch;
    }

}
