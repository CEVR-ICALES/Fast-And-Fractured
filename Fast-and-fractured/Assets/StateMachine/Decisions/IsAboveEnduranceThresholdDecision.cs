using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsAboveEnduranceThresholdDecision), menuName = "EnemyStateMachine/Decisions/IsAboveEnduranceThreshold")]
public class IsAboveEnduranceThresholdDecision : Decision
{
    [SerializeField] float enduranceThreshold = -1f
        ;
    [SerializeField] bool selfEndurance = true;
    public override bool Decide(Controller controller)
    {
        float health = 100;
        if (selfEndurance)
        {
            EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();
            health = brain.GetHealth();
        } else
        {
            //TODO
            //get target endurance
        }
        

        return health <= enduranceThreshold;
    }

    
}
