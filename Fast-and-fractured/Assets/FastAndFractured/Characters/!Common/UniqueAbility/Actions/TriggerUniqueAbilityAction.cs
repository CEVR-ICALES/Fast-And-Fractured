using FastAndFractured;
using StateMachine;
using UnityEngine;
using Utilities;

[CreateAssetMenu(fileName = nameof(TriggerUniqueAbilityAction), menuName = "UniqueAbility/Actions/TriggerUniqueAbilityAction")]
public class TriggerUniqueAbilityAction : Action
{
    public override void Act(Controller controller)
    {
        NormalShootHandle normalShootHandle = controller.GetBehaviour<NormalShootHandle>();
        normalShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
        controller.GetBehaviour<BaseUniqueAbility>().ActivateAbility();
    }
}
