using UnityEngine;

namespace FastAndFractured
{
    
    public class AiInputProvider : MonoBehaviour, IInputProvider
    {
        public Vector2 DesiredMoveInput { get; set; } = Vector2.zero;
        public bool DesiredBraking { get; set; } = false;
        public bool DesiredDashing { get; set; } = false;
        public Vector2 MoveInput => DesiredMoveInput;
        public bool IsBraking => DesiredBraking;
        public bool IsDashing => DesiredDashing;

        public Vector3 AimDirection => Vector3.zero;

        public bool IsNormalShooting => false;

        public bool IsPushShooting => false;

        public bool FirePushAction => false;

        public bool IsThrowingMine => false;

        public void Initialize()
        {
        }
    }
}
