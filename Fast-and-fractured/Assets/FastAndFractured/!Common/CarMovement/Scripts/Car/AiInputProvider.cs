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

        public Vector3 AimDirection => throw new System.NotImplementedException();

        public bool IsNormalShooting => throw new System.NotImplementedException();

        public bool IsPushShooting => throw new System.NotImplementedException();

        public bool FirePushAction => throw new System.NotImplementedException();

        public bool IsThrowingMine => throw new System.NotImplementedException();

        public void Initialize()
        {
        }
    }
}
