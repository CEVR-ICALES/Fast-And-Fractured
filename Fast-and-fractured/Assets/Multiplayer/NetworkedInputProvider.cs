using UnityEngine;

namespace FastAndFractured.Multiplayer
{ 
    public class NetworkInputProvider : MonoBehaviour, IInputProvider
    {
        public Vector2 MoveInput { get; private set; }
        public float IsAccelerating { get; private set; }
        public float IsReversing { get; private set; }
        public bool IsBraking { get; private set; }
        public bool IsDashing { get; private set; }

        public void Initialize()
        { }

        public void SetNetworkInput(VehicleInputData data)
        {
            MoveInput = data.MoveInput;
            IsAccelerating = Mathf.Max(0, data.AccelerateInput);
            IsReversing = Mathf.Max(0, -data.AccelerateInput);
            IsBraking = data.HandbrakeInput;
            IsDashing = data.DashInput;
        }
    }
    public struct VehicleInputData { public Vector2 MoveInput; public float AccelerateInput; public bool HandbrakeInput; public bool DashInput; }

}