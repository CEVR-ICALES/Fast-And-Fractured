using FastAndFractured.Utilities;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using Utilities;

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

        public Vector3 linearVelocity { get => PredictionRigidbody.Rigidbody.linearVelocity; set => PredictionRigidbody.Velocity(value); }
        public Vector3 angularVelocity { get => PredictionRigidbody.Rigidbody.angularVelocity; set => PredictionRigidbody.AngularVelocity(value); }

        public float mass { get => _rb.mass; set => _rb.mass = value; }
        public float linearDamping { get => _rb.linearDamping; set => _rb.linearDamping = value; }
        public float angularDamping { get => _rb.angularDamping; set => _rb.angularDamping = value; }

        public bool isKinematic { get => _rb.isKinematic; set => _rb.isKinematic = value; }
        public RigidbodyConstraints constraints { get => _rb.constraints; set => _rb.constraints = value; }
        public new Transform transform => _rb.transform;

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force) => PredictionRigidbody.AddForce(force, mode);
        public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force) => PredictionRigidbody.AddTorque(torque, mode);
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force) => PredictionRigidbody.AddForceAtPosition(force, position, mode);
         
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