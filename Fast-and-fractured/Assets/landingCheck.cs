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
        private bool applyForces = false;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if (_rb == null)
            {
                _rb = GetComponent<ICustomRigidbody>();
            }
            _landingTimer = TimerSystem.Instance.CreateTimer(Mathf.Infinity);
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
            _rb.AddForce(force,forceMode);
            _startCheck = true;
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
            _rb.linearVelocity = Vector3.zero;
         //todo    _rb.useGravity = false;
            onLanding?.Invoke(_landingTimer.GetData().CurrentTime);
            _landingTimer.StopTimer();
            applyForces = false;
        }
    }
}
