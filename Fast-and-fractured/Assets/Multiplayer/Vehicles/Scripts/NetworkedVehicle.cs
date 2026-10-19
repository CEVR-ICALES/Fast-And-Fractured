using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using FastAndFractured;
using FishNet.Connection;
using FastAndFractured.Utilities;
using Utilities;

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

            public int DashTicksRemaining;
            public Vector3 DashDirection;
            public float CurrentSteerAngle;
            public float CurrentRbMaxVelocity;
            // Impulse in flight (trampoline/TNT/push): max speed is not re-clamped.
            public bool IsBeenImpulsed;
            public bool IsDrifting;
            public float DriftDirection;
            public float InitialSpeedWhenDrifting;
            public bool DriftIsBackwards;
            public float LinearDamping;

            // World-space copy: RigidbodyState round-trips in local space.
            public Vector3 WorldPosition;
            public Quaternion WorldRotation;
            public Vector3 WorldVelocity;
            public Vector3 WorldAngularVelocity;

            private const float DEFAULT_LINEAR_DAMPING = 0.08f;

            public ReconcileData(PredictionRigidbody prb,float speed)
            {
                PredictionRigidbody = prb;
                _tick = 0;
                Speed = speed;
                DashTicksRemaining = 0;
                DashDirection = Vector3.zero;
                CurrentSteerAngle = 0f;
                CurrentRbMaxVelocity = 0f;
                IsBeenImpulsed = false;
                IsDrifting = false;
                DriftDirection = 1f;
                InitialSpeedWhenDrifting = 0f;
                DriftIsBackwards = false;
                LinearDamping = DEFAULT_LINEAR_DAMPING;
                WorldPosition = Vector3.zero;
                WorldRotation = Quaternion.identity;
                WorldVelocity = Vector3.zero;
                WorldAngularVelocity = Vector3.zero;
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
        private NetworkedStateMachineDriver _stateMachineDriver;
        private VehicleVisualSmoother _visualSmoother;
        private bool _subscribedToTimeManager;

        private AiInputProvider _serverDrivenAiProvider;

        private bool _wasSpDashing;
        private int _dashTicksRemaining;
        private Vector3 _dashWorldDirection;
        private float _cachedDashForce = float.NaN;

        private int _dashSettleTicks;

        private bool _cameraTargetChecked;

        [Header("Multiplayer Runtime Guards")]
        [SerializeField] private bool suppressFalseFlipImpulse = true;
        [SerializeField, Range(-1f, 1f)] private float uprightDotThreshold = 0.8f;
        [SerializeField] private float stationarySpeedThreshold = 1.5f;

        #region Events
        private void Awake()
        {
            _coreMovementController = GetComponent<CarMovementController>();
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            // Prefer the multiplayer adapter; a leftover singleplayer one breaks reconciliation.
            _customRigidbody = (ICustomRigidbody)GetComponent<MultiplayerRigidbodyAdapter>() ?? GetComponent<ICustomRigidbody>();
            _visualSmoother = GetComponent<VehicleVisualSmoother>();

            if (_coreMovementController == null || _customRigidbody == null)
            {
                Debug.LogError($"NetworkedVehicle en '{gameObject.name}' no pudo encontrar CarMovementController o ICustomRigidbody.", this);
                this.enabled = false;
            }

            CacheAuthoredVisualPoses();
        }

        private static bool _canaryFirstTickLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForDomainReloadDisabled() => _canaryFirstTickLogged = false;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            MpCanary.Mark($"NetworkedVehicle.OnStartNetwork '{gameObject.name}' owner={OwnerId}");

            var multiplayerRigidbody = _customRigidbody as MultiplayerRigidbodyAdapter;
            if (multiplayerRigidbody != null)
            {
                multiplayerRigidbody.InitializeAdapter();
            }

            EnsureStateMachineDriver();
            EnsureNetworkObjectGraphicalBinding();
            VerifySerializedNetworkBehaviours();

            if (!_subscribedToTimeManager)
            {
                base.TimeManager.OnTick += TimeManager_OnTick;
                base.TimeManager.OnPostTick += TimeManager_OnPostTick;
                _subscribedToTimeManager = true;
            }

            if (base.IsServerInitialized)
            {
                InjectInputProvider();

                if (base.IsClientInitialized)
                    ApplyInputProviderLocally();
            }
            else
            {
                // Pure clients cannot invoke ObserversRpc. Apply locally and wait for server RPC updates.
                ApplyInputProviderLocally();
            }
        }
        public override void OnStartClient()
        {
            if (_customRigidbody != null)
            {
                _customRigidbody.isKinematic = false;
            }

            ApplyVisualSmoothingForOwnership();
        }

        /// <summary>Stops the NetworkTickSmoother, which drifts the graphics away from the body, and restores their authored pose.</summary>
        private void ApplyVisualSmoothingForOwnership()
        {
            var smoothers = GetComponentsInChildren<FishNet.Component.Transforming.Beta.NetworkTickSmoother>(true);

            bool stoppedALiveController = false;
            foreach (var smoother in smoothers)
            {
                if (smoother == null) continue;

                // Disabling the component is not enough: the controller has its own TimeManager callbacks.
                var controller = smoother.SmootherController;
                if (controller != null)
                {
                    controller.StopSmoother();
                    stoppedALiveController = true;
                }
                smoother.enabled = false;
            }

            SnapVisualsToBody();

            // Rigidbody.interpolation stays None: on a predicting client it splits the body from its transform.

            // The smoother may start after us (its own OnStartClient): retry from the tick loop until stopped.
            _visualPresentationApplied = smoothers.Length == 0 || stoppedALiveController;
        }

        private bool _visualPresentationApplied;

        /// <summary>Restores the authored local pose; stopping the smoother does not undo its offset.</summary>
        private void SnapVisualsToBody()
        {
            foreach (var pose in _authoredVisualPoses)
            {
                if (pose.Key == null) continue;
                pose.Key.SetLocalPositionAndRotation(pose.Value.Position, pose.Value.Rotation);
            }
        }

        private readonly struct LocalPose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public LocalPose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        private readonly System.Collections.Generic.Dictionary<Transform, LocalPose> _authoredVisualPoses = new();

        /// <summary>Taken in Awake, before any smoother can displace the children.</summary>
        private void CacheAuthoredVisualPoses()
        {
            foreach (var smoother in GetComponentsInChildren<FishNet.Component.Transforming.Beta.NetworkTickSmoother>(true))
            {
                if (smoother == null) continue;
                Transform t = smoother.transform;
                if (!_authoredVisualPoses.ContainsKey(t))
                    _authoredVisualPoses[t] = new LocalPose(t.localPosition, t.localRotation);
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


        [ObserversRpc(RunLocally = true)]
        public void InjectInputProvider()
        {
            ApplyInputProviderLocally();
        }

        /// <summary>Ownership can arrive after OnStartNetwork: re-run the wiring for the final owner.</summary>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyInputProviderLocally();
            ApplyVisualSmoothingForOwnership();
        }

        private bool _ownerWiringVerified;

        /// <summary>A spawn-order race can leave the owner's NetworkInputProvider without its PlayerInputProvider; rewire it once.</summary>
        private void EnsureOwnerProviderWired()
        {
            if (_ownerWiringVerified) return;

            var nip = GetComponentInParent<NetworkInputProvider>();
            if (nip != null && !nip.HasLocalProvider &&
                GetComponentInParent<PlayerInputProvider>() != null)
            {
                Debug.LogWarning($"[NetworkedVehicle] Owner NetworkInputProvider on '{transform.root.name}' has no " +
                                 "local provider (spawn-order race) — rewiring input chain.");
                ApplyInputProviderLocally();
                nip = GetComponentInParent<NetworkInputProvider>();
            }

            if (nip == null || nip.HasLocalProvider)
                _ownerWiringVerified = true;
        }

        public void ApplyInputProviderLocally()
        {
            var parentPlayerInputProvider = GetComponentInParent<PlayerInputProvider>();
            var parentNetworkInputProvider = GetComponentInParent<NetworkInputProvider>();

            // Server-owned AI keeps its brain's AiInputProvider; TimeManager_OnTick replicates its inputs.
            var aiProvider = GetComponentInParent<AiInputProvider>();
            bool isServerControlledAi = aiProvider != null && (base.Owner == null || !base.Owner.IsValid);
            if (isServerControlledAi && base.IsServerInitialized)
            {
                _serverDrivenAiProvider = aiProvider;
                _coreMovementController.InputProvider = aiProvider;
                return;
            }

            if (parentNetworkInputProvider == null)
            {
                var parent = transform.parent;
                if (parent != null)
                {
                    parentNetworkInputProvider = parent.gameObject.AddComponent<NetworkInputProvider>();
                }
            }

            if (!base.IsOwner)
            {
                _coreMovementController.InputProvider = parentNetworkInputProvider;
                
                if (parentPlayerInputProvider != null)
                {
                    (parentPlayerInputProvider as MonoBehaviour).enabled = false;
                }
            }
            else // Owner
            {
                if (parentNetworkInputProvider != null && parentPlayerInputProvider != null)
                {
                    parentNetworkInputProvider.Initialize(parentPlayerInputProvider);
                }
                
                _coreMovementController.InputProvider = parentNetworkInputProvider ?? (IInputProvider)parentPlayerInputProvider;
                
                if (parentPlayerInputProvider != null)
                {
                    (parentPlayerInputProvider as MonoBehaviour).enabled = true;
                }
            }
        }

        /// <summary>
        /// Attaches the NetworkedStateMachineDriver to the movement Controller. Retried every tick: the
        /// car is only reparented under its player shell after OnStartNetwork.
        /// </summary>
        private void EnsureStateMachineDriver()
        {
            if (_stateMachineDriver != null) return;

            // Ancestors only: the shooting Controller on the shell must keep running from Update.
            var controller = GetComponentInParent<StateMachine.Controller>(true);
            if (controller == null)
            {
                _driverResolveAttempts++;
                if (_driverResolveAttempts == DRIVER_RESOLVE_WARN_TICKS)
                {
                    Debug.LogError($"[NetworkedVehicle] '{gameObject.name}' still has no movement " +
                                   $"StateMachine.Controller ancestor after {DRIVER_RESOLVE_WARN_TICKS} ticks. " +
                                   "The movement state machine will never tick and the car will barely drive.", this);
                }
                return;
            }

            _stateMachineDriver = controller.GetComponent<NetworkedStateMachineDriver>();
            if (_stateMachineDriver == null)
                _stateMachineDriver = controller.gameObject.AddComponent<NetworkedStateMachineDriver>();

            _stateMachineDriver.TakeOverFromUpdate();
            MpCanary.Mark($"state machine driver bound on '{controller.gameObject.name}' " +
                          $"for '{gameObject.name}' after {_driverResolveAttempts} retries");
        }

        private int _driverResolveAttempts;
        private const int DRIVER_RESOLVE_WARN_TICKS = 250; // 5s at 50Hz

        // Same child CarInjector.Install(null, true) binds the camera to.
        private const string VISUALS_CHILD_NAME = "Visuals";

        /// <summary>Points the shell's camera constraints at the interpolated 'Visuals' instead of the car root.</summary>
        private void EnsureCameraFollowsVisuals()
        {
            if (_cameraTargetChecked) return;

            Transform shell = transform.parent;
            if (shell == null) return; // not under its player shell yet; retried every tick

            _cameraTargetChecked = true;
            Transform visuals = transform.Find(VISUALS_CHILD_NAME);
            if (visuals == null) return;

            foreach (var constraint in shell.GetComponentsInChildren<UnityEngine.Animations.IConstraint>(true))
            {
                for (int i = 0; i < constraint.sourceCount; i++)
                {
                    UnityEngine.Animations.ConstraintSource source = constraint.GetSource(i);
                    if (source.sourceTransform != transform) continue;
                    source.sourceTransform = visuals;
                    constraint.SetSource(i, source);
                }
            }
        }

        /// <summary>Binds NetworkObject._graphicalObject only when the car has no NetworkTickSmoother (two smoothers make it tremble).</summary>
        private static System.Reflection.FieldInfo _vehicleGraphicalObjectField;

        private void EnsureNetworkObjectGraphicalBinding()
        {
            var nob = GetComponent<FishNet.Object.NetworkObject>();
            if (nob == null) return;

            if (GetComponentInChildren<FishNet.Component.Transforming.Beta.NetworkTickSmoother>(true) != null)
                return;

            if (_vehicleGraphicalObjectField == null)
            {
                _vehicleGraphicalObjectField = typeof(FishNet.Object.NetworkObject).GetField("_graphicalObject",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            if (_vehicleGraphicalObjectField == null) return;

            Transform current = _vehicleGraphicalObjectField.GetValue(nob) as Transform;
            if (current != null && current != transform) return; // already valid

            Transform target = ResolveGraphicalTarget();
            if (target != null && target != transform)
                _vehicleGraphicalObjectField.SetValue(nob, target);
        }

        private Transform ResolveGraphicalTarget()
        {
            foreach (Transform child in transform)
            {
                if (child != null) return child;
            }
            return null;
        }

        /// <summary>Logs loudly when a prefab ships an empty NetworkBehaviours list: its RPCs would silently no-op.</summary>
        private void VerifySerializedNetworkBehaviours()
        {
            var nob = GetComponent<FishNet.Object.NetworkObject>();
            if (nob == null) return;
            if (nob.NetworkBehaviours == null || nob.NetworkBehaviours.Count == 0)
            {
                Debug.LogError(
                    $"[NetworkedVehicle] '{gameObject.name}' has an empty NetworkBehaviours list on its NetworkObject. " +
                    "FishNet cannot route RPCs or replicate state for this prefab. " +
                    "Open the prefab in Unity and save it, or run Tools > Fish-Networking > Refresh All NetworkObjects.",
                    this);
            }
        }
        #endregion

        #region Prediction
        private void TimeManager_OnTick()
        {
            if (!_canaryFirstTickLogged)
            {
                _canaryFirstTickLogged = true;
                MpCanary.Mark($"first NetworkedVehicle tick ('{gameObject.name}')");
            }

            EnsureStateMachineDriver();
            EnsureCameraFollowsVisuals();

            if (!_visualPresentationApplied && base.IsClientInitialized)
                ApplyVisualSmoothingForOwnership();

            GuardFalseFlipImpulse();

            if (base.IsOwner)
            {
                EnsureOwnerProviderWired();
                BuildMoveData(out MoveData md);
                Replicate(md);
            }
            else if (base.IsServerInitialized)
            {
                if (_serverDrivenAiProvider != null)
                {
                    // Server-owned AI: replicate the brain's real inputs so clients see it move.
                    MoveData aiMd = new MoveData
                    {
                        MoveInput = _serverDrivenAiProvider.MoveInput,
                        HandbrakeInput = _serverDrivenAiProvider.IsBraking,
                        DashInput = _serverDrivenAiProvider.IsDashing,
                        AccelerateInput = _serverDrivenAiProvider.MoveInput.y
                    };
                    Replicate(aiMd);
                }
                else
                {
                    Replicate(default);
                }
            }
            else
            {
                // Spectated car: FishNet only runs its forwarded inputs when Replicate is called every tick.
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

                rd.DashTicksRemaining = _dashTicksRemaining;
                rd.DashDirection = _dashWorldDirection;
                rd.LinearDamping = adapter.linearDamping;

                Rigidbody rb = adapter.PredictionRigidbody.Rigidbody;
                rd.WorldPosition = rb.position;
                rd.WorldRotation = rb.rotation;
                rd.WorldVelocity = rb.linearVelocity;
                rd.WorldAngularVelocity = rb.angularVelocity;

                rd.CurrentSteerAngle = ReadFloat(SingleplayerReflectionCache.CarMovement_CurrentSteerAngle);
                rd.CurrentRbMaxVelocity = ReadFloat(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity);
                rd.IsBeenImpulsed = ReadBool(SingleplayerReflectionCache.CarMovement_IsBeenImpulsed);
                rd.IsDrifting = ReadBool(SingleplayerReflectionCache.CarMovement_IsDrifting);
                rd.DriftDirection = ReadFloat(SingleplayerReflectionCache.CarMovement_DriftDirection, 1f);
                rd.InitialSpeedWhenDrifting = ReadFloat(SingleplayerReflectionCache.CarMovement_InitialSpeedWhenDrifting);
                rd.DriftIsBackwards = ReadBool(SingleplayerReflectionCache.CarMovement_DriftIsBackwards);

                Reconcile(rd);
            }
        }

        private float ReadFloat(System.Reflection.FieldInfo field, float fallback = 0f)
            => field != null && _coreMovementController != null ? (float)field.GetValue(_coreMovementController) : fallback;

        private bool ReadBool(System.Reflection.FieldInfo field)
            => field != null && _coreMovementController != null && (bool)field.GetValue(_coreMovementController);

        private void WriteField(System.Reflection.FieldInfo field, object value)
        {
            if (field != null && _coreMovementController != null)
                field.SetValue(_coreMovementController, value);
        }

        private void BuildMoveData(out MoveData md)
        {
            md = default;
            if (_coreMovementController?.InputProvider == null) return;
            
            var netProvider = _coreMovementController.InputProvider as NetworkInputProvider;
            if (netProvider != null)
            {
                md.MoveInput = netProvider.MoveInput;
                md.HandbrakeInput = netProvider.IsBraking;
                md.DashInput = netProvider.IsDashing;
                md.AccelerateInput = netProvider.IsAccelerating - netProvider.IsReversing;
            }
            else
            {
                var provider = _coreMovementController.InputProvider as PlayerInputProvider;
                if (provider == null) return;
                md.MoveInput = provider.MoveInput;
                md.HandbrakeInput = provider.IsBraking;
                md.DashInput = provider.IsDashing;
                md.AccelerateInput = provider.IsAccelerating - provider.IsReversing;
            }
        }

         [Replicate]
        private void Replicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            bool isReplaying = state.ContainsReplayed();

            // Under PhysicsMode.Unity a replay never steps physics, so anything it adds lands on top of the live ticks.
            if (isReplaying)
                return;

            if (!base.IsOwner)
                md = ContinueLastInputIfMissing(md, state);

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

            if (_stateMachineDriver != null && _stateMachineDriver.IsReady)
            {
                _stateMachineDriver.TickStateMachine(md.GetTick());
            }
            else
            {
                _coreMovementController.ProcessMovementInput();
            }

            TickDeterministicDash(isReplaying);
            RefreshDashSettleWindow();

            _customRigidbody?.Simulate();

            // Clamp at tick cadence too: ticks and replays integrate between the SP FixedUpdate clamps.
            if (_physicsBehaviour != null)
            {
                float maxVelocity = ReadFloat(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity, -1f);
                if (maxVelocity > 0f)
                    _physicsBehaviour.LimitRigidBodySpeed(maxVelocity);
            }
        }

        private MoveData _lastCreatedInput;
        private int _missingInputTicks;
        private const int MAX_CONTINUED_INPUT_TICKS = 5;

        /// <summary>No input for this tick: keep the last real input instead of braking for one tick.</summary>
        private MoveData ContinueLastInputIfMissing(MoveData md, ReplicateState state)
        {
            if (state.ContainsCreated())
            {
                _lastCreatedInput = md;
                _missingInputTicks = 0;
                return md;
            }

            if (++_missingInputTicks > MAX_CONTINUED_INPUT_TICKS)
                return md;

            MoveData continued = _lastCreatedInput;
            continued.SetTick(md.GetTick());
            return continued;
        }

        /// <summary>Applies the SP dash impulse once per tick instead of per frame, so its strength no longer depends on frame rate.</summary>
        private void TickDeterministicDash(bool isReplaying)
        {
            if (_coreMovementController == null) return;

            // Replays neither re-apply the impulse nor count down (a replay skips the CancelDash below).
            if (isReplaying) return;

            bool spDashing = _coreMovementController.IsDashing;
            if (spDashing && !_wasSpDashing)
                OnSpDashStarted();
            else if (!spDashing && _wasSpDashing && _dashTicksRemaining > 0)
                _dashTicksRemaining = 0; // externally cancelled (explosion, egg, push zone…)
            _wasSpDashing = spDashing;

            if (_dashTicksRemaining <= 0)
            {
                // A reconcile can restore a finished count before our tick reached 0: end the SP dash here too.
                if (_wasSpDashing && _coreMovementController.IsDashing)
                {
                    _coreMovementController.CancelDash();
                    _wasSpDashing = false;
                }
                return;
            }

            _customRigidbody.AddForce(_dashWorldDirection * GetDashForce(), ForceMode.Impulse);
            _dashTicksRemaining--;

            var stats = _physicsBehaviour != null ? _physicsBehaviour.StatsController : null;
            if (stats != null)
            {
                float remainingSeconds = _dashTicksRemaining * (float)base.TimeManager.TickDelta;
                _coreMovementController.onDashCooldownUpdate?.Invoke(
                    Mathf.Max(0f, stats.DashTime - remainingSeconds), stats.DashTime);
            }

            if (_dashTicksRemaining == 0 && _coreMovementController.IsDashing)
            {
                // Ends the dash through the SP path; its impulse timer is already stopped.
                _coreMovementController.CancelDash();
                _wasSpDashing = false;
            }
        }

        /// <summary>Keeps the dash window open DashSettleSeconds after our dash ends, until the server's report catches up.</summary>
        private void RefreshDashSettleWindow()
        {
            if (_dashTicksRemaining > 0)
                _dashSettleTicks = DashSettleTicks;
            else if (_dashSettleTicks > 0)
                _dashSettleTicks--;
        }

        private int DashSettleTicks => Mathf.CeilToInt(DashSettleSeconds * base.TimeManager.TickRate);

        private void OnSpDashStarted()
        {
            // Kill the frame-rate-dependent impulse timer before TimerSystem updates it.
            StopSpDashTimer();

            var stats = _physicsBehaviour != null ? _physicsBehaviour.StatsController : null;
            float dashTime = stats != null ? stats.DashTime : DEFAULT_DASH_TIME_SECONDS;

            _dashWorldDirection = transform.forward.normalized;
            _dashTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(dashTime * base.TimeManager.TickRate));
        }

        private void StopSpDashTimer()
        {
            var timerField = SingleplayerReflectionCache.CarMovement_DashTimer;
            var timer = timerField?.GetValue(_coreMovementController) as ITimer;
            if (timer != null && TimerSystem.Instance != null && TimerSystem.Instance.HasTimer(timer))
                TimerSystem.Instance.StopTimer(timer.GetData().ID);
        }

        private float GetDashForce()
        {
            if (!float.IsNaN(_cachedDashForce)) return _cachedDashForce;
            var field = SingleplayerReflectionCache.CarMovement_DashForce;
            _cachedDashForce = field != null ? (float)field.GetValue(_coreMovementController) : 0f;
            return _cachedDashForce;
        }

         [Reconcile]
        private void Reconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            if (IsServerInitialized && !IsHostInitialized)
                return;
            bool restoreGameplayState = !base.IsOwner; // non-owned cars always follow the server; see below
            var adapter = _customRigidbody as MultiplayerRigidbodyAdapter;
            if (adapter != null)
            {
                Rigidbody rb = adapter.PredictionRigidbody.Rigidbody;
                Vector3 poseBeforeCorrection = rb.position;
                Quaternion rotationBeforeCorrection = rb.rotation;

                // Soft reconcile only for our own car; other cars follow the server's state exactly.
                if (base.IsOwner)
                {
                    restoreGameplayState = ApplyServerPose(adapter, rb, rd);
                }
                else
                {
                    // World-space snapshot on the Rigidbody: FishNet's Reconcile() writes the Transform in local space.
                    rb.position = rd.WorldPosition;
                    rb.rotation = rd.WorldRotation;
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = rd.WorldVelocity;
                        rb.angularVelocity = rd.WorldAngularVelocity;
                    }

                    // Drop pending forces and return the pooled PredictionRigidbody, as FishNet's Reconcile() would.
                    adapter.PredictionRigidbody.ClearPendingForces();
                    if (rd.PredictionRigidbody != null && !ReferenceEquals(rd.PredictionRigidbody, adapter.PredictionRigidbody))
                        GameKit.Dependencies.Utilities.ResettableObjectCaches<PredictionRigidbody>.Store(rd.PredictionRigidbody);
                }

                adapter.linearDamping = rd.LinearDamping;

                // Let the drawn car ease into the corrected pose instead of jumping to it.
                if (_visualSmoother != null)
                    _visualSmoother.AbsorbCorrection(rb.position - poseBeforeCorrection,
                        rb.rotation * Quaternion.Inverse(rotationBeforeCorrection));

                // Never write the Transform from rb.position here: RigidbodyState is local-space and that reverts the reconcile.

            }

            // Restore gameplay state only after a real rewind: the server's dash count is a round trip old.
            if (restoreGameplayState)
            {
                _physicsBehaviour.CurrentSpeed = rd.Speed;
                _dashTicksRemaining = rd.DashTicksRemaining;
                _dashWorldDirection = rd.DashDirection;
                WriteField(SingleplayerReflectionCache.CarMovement_CurrentSteerAngle, rd.CurrentSteerAngle);
                WriteField(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity, rd.CurrentRbMaxVelocity);
                WriteField(SingleplayerReflectionCache.CarMovement_IsBeenImpulsed, rd.IsBeenImpulsed);
                WriteField(SingleplayerReflectionCache.CarMovement_IsDrifting, rd.IsDrifting);
                WriteField(SingleplayerReflectionCache.CarMovement_DriftDirection, rd.DriftDirection);
                WriteField(SingleplayerReflectionCache.CarMovement_InitialSpeedWhenDrifting, rd.InitialSpeedWhenDrifting);
                WriteField(SingleplayerReflectionCache.CarMovement_DriftIsBackwards, rd.DriftIsBackwards);
            }
        }

        // Soft reconcile (owner only): compare against the current body; no velocity correction during a dash.
        private const float SnapDistance = 3f;
        private const float SnapAngleDegrees = 60f;
        private const float VelocityBlend = 0.15f;
        private const float RotationBlend = 0.15f;
        private const float PositionBlend = 0.15f;

        // Snap distance while a dash is in flight (ours or the server's).
        private const float DashSnapDistance = 15f;
        private const float DashSettleSeconds = 0.5f;
        private const float DEFAULT_DASH_TIME_SECONDS = 0.5f;

        /// <summary>Returns true if it hard-snapped. During a dash the report trails the car without a real desync, so the snap distance is relaxed.</summary>
        private bool ApplyServerPose(MultiplayerRigidbodyAdapter adapter, Rigidbody rb, ReconcileData rd)
        {
            Vector3 positionError = rd.WorldPosition - rb.position;
            float angleError = Quaternion.Angle(rb.rotation, rd.WorldRotation);

            if (rd.DashTicksRemaining > 0)
                _dashSettleTicks = DashSettleTicks;
            bool dashInFlight = _dashTicksRemaining > 0 || _dashSettleTicks > 0;
            float snapDistance = dashInFlight ? DashSnapDistance : SnapDistance;

            if (positionError.sqrMagnitude > snapDistance * snapDistance || angleError > SnapAngleDegrees)
            {
                // Real desync: snap in world space (RigidbodyState is local to the player root).
                rb.position = rd.WorldPosition;
                rb.rotation = rd.WorldRotation;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = rd.WorldVelocity;
                    rb.angularVelocity = rd.WorldAngularVelocity;
                }
                adapter.PredictionRigidbody.ClearPendingForces();
                if (rd.PredictionRigidbody != null && !ReferenceEquals(rd.PredictionRigidbody, adapter.PredictionRigidbody))
                    GameKit.Dependencies.Utilities.ResettableObjectCaches<PredictionRigidbody>.Store(rd.PredictionRigidbody);
                return true;
            }

            // Return the pooled PredictionRigidbody ourselves on the soft path.
            if (rd.PredictionRigidbody != null && !ReferenceEquals(rd.PredictionRigidbody, adapter.PredictionRigidbody))
                GameKit.Dependencies.Utilities.ResettableObjectCaches<PredictionRigidbody>.Store(rd.PredictionRigidbody);

            if (rb.isKinematic)
                return false;

            if (rd.DashTicksRemaining <= 0 && _dashTicksRemaining <= 0)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, rd.WorldVelocity, VelocityBlend);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, rd.WorldAngularVelocity, VelocityBlend);
            }

            rb.rotation = Quaternion.Slerp(rb.rotation, rd.WorldRotation, RotationBlend);
            rb.position += positionError * PositionBlend;
            return false;
        }

        /// <summary>
        /// Multiplayer-only guard against false flipped-state transitions while grounded and upright.
        /// This prevents intermittent upward impulses from flip recovery when the car is actually stable.
        /// </summary>
        private void GuardFalseFlipImpulse()
        {
            if (!suppressFalseFlipImpulse || _coreMovementController == null || _customRigidbody == null)
                return;

            bool isGrounded = _coreMovementController.IsGrounded();
            bool isNearlyStill = _customRigidbody.linearVelocity.sqrMagnitude <= (stationarySpeedThreshold * stationarySpeedThreshold);
            float uprightDot = Vector3.Dot(transform.up.normalized, Vector3.up);
            bool isUpright = uprightDot >= uprightDotThreshold;

            if (isGrounded && isNearlyStill && isUpright)
            {
                _coreMovementController.StopFlippedTimer();
                if (_coreMovementController.IsFlipped)
                {
                    _coreMovementController.IsFlipped = false;
                }
            }
        }

        [Server]
        public void ApplyExternalForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            if (_customRigidbody == null) return;
            
            _customRigidbody.AddForce(force, mode);
            
            RpcApplyExternalForce(force, mode);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcApplyExternalForce(Vector3 force, ForceMode mode)
        {
        }

        #endregion
    }
}
