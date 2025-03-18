using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = "BlockInputsAction", menuName = "PlayerStateMachine/Actions/BlockInputsAction")]

public class BlockInputsAction : Action
{
    public InputBlockTypes inputBlockType;
    public bool hasTime;
    private float _blockTime = 1f;
    
    public override void Act(Controller controller)
    {
        if(hasTime)
        {
            controller.GetBehaviour<PlayerInputController>().BlockInput(inputBlockType, _blockTime);
        }
        else
        {
            controller.GetBehaviour<PlayerInputController>().BlockInput(inputBlockType);
        }
        
    }
}