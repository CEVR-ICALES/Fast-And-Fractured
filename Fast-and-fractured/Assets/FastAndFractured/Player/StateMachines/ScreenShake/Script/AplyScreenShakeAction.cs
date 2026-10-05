using FastAndFractured;
using UnityEngine;
namespace StateMachine{
    [CreateAssetMenu(fileName = "__ShakeAction", menuName = "PlayerStateMachine/Actions/__ShakeAction")]
    public class ApplyScreenShakeAction : Action
    {
        [SerializeField]
        private bool globalShake = false;
        [SerializeField]
        private bool fromProfile = true;

        [SerializeField]
        private ScreenShakeProfileType screenShakeProfile;
        public override void Act(Controller controller)
        {
            ScreenShakeSourceController screenShakeSourceController = controller.GetBehaviour<ScreenShakeSourceController>();
            CameraBehaviours cameraBehaviours = controller.GetBehaviour<CameraBehaviours>();
            if(screenShakeSourceController==null){
                Debug.LogWarning("Null reference ScreenShake Source Controller. Add a ScreenShakeSourceController in " + controller.gameObject + " from the player");
                return;
            }
            if (fromProfile)
            {
                if(globalShake)
                    screenShakeSourceController.PlayGlobalShakeFromProfile(cameraBehaviours,screenShakeProfile);
                else
                    screenShakeSourceController.PlayLocalShakeFromProfile(cameraBehaviours,screenShakeProfile);
            }
            else
            {
                if(globalShake)
                    screenShakeSourceController.PlayGlobalShake(cameraBehaviours);
                else
                    screenShakeSourceController.PlayLocalShake(cameraBehaviours);
            }
        }
    }
}
