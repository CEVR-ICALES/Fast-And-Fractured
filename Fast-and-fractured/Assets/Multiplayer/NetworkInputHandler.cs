using FastAndFractured;
using FishNet.Object;
using UnityEngine;

public class NetworkInputHandler : NetworkBehaviour
{
    [SerializeField] private PlayerInputController playerInput;
    CarMovementController _carMovementController;
    public struct VehicleInputData { public Vector2 MoveInput; public float AccelerateInput; public bool HandbrakeInput; public bool DashInput; }
    VehicleInputData _lastVehicleInputData;
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!base.IsOwner)
        {
            playerInput.enabled = false;
        }
    }

    void Update()
    {
        if (!base.IsOwner)
            return;
        float combinedThrottle = 0f;
        if (playerInput.IsAccelerating > 0) combinedThrottle = playerInput.IsAccelerating;
        else if (playerInput.IsReversing > 0) combinedThrottle = -playerInput.IsReversing;
        VehicleInputData input = new VehicleInputData
        {
            MoveInput = playerInput.MoveInput,
            AccelerateInput = combinedThrottle,
            HandbrakeInput = playerInput.IsBraking,
            DashInput = playerInput.IsDashing
        };

        SendInputToServer(input);
    }

    [ServerRpc]
    private void SendInputToServer(VehicleInputData vehicleInputData)
    {

        _lastVehicleInputData = vehicleInputData;

         
    }
    public VehicleInputData GetLastInput()
    {
        return _lastVehicleInputData;
    }
}
