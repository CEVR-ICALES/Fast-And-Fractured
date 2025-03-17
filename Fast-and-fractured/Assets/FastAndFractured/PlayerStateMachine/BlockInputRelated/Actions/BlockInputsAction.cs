using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = "BlockInputsAction", menuName = "PlayerStateMachine/Actions/BlockInputsAction")]

public class BlockInputsAction : Action
{
    public InputBlockTypes InputBlockType;
    public override void Act(Controller controller)
    {
        controller.GetBehaviour<PlayerInputController>().BlockInput(InputBlockType);
    }
}