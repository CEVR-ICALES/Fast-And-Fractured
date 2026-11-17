using Enums;
using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "ToggleUISight", menuName = "PlayerShootingStateMachine/Actions/ToggleUISight")]
    public class ToggleUISight : Action
    {
        [SerializeField]
        private bool enable;
        public override void Act(Controller controller)
        {
            NormalShootHandle normalShootHandle = controller.GetBehaviour<NormalShootHandle>();
            if (normalShootHandle != null)
            {
                normalShootHandle.HandleExternalFadeSightUI(enable);
            }
        }
    }
}
