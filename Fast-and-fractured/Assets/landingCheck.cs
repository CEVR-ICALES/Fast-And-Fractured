using FastAndFractured.Utilities;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
namespace FastAndFractured
{
    public class landingCheck : MonoBehaviour
    {
        public UnityEvent<float> onLanding;
        private ICustomRigidbody _rb;
        private bool _startCheck = false;
        private ITimer _landingTimer;
        [SerializeField]
        private float customGravity = 16.8f;
        [SerializeField]
        private float airFriction = 9.8f;
        private bool applyForces;
        [SerializeField]
        private LayerMask ignoreLayer;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if (_rb == null)
            {
                _rb = GetComponent<ICustomRigidbody>();
            }
            _landingTimer = TimerSystem.Instance.CreateTimer(100,Enums.TimerDirection.INCREASE);
        }

        private void FixedUpdate()
        {
            if (applyForces)
            {
               _rb.AddForce(Vector3.down * customGravity, ForceMode.Acceleration);
                _rb.AddForce(-_rb.linearVelocity.normalized * airFriction, ForceMode.Acceleration);
            }
        }

        public void StartChecking(Vector3 force,ForceMode forceMode)
        {
            if (_rb == null)
            {
                _rb = GetComponent<ICustomRigidbody>();
            }
            // Guard: when the trampoline prefab is placed without an ICustomRigidbody adapter on
            // its landingCheck child (common after networked variants strip components), the old
            // unguarded AddForce threw an NRE on every scene load. Silently skip — there is no
            // meaningful work to do without a rigidbody to push.
            if (_rb == null) return;
            _rb.AddForce(force,forceMode);
            _startCheck = true;
            _rb.useGravity = true;
            applyForces = true;
        }

        public ICustomRigidbody GetRigidbody()
        {
            if (_rb == null)
            {
                _rb = GetComponent<ICustomRigidbody>();
            }
            return _rb;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Same guard as StartChecking — without an ICustomRigidbody we have nothing to land
            // onto and the original code would NRE on the first physics tick after spawn.
            if (_rb == null) return;
            if (!((ignoreLayer & 1 << other.gameObject.layer) == 1 << other.gameObject.layer)) {
                _rb.linearVelocity = Vector3.zero;
                _rb.useGravity = false;
                onLanding?.Invoke(_landingTimer.GetData().CurrentTime);
                _landingTimer.StopTimer();
                applyForces = false;
            }
        }
    }
}
