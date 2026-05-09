using FastAndFractured.Utilities;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using Utilities;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Multiplayer rigidbody adapter  for singleplayer use the other one
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-100)]

    public class MultiplayerRigidbodyAdapter : MonoBehaviour, ICustomRigidbody, IResettable
    {
        public PredictionRigidbody PredictionRigidbody { get; private set; }

        private Rigidbody _rb;
        private bool _isInitialized = false;


         void Awake()
        {
            InitializeAdapter();
        }
        public void InitializeAdapter()
        {
            if (_isInitialized) return;

            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                Debug.LogError("Rigidbody not found the adapter will fail", this);
                return;
            }

            PredictionRigidbody = ResettableObjectCaches<PredictionRigidbody>.Retrieve();
            PredictionRigidbody.Initialize(_rb);
            _isInitialized = true;
        }
    
        private void OnDestroy()
        {
            if (PredictionRigidbody != null)
            {
                ResettableObjectCaches<PredictionRigidbody>.Store(PredictionRigidbody);
                PredictionRigidbody = null;  
            }
        }

        #region   ICustomRigidbody

        public Vector3 position { get => _rb.position; set => _rb.position = value; }
        public Quaternion rotation { get => _rb.rotation; set => _rb.rotation = value; }

        // Apply velocity directly to the Rigidbody (same as singleplayer UnityRigidbodyAdapter).
        // Routing through PredictionRigidbody.Velocity() queues the set, which only applies on
        // Simulate(). FixedUpdate calls LimitRigidBodySpeed between ticks, but the clamp never
        // takes effect until the next Simulate(), allowing accumulated forces to overshoot.
        // Setters silently skip when kinematic — Unity logs a warning and drops the assignment
        // anyway, but our callers (LimitRigidBodySpeed, etc.) shouldn't spam the console.
        public Vector3 linearVelocity
        {
            get => _rb.linearVelocity;
            set { if (!_rb.isKinematic) _rb.linearVelocity = value; }
        }
        public Vector3 angularVelocity
        {
            get => _rb.angularVelocity;
            set { if (!_rb.isKinematic) _rb.angularVelocity = value; }
        }

        public float mass { get => _rb.mass; set => _rb.mass = value; }
        public float linearDamping { get => _rb.linearDamping; set => _rb.linearDamping = value; }
        public float angularDamping { get => _rb.angularDamping; set => _rb.angularDamping = value; }

        public bool isKinematic { get => _rb.isKinematic; set => _rb.isKinematic = value; }
        public bool useGravity { get => _rb.useGravity; set => _rb.useGravity = value; }
        public RigidbodyConstraints constraints { get => _rb.constraints; set => _rb.constraints = value; }
        public new Transform transform => _rb.transform;

        // Apply forces directly to the Rigidbody (same as singleplayer UnityRigidbodyAdapter).
        // Routing through PredictionRigidbody.AddForce() queues forces; they only apply on
        // Simulate(). FixedUpdate calls ApplyDrift/AddForce between ticks, accumulating N frames
        // of drift force that all fire at once in Simulate() — causing extreme speed bursts.
        // Direct application matches singleplayer: each FixedUpdate force is processed by
        // Unity physics in the same physics step it was added.
        // PredictionRigidbody is kept for reconciliation only (position/velocity sync from server).
        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force) => _rb.AddForce(force, mode);
        public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force) => _rb.AddTorque(torque, mode);
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force) => _rb.AddForceAtPosition(force, position, mode);
         
        public void MovePosition(Vector3 position) => _rb.MovePosition(position);
        public void MoveRotation(Quaternion rot) => _rb.MoveRotation(rot);

         public void Simulate()
        {
            PredictionRigidbody.Simulate();
        }

        #endregion

        #region IResettable (FishNet dependency)
        public void InitializeState() { }

        public void ResetState()
        {
            PredictionRigidbody?.ResetState();
        }
        #endregion
    }
}
