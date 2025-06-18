
using FishNet.Object;
using UnityEngine;
using static NetworkInputHandler;

namespace FastAndFractured.Multiplayer
{
    public class NetworkedVehicle : NetworkBehaviour
    {
        private CarMovementController _coreCarMovement;
        [SerializeField] private NetworkInputHandler networkInputHandler;
        private Rigidbody _rb;

 
        private void Awake()
        {
            _coreCarMovement = GetComponent<CarMovementController>();
            networkInputHandler = GetComponent<NetworkInputHandler>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (base.IsOwner)
            {
                if (networkInputHandler != null) networkInputHandler.enabled = true;
                if (_rb != null) _rb.isKinematic = false;
            }
            else
            {
                if (networkInputHandler != null) networkInputHandler.enabled = false;
                if (_rb != null) _rb.isKinematic = true;
            }
        }
        [Server]
        private void Update()
        {
            SendInputToServerRpc(networkInputHandler.GetLastInput());
        }

        [ServerRpc]
        private void SendInputToServerRpc(VehicleInputData input)
        {
            if (_coreCarMovement == null) return;

            _coreCarMovement.HandleSteeringInput(input.MoveInput);
            _coreCarMovement.HandleAccelerateInput(input.AccelerateInput);
            _coreCarMovement.HandleBrakingInput(input.HandbrakeInput, input.MoveInput);

            if (input.DashInput && _coreCarMovement.CanDash)
            {
                _coreCarMovement.HandleDashWithPhysics();
            }
        }
    }
}