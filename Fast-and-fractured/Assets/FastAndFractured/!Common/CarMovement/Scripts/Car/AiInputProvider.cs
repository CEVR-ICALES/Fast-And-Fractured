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

        public void Initialize()
        {
        }
    }
}