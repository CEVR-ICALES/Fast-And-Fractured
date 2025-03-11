using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;


public class ORDecision : Decision
{
    [SerializeField] Decision[] decisions;
    public override bool Decide(Controller controller)
    {
        bool success = false; 
        for (int i = 0; i < decisions.Length && !success; i++)
        {
            success = decisions[i].Decide(controller);
        }
        return success;
    }
}
