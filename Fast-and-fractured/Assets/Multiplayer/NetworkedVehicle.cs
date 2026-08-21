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

            // Extended vehicle state so post-reconcile ticks resume from the server's exact
            // gameplay state instead of only its rigidbody state (dash/drift/steer drive forces
            // on subsequent ticks; without them the owner re-diverges immediately).
            public int DashTicksRemaining;
            public Vector3 DashDirection;
            public float CurrentSteerAngle;
            public float CurrentRbMaxVelocity;
            public bool IsDrifting;
            public float DriftDirection;
            public float InitialSpeedWhenDrifting;
            public bool DriftIsBackwards;
            public float LinearDamping;

            public ReconcileData(PredictionRigidbody prb,float speed)
            {
                PredictionRigidbody = prb;
                _tick = 0;
                Speed = speed;
                DashTicksRemaining = 0;
                DashDirection = Vector3.zero;
                CurrentSteerAngle = 0f;
                CurrentRbMaxVelocity = 0f;
                IsDrifting = false;
                DriftDirection = 1f;
                InitialSpeedWhenDrifting = 0f;
                DriftIsBackwards = false;
                LinearDamping = 0.08f;
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
        private bool _subscribedToTimeManager;

        // Server-owned AI vehicles keep their brain-driven provider (see ApplyInputProviderLocally).
        private AiInputProvider _serverDrivenAiProvider;

        // ---- Deterministic dash (see TickDeterministicDash) ----
        private bool _wasSpDashing;
        private int _dashTicksRemaining;
        private Vector3 _dashWorldDirection;
        private float _cachedDashForce = float.NaN;

        // Mirrors ApplyMovementAction.HandleAutoDeceleration's constant.
        private const float AUTO_DECELERATION_FORCE = 10f;

        [Header("Multiplayer Runtime Guards")]
        [SerializeField] private bool suppressFalseFlipImpulse = true;
        [SerializeField, Range(-1f, 1f)] private float uprightDotThreshold = 0.8f;
        [SerializeField] private float stationarySpeedThreshold = 1.5f;

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

            CacheAuthoredVisualPoses();
        }

        private static bool _canaryFirstTickLogged;

        // Domain reload is disabled in Enter Play Mode options; statics survive between plays.
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

                // Host still needs immediate local provider wiring.
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

        /// <summary>
        /// Stops the NetworkTickSmoother on this vehicle and restores its graphical children.
        ///
        /// The smoother does not converge in this project and leaves the graphics detached from the
        /// body. Measured on a live host+client match (2026-08-12), with the car body itself
        /// perfectly replicated — identical world positions on both peers:
        ///   * remote car: the 'Visuals' child sat 262 m from its body and kept drifting (917 m).
        ///   * owned car:  'Visuals' floated 1.15 m above the body and the turret 1.1 m above its
        ///     authored height — the chassis visibly lifting off the ground while un-smoothed
        ///     children (the *ExtraObjects) stayed on the car, which reads as those objects
        ///     "going their own way".
        /// Raising the spectator teleport threshold did not help; the drift kept growing.
        ///
        /// Stopping it fixes both: every graphical child returns to its authored local pose and
        /// stays there. Note that disabling the MonoBehaviour is NOT enough — the controller
        /// subscribes to TimeManager callbacks itself and keeps moving the graphics — so
        /// StopSmoother() is the call that matters.
        /// </summary>
        private void ApplyVisualSmoothingForOwnership()
        {
            var smoothers = GetComponentsInChildren<FishNet.Component.Transforming.Beta.NetworkTickSmoother>(true);

            bool stoppedALiveController = false;
            foreach (var smoother in smoothers)
            {
                if (smoother == null) continue;

                // StopSmoother is the part that matters: the controller subscribes to TimeManager
                // callbacks of its own, so merely disabling the MonoBehaviour leaves it moving the
                // graphics (measured — the visual kept drifting with the component disabled).
                var controller = smoother.SmootherController;
                if (controller != null)
                {
                    controller.StopSmoother();
                    stoppedALiveController = true;
                }
                smoother.enabled = false;
            }

            SnapVisualsToBody();

            // Deliberately NOT touching Rigidbody.interpolation here. Interpolation on the OWNER
            // looks like the obvious replacement for the smoother, but on a predicting client it
            // desynchronises the body from its own transform, and the camera rides that gap.
            //
            // Measured on a live host+client match (2026-08-12) on the client-owned car, with
            // interpolation forced to Interpolate:
            //     transform.position = (771.10, 267.15, 961.40)   <- grounded, correct
            //     Rigidbody.position = (744.89, 404.14, 894.82)   <- 140 m away, in the air
            // The two alternated every frame, so the car (and the camera constrained to its
            // 'Visuals') was thrown 23.7 m per frame on average, 158 m at worst. Setting it back
            // to None on the same live car closed the gap to 0.02 m and dropped per-frame motion
            // to 0.009 m. The host did not show it: only a client-owner both predicts and takes
            // reconcile corrections, and it is that write that interpolation fights.
            //
            // The prefabs already ship interpolation None (VehicleSmoothingSetupTool) and
            // NetworkedVehicleSetupTests asserts it; leaving it alone at runtime keeps the two
            // in agreement instead of silently overriding the tested setup.

            // The smoother starts itself in its own OnStartClient, which may run AFTER ours; keep
            // re-applying from the tick loop until we have actually stopped a live controller.
            _visualPresentationApplied = smoothers.Length == 0 || stoppedALiveController;
        }

        private bool _visualPresentationApplied;

        /// <summary>
        /// Puts the graphical children back where the prefab places them. Disabling the smoother
        /// stops it moving them, but it does not undo the offset it already introduced, so without
        /// this a car that had already drifted stays drawn in the wrong place for the rest of the
        /// match. Restores the authored local pose (the turret sits above the body, so zeroing it
        /// would be wrong) captured before any smoother could run.
        /// </summary>
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

        /// <summary>
        /// Snapshots the prefab-authored local pose of each smoothed child. Taken in Awake, before
        /// any smoother has had a chance to displace them.
        /// </summary>
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

        // NOTE on the client-only tremble (still OPEN, 2026-08-12).
        //
        // Measured live, both peers, cars standing still (NetworkedVehicleDiagnostics):
        //                       HOST                        CLIENT
        //   owned car  jump     0.016 m                     0.254 m
        //   remote car jump     0.058 m                     0.246 m
        //   body vs transform   0.000 m both cars           0.017/max 0.253 owned,
        //                                                   0.054/max 0.178 remote
        //   reconciles          none (is the server)        41/s, mean 0.00 cm, max 0.01 cm
        //
        // What this rules OUT:
        //   * Prediction diverging. Reconcile corrections are ~0 cm, so the local simulation agrees
        //     with the server; PhysicsMode = TimeManager is NOT needed to fix this.
        //   * An owner-only cause. The REMOTE car on the client shows the same defect, and remote
        //     cars never reconcile — so it is a property of the CLIENT peer, not of ownership.
        //   * Rigidbody interpolation, which is None everywhere (see ApplyVisualSmoothingForOwnership).
        //
        // A Physics.SyncTransforms() hook on OnPostReconcile was tried here and REMOVED: it syncs
        // Transform -> physics engine, the opposite of the direction needed, so at best it does
        // nothing and at worst it pushes the stale Transform back over the reconciled body pose.
        //
        // Still needed: find what writes the Transform without the Rigidbody following, by sampling
        // both in FixedUpdate and in LateUpdate around one of the 25 cm jumps.



        [ObserversRpc(RunLocally = true)]
        public void InjectInputProvider()
        {
            ApplyInputProviderLocally();
        }

        /// <summary>
        /// Ownership can arrive after OnStartNetwork ran the wiring — re-run it with the final
        /// owner so the owner never keeps a non-owner provider setup.
        /// </summary>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyInputProviderLocally();
            // Ownership decides whether this car's visuals are smoothed (see the method's notes).
            ApplyVisualSmoothingForOwnership();
        }

        private bool _ownerWiringVerified;

        /// <summary>
        /// A spawn-order race can leave the root's NetworkInputProvider without its
        /// local PlayerInputProvider (HasLocalProvider == false) on the OWNER. Movement survives
        /// because CarInjector.Install later assigns the PlayerInputProvider directly to the
        /// CarMovementController (BuildMoveData's fallback), but NetworkedShoot reads the
        /// NetworkInputProvider component — which reports every input as false forever.
        /// Verify once per session on the first owned ticks and rewire if broken.
        /// </summary>
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

        /// <summary>
        /// Applies the correct InputProvider locally. Can be called directly (e.g., after CarInjector overwrites).
        /// </summary>
        public void ApplyInputProviderLocally()
        {
            var parentPlayerInputProvider = GetComponentInParent<PlayerInputProvider>();
            var parentNetworkInputProvider = GetComponentInParent<NetworkInputProvider>();

            // Server-owned AI: EnemyAIBrain writes its steering into AiInputProvider. Replacing
            // it with a NetworkInputProvider would feed the car the replicated MoveData instead —
            // but for AI the server IS the input source, and the generic server path replicates
            // default (zeroed) data, so the AI would simply never move. Keep the brain's provider
            // on the server; TimeManager_OnTick feeds its inputs into Replicate so observers
            // still receive the real movement data.
            var aiProvider = GetComponentInParent<AiInputProvider>();
            bool isServerControlledAi = aiProvider != null && (base.Owner == null || !base.Owner.IsValid);
            if (isServerControlledAi && base.IsServerInitialized)
            {
                _serverDrivenAiProvider = aiProvider;
                _coreMovementController.InputProvider = aiProvider;
                return;
            }

            // Create NetworkInputProvider if it doesn't exist
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
                // Non-owner: Use NetworkInputProvider (reads replicated input)
                _coreMovementController.InputProvider = parentNetworkInputProvider;
                
                if (parentPlayerInputProvider != null)
                {
                    (parentPlayerInputProvider as MonoBehaviour).enabled = false;
                }
            }
            else // Owner
            {
                // Owner: Initialize NetworkInputProvider with the local PlayerInputProvider
                if (parentNetworkInputProvider != null && parentPlayerInputProvider != null)
                {
                    parentNetworkInputProvider.Initialize(parentPlayerInputProvider);
                }
                
                // ALWAYS set InputProvider to NetworkInputProvider for consistency
                // BuildMoveData knows how to read from NetworkInputProvider
                _coreMovementController.InputProvider = parentNetworkInputProvider ?? (IInputProvider)parentPlayerInputProvider;
                
                if (parentPlayerInputProvider != null)
                {
                    (parentPlayerInputProvider as MonoBehaviour).enabled = true;
                }
            }
        }

        /// <summary>
        /// Locates the singleplayer MOVEMENT state machine Controller for this vehicle and ensures a
        /// NetworkedStateMachineDriver is attached to it. The driver disables Controller.Update
        /// so the state machine only runs from NetworkedVehicle.Replicate (tick rate, deterministic).
        ///
        /// Must be retried every tick until it succeeds: at OnStartNetwork the car has not been
        /// reparented under its NetworkedPlayer shell yet, so the movement Controller is not an
        /// ancestor and resolution fails. Resolving only once left _stateMachineDriver null while
        /// NetworkedLevelControllerWrapper still disabled the Controller — the movement state
        /// machine was then frozen in its first state forever (no ApplyMovementAction, no air
        /// control, no grounded/air/dash transitions), which is exactly the "slow and clunky,
        /// nothing like singleplayer" driving feel.
        /// </summary>
        private void EnsureStateMachineDriver()
        {
            if (_stateMachineDriver != null) return;

            // Ancestors only. GetComponentInChildren would happily return the SHOOTING Controller
            // that lives on a child of the player shell, which must keep running from Update.
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

        /// <summary>
        /// Binds the NetworkObject's <c>_graphicalObject</c> so FishNet's built-in prediction
        /// smoother knows which transform to interpolate between ticks — BUT ONLY when the vehicle
        /// isn't already smoothing itself with a <see cref="NetworkTickSmoother"/> component.
        ///
        /// All four car prefabs carry NetworkTickSmoother components on their Visuals (and Turret)
        /// child. Those are FishNet's newer standalone smoothers and are the intended, sole source
        /// of between-tick smoothing. Setting <c>_graphicalObject</c> as well spins up the
        /// NetworkObject's OWN PredictionSmoother on top of them — two independent smoothers writing
        /// transforms every frame — which is the visible "trembling" the cars and turrets show.
        /// (The auto-picked target was even the wrong child, PushApplyPoint, a logic anchor.)
        ///
        /// So: if a NetworkTickSmoother exists, leave _graphicalObject at its shipped-empty value
        /// (built-in smoother stays off). Only vehicles WITHOUT one fall back to the auto-bind, which
        /// mirrors NetworkedProjectile.EnsureNetworkObjectGraphicalBinding.
        /// </summary>
        private static System.Reflection.FieldInfo _vehicleGraphicalObjectField;

        private void EnsureNetworkObjectGraphicalBinding()
        {
            var nob = GetComponent<FishNet.Object.NetworkObject>();
            if (nob == null) return;

            // The car self-smooths via NetworkTickSmoother — do not add a second, competing smoother.
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
            // Prefer first child transform (typically the visual mesh root).
            foreach (Transform child in transform)
            {
                if (child != null) return child;
            }
            return null;
        }

        /// <summary>
        /// MariaAntonia/Carme variant prefabs were checked in with empty NetworkBehaviours lists,
        /// which means FishNet has nothing to register and RPCs/state-replication for components
        /// attached here will silently no-op. This produces "still desynced" symptoms after the
        /// rigidbody-prediction-type fix (the rb syncs, but the rest doesn't). Surface this loudly
        /// at runtime so the regression is caught when prefabs are not re-serialized.
        /// </summary>
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

            // Retries until the car has been reparented under its player shell (see the method).
            EnsureStateMachineDriver();

            // The NetworkTickSmoother starts itself in its own OnStartClient, which can run after
            // ours; retry until we have actually stopped a live controller.
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
                    // Server-owned AI: the server is the input authority. Replicating the brain's
                    // real inputs (instead of default/zeroed data) is what makes AI cars move on
                    // every client.
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
                rd.CurrentSteerAngle = ReadFloat(SingleplayerReflectionCache.CarMovement_CurrentSteerAngle);
                rd.CurrentRbMaxVelocity = ReadFloat(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity);
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
            
            // Try NetworkInputProvider first (set by InjectInputProvider), fallback to PlayerInputProvider
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

            // Live tick vs replay split:
            // - Live tick: drive the full state machine. ApplyMovementAction handles ProcessMovementInput,
            //   transitions evaluate (grounded/air/dash), HandleAutoDeceleration kicks in, etc.
            // - Replay: only re-apply ProcessMovementInput. Do NOT re-tick the state machine because
            //   its actions have non-idempotent side effects (TimerSystem.CreateTimer for dashes,
            //   AddForce in HandleAutoDeceleration). Re-running them N times during reconcile replay
            //   creates N timers / N forces -> extreme acceleration on owner-client.
            //   Motor torque setter is idempotent so re-applying input is safe.
            if (!isReplaying && _stateMachineDriver != null && _stateMachineDriver.IsReady)
            {
                _stateMachineDriver.TickStateMachine();
            }
            else
            {
                _coreMovementController.ProcessMovementInput();

                // The state machine doesn't re-tick during replays, so its coasting deceleration
                // (ApplyMovementAction.HandleAutoDeceleration) would be lost from every replayed
                // tick and the owner would mispredict whenever rolling with no throttle.
                if (isReplaying)
                    ApplyReplayAutoDeceleration(md);
            }

            TickDeterministicDash(isReplaying);

            _customRigidbody?.Simulate();

            // SP clamps velocity in CarMovementController.FixedUpdate, but with prediction the
            // rigidbody also integrates inside ticks and replays — steps FixedUpdate never sees.
            // Without clamping here too, the owner's car bursts past MaxSpeed (>100 km/h from a
            // small push on Pepe) because several tick/replay simulations accumulate between
            // FixedUpdate clamps. Same limit, same API, applied at tick cadence.
            if (_physicsBehaviour != null)
            {
                float maxVelocity = ReadFloat(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity, -1f);
                if (maxVelocity > 0f)
                    _physicsBehaviour.LimitRigidBodySpeed(maxVelocity);
            }
        }

        /// <summary>
        /// Replaces the singleplayer dash's per-frame TimerSystem impulses with per-tick impulses.
        ///
        /// The SP dash (CarMovementController.HandleDashWithPhysics) applies
        /// AddForce(dashForce, Impulse) on every TimerSystem frame update, so total dash strength
        /// scales with frame rate: a 144fps host dashes ~2.4x harder than a 60fps client, and the
        /// owner's prediction always diverges from the server mid-dash. We let the SP dash trigger
        /// normally (VFX, rotation lock, max-speed switch, air-dash limit, initial nudge), then
        /// immediately stop its impulse timer and apply the same per-impulse force once per tick
        /// for DashTime seconds — identical on server, owner and observers, and replay-safe.
        /// </summary>
        private void TickDeterministicDash(bool isReplaying)
        {
            if (_coreMovementController == null) return;

            if (!isReplaying)
            {
                bool spDashing = _coreMovementController.IsDashing;
                if (spDashing && !_wasSpDashing)
                    OnSpDashStarted();
                else if (!spDashing && _wasSpDashing && _dashTicksRemaining > 0)
                    _dashTicksRemaining = 0; // externally cancelled (explosion, egg, push zone…)
                _wasSpDashing = spDashing;
            }

            if (_dashTicksRemaining <= 0) return;

            _customRigidbody.AddForce(_dashWorldDirection * GetDashForce(), ForceMode.Impulse);
            _dashTicksRemaining--;

            if (!isReplaying)
            {
                var stats = _physicsBehaviour != null ? _physicsBehaviour.StatsController : null;
                if (stats != null)
                {
                    float remainingSeconds = _dashTicksRemaining * (float)base.TimeManager.TickDelta;
                    _coreMovementController.onDashCooldownUpdate?.Invoke(
                        Mathf.Max(0f, stats.DashTime - remainingSeconds), stats.DashTime);
                }

                if (_dashTicksRemaining == 0 && _coreMovementController.IsDashing)
                {
                    // Ends the dash through the SP path (unblock rotations, restore max speed,
                    // stop VFX, start the cooldown timer). Its impulse timer is already stopped,
                    // so CancelDash just runs FinishDash.
                    _coreMovementController.CancelDash();
                    _wasSpDashing = false;
                }
            }
        }

        private void OnSpDashStarted()
        {
            // Kill the frame-rate-dependent impulse timer before TimerSystem ever updates it
            // (we are inside the same call stack that created it).
            var timerField = SingleplayerReflectionCache.CarMovement_DashTimer;
            var timer = timerField?.GetValue(_coreMovementController) as ITimer;
            if (timer != null && TimerSystem.Instance != null && TimerSystem.Instance.HasTimer(timer))
                TimerSystem.Instance.StopTimer(timer.GetData().ID);

            var stats = _physicsBehaviour != null ? _physicsBehaviour.StatsController : null;
            float dashTime = stats != null ? stats.DashTime : 0.5f;

            _dashWorldDirection = transform.forward.normalized;
            _dashTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(dashTime * base.TimeManager.TickRate));
        }

        private float GetDashForce()
        {
            if (!float.IsNaN(_cachedDashForce)) return _cachedDashForce;
            var field = SingleplayerReflectionCache.CarMovement_DashForce;
            _cachedDashForce = field != null ? (float)field.GetValue(_coreMovementController) : 0f;
            return _cachedDashForce;
        }

        private void ApplyReplayAutoDeceleration(MoveData md)
        {
            if (md.MoveInput.y != 0 || _customRigidbody == null) return;

            Vector3 velocity = _customRigidbody.linearVelocity;
            if (velocity.magnitude > 0.1f)
                _customRigidbody.AddForce(-velocity.normalized * AUTO_DECELERATION_FORCE, ForceMode.Acceleration);
            else
                _customRigidbody.linearVelocity = Vector3.zero;
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
                adapter.linearDamping = rd.LinearDamping;
            }
            _physicsBehaviour.CurrentSpeed = rd.Speed;

            // Restore the gameplay state that drives forces on subsequent ticks so the replay
            // (and the live ticks after it) start from the server's exact vehicle state.
            _dashTicksRemaining = rd.DashTicksRemaining;
            _dashWorldDirection = rd.DashDirection;
            WriteField(SingleplayerReflectionCache.CarMovement_CurrentSteerAngle, rd.CurrentSteerAngle);
            WriteField(SingleplayerReflectionCache.CarMovement_CurrentRbMaxVelocity, rd.CurrentRbMaxVelocity);
            WriteField(SingleplayerReflectionCache.CarMovement_IsDrifting, rd.IsDrifting);
            WriteField(SingleplayerReflectionCache.CarMovement_DriftDirection, rd.DriftDirection);
            WriteField(SingleplayerReflectionCache.CarMovement_InitialSpeedWhenDrifting, rd.InitialSpeedWhenDrifting);
            WriteField(SingleplayerReflectionCache.CarMovement_DriftIsBackwards, rd.DriftIsBackwards);
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

        /// <summary>
        /// Applies an external force to the vehicle. Server-authoritative, synced to clients.
        /// Used for environmental pushes (McChicken, explosions, etc.)
        /// </summary>
        /// <param name="force">Force vector to apply</param>
        /// <param name="mode">Force mode (default Impulse)</param>
        [Server]
        public void ApplyExternalForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            if (_customRigidbody == null) return;
            
            // Apply on server through the adapter (applies directly to Rigidbody)
            _customRigidbody.AddForce(force, mode);
            
            // Notify clients for visual feedback
            RpcApplyExternalForce(force, mode);
        }

        /// <summary>
        /// Client-side external force application for visual sync.
        /// </summary>
        [ObserversRpc(ExcludeServer = true)]
        private void RpcApplyExternalForce(Vector3 force, ForceMode mode)
        {
        }

        #endregion
    }
}
