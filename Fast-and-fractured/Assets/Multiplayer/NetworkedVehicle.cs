using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using FastAndFractured;
using FishNet.Connection;
using FastAndFractured.Utilities;

namespace FastAndFractured.Multiplayer
{
    [RequireComponent(typeof(CarMovementController))]
    public class NetworkedVehicle : NetworkBehaviour
    {
        #region Structs de Red
        public struct MoveData : IReplicateData
        {
            public Vector2 MoveInput;
            public float AccelerateInput;
            public bool HandbrakeInput;
            public bool DashInput;

            private uint _tick;
            public void Dispose() { }
            public bool IsValid() => _tick > 0;

            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public PredictionRigidbody PredictionRigidbody;
            public float Speed;

            public ReconcileData(PredictionRigidbody prb,float speed)
            {
                PredictionRigidbody = prb;
                _tick = 0;
                Speed = speed;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        private CarMovementController _coreMovementController;
        private PhysicsBehaviour _physicsBehaviour;
        private ICustomRigidbody _customRigidbody;
        private bool _subscribedToTimeManager;
        private MoveData _lastReceivedMoveData;

        #region Events
        private void Awake()
        {
            _coreMovementController = GetComponent<CarMovementController>();
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            _customRigidbody = GetComponent<ICustomRigidbody>();

            if (_coreMovementController == null || _customRigidbody == null)
            {
                Debug.LogError($"NetworkedVehicle en '{gameObject.name}' no pudo encontrar CarMovementController o ICustomRigidbody.", this);
                this.enabled = false;
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            var multiplyaerRigidbody = _customRigidbody as MultiplayerRigidbodyAdapter;
            multiplyaerRigidbody.InitializeAdapter();

            if (!_subscribedToTimeManager)
            {
                base.TimeManager.OnTick += TimeManager_OnTick;
                base.TimeManager.OnPostTick += TimeManager_OnPostTick;
                _subscribedToTimeManager = true;
            }
      
        }
        public override void OnStartClient()
        {
            if (_customRigidbody != null)
            {
                _customRigidbody.isKinematic = false;
            }
        }
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null && _subscribedToTimeManager)
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
                _subscribedToTimeManager = false;
            }
        }

       
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

 
            if (base.IsOwner && LevelControllerButBetter.Instance != null && transform.parent != null)
            {
                // LevelControllerButBetter.Instance.SetLocalPlayer(transform.parent.gameObject);
            }
        }
        [ObserversRpc]
        public void InjectInputProvider()
        {
            var parentPlayerInputProvider = GetComponentInParent<PlayerInputProvider>();
            var parentNetworkInputProvider = GetComponentInParent<NetworkInputProvider>();

            if (!base.IsOwner)
            {
                _coreMovementController.InputProvider = parentNetworkInputProvider;
            }
            else // Cliente
            {
                _coreMovementController.InputProvider = parentPlayerInputProvider;
            }

            if (parentPlayerInputProvider != null)
            {
                (parentPlayerInputProvider as MonoBehaviour).enabled = base.IsOwner;
            }
        }
        #endregion

        #region 
        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            { 
                BuildMoveData(out MoveData md);
                Replicate(md);
            }
            if (base.IsServerInitialized)
            { 
                Replicate(default);
            }
        }

        private void TimeManager_OnPostTick()
        {
            if (base.IsServerInitialized)
            {
                 CreateReconcile();
            }
        }

        public override void CreateReconcile()
        {
            var adapter = _customRigidbody as MultiplayerRigidbodyAdapter;
            if (adapter != null)
            {
                ReconcileData rd = new ReconcileData(adapter.PredictionRigidbody,adapter.linearVelocity.magnitude);
                Reconcile(rd);
            }
        }

        private void BuildMoveData(out MoveData md)
        {
         
            
            md = default;
            if (_coreMovementController?.InputProvider == null) return;
            var provider = _coreMovementController.InputProvider as PlayerInputProvider;
            if (provider == null) return;

            md.MoveInput = provider.MoveInput;
            md.HandbrakeInput = provider.IsBraking;
            md.DashInput = provider.IsDashing;
            md.AccelerateInput = provider.IsAccelerating - provider.IsReversing;
            if (md.MoveInput != Vector2.zero || md.AccelerateInput != 0f || md.DashInput)
            {
                string context = base.IsServer ? "SERVER" : (base.IsOwner ? "OWNER_CLIENT" : "SPECTATOR");
                Debug.Log($"<color=yellow>[{context} - Tick {base.TimeManager.Tick}] Replicating Input: Move={md.MoveInput}, Accel={md.AccelerateInput}</color>");
            }
        }

         [Replicate]
        private void Replicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            bool isReplaying = (state == ReplicateState.Replayed);


            MoveData inputToProcess;

            if (base.IsOwner)
            {
                inputToProcess = md;
            }
            else 
            {
                if (md.GetTick() > 0)
                {
                    _lastReceivedMoveData = md;
                    inputToProcess = md;
                }
                else
                {
                    inputToProcess = _lastReceivedMoveData;
                }
            }

            if (md.MoveInput != Vector2.zero || md.AccelerateInput != 0f || md.DashInput)
            {
                string context = base.IsServer ? "SERVER" : (base.IsOwner ? "OWNER_CLIENT" : "SPECTATOR");
          //      Debug.Log($"<color=yellow>[{context} - Tick {base.TimeManager.Tick}] Replicating Input: Move={md.MoveInput}, Accel={md.AccelerateInput}</color>");
            }
            if (!base.IsOwner)
            {
                var provider = _coreMovementController.InputProvider as NetworkInputProvider;
                if (provider != null)
                {
                    provider.SetNetworkInput(new VehicleInputData
                    {
                        MoveInput = md.MoveInput,
                        AccelerateInput = md.AccelerateInput,
                        HandbrakeInput = md.HandbrakeInput,
                        DashInput = md.DashInput
                    });
                }
            }

            _coreMovementController.ProcessMovementInput();

            if (_coreMovementController?.InputProvider != null && _coreMovementController.InputProvider.IsDashing && _coreMovementController.CanDash)
            {
                _coreMovementController.HandleDashWithPhysics(isReplaying);
            }

             _customRigidbody?.Simulate();
        }

         [Reconcile]
        private void Reconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            if (IsServerInitialized && !IsHostInitialized)
                return;
            var adapter = _customRigidbody as MultiplayerRigidbodyAdapter;
            if (adapter != null)
            {
                adapter.PredictionRigidbody.Reconcile(rd.PredictionRigidbody);
            }
            _physicsBehaviour.CurrentSpeed = rd.Speed;
        }

        #endregion
    }
}