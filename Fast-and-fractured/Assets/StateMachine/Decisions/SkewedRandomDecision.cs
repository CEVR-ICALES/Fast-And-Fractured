using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    [CreateAssetMenu(fileName = nameof(SkewedRandomDecision), menuName = "StateMachine/SkewedRandomDecision")]
public class SkewedRandomDecision : Decision
{
    [Tooltip("The percentage it will go to the True state. The other percentage will go to the False state")]
    [SerializeField][Range(10, 100)] int percentageTrueState = 90;
    public override bool Decide(Controller controller)
    {
        //101 because max is exclusive
        return Random.Range(1, 101) <= percentageTrueState;
    }

}
