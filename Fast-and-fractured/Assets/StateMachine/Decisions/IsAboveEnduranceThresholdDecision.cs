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
        float health = 0;
        if (selfEndurance)
        {
            //TODO
            //get health of self
        } else
        {
            EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();
            //TODO
            //get target health from target
        }
        

        return health >= enduranceThreshold;
    }

    
}
