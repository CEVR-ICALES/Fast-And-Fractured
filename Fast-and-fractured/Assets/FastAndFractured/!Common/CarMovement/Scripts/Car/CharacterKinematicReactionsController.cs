using UnityEngine;
using Enums;
using Utilities;
namespace FastAndFractured
{
    public class CharacterKinematicReactionsController : MonoBehaviour
    {
        [Header("Rig Tragets")]
        public Transform SpineTarget;
        //public Transform RightArmTarget;
        //public Transform LeftArmTarget;
        //public Transform HeadTarget;

        [Header("Continuous Lean Settings")]
        [SerializeField] private float maxLateralLean = 20f;
        [SerializeField] private float maxForwardLean = 8f;
        [SerializeField] private float leanSmoothing = 8f; // bigger value snappier reactions
        [SerializeField] private float maxExpectedLateralAcceleration = 25f; //we normalize the accel considering this value the maxPossibble accel

        [Header("Collision Reaction Settings")]
        [SerializeField] private float maxImpactAngleOffset = 35f;
        [SerializeField] private float firstReactionDuration = 0.2f;
        [SerializeField] private float returnReactionDuration = 0.5f;
        [SerializeField] private float impactReturnSmoothing = 8f;

        private Quaternion _continuousLeanRotation = Quaternion.identity;
        private Quaternion _impactRotation = Quaternion.identity;
        private Quaternion _currentSpineRotation = Quaternion.identity;

        private Vector3 _previousVelocity;
        private Rigidbody _rb;

        private ITimer _impactTimer;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _previousVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            UpdateContinuousLean();
            ComposeFinalRotation();
        }

        private void UpdateContinuousLean()
        {
            if (_rb == null) return;

            Vector3 velocityDelta = (_rb.linearVelocity - _previousVelocity) / Time.deltaTime;
            _previousVelocity = _rb.linearVelocity;

            float lateralAccel = Vector3.Dot(velocityDelta, transform.right);
            float forwardAccel = Vector3.Dot(velocityDelta, transform.forward);

            //lean opposite to lateral force
            float targetLateralLean = Mathf.Clamp(lateralAccel / maxExpectedLateralAcceleration, -1f, 1f) * maxLateralLean;
            //lean back on acceleration, forward on braking
            float targetForwardLean = -Mathf.Clamp(forwardAccel / maxExpectedLateralAcceleration, -1f, 1f) * maxForwardLean;

            Quaternion targetLean = Quaternion.Euler(targetForwardLean, 0f, targetLateralLean);
            _continuousLeanRotation = Quaternion.Slerp(
                _continuousLeanRotation,
                targetLean,
                Time.deltaTime * leanSmoothing
            );
        }

        private void ComposeFinalRotation()
        {
            // base lean + impact offset on top
            Quaternion target = _continuousLeanRotation * _impactRotation;
            _currentSpineRotation = Quaternion.Slerp(_currentSpineRotation, target, Time.deltaTime * leanSmoothing);

            if (SpineTarget != null)
                SpineTarget.localRotation = _currentSpineRotation;
        }

        public void ApplyImpactReaction(Vector3 impactDirection, float force, float baseForce) //force is the force being applied, baseForce is the maxForce that can be applioed
        {
            Vector3 localDir = transform.InverseTransformDirection(impactDirection);
            float intensity = Mathf.Clamp01(force / baseForce);

            float tiltX = localDir.z * maxImpactAngleOffset * intensity;
            float tiltZ = -localDir.x * maxImpactAngleOffset * intensity;
            Quaternion targetImpact = Quaternion.Euler(tiltX, 0f, tiltZ);
            _impactTimer?.StopTimer();
            _impactTimer = null;

            _impactTimer = TimerSystem.Instance.CreateTimer(
                firstReactionDuration,
                TimerDirection.INCREASE,
                onTimerIncreaseComplete: () =>
                {
                    _impactTimer = null;
                    StartImpactReturn(targetImpact);
                },
                onTimerIncreaseUpdate: (progress) =>
                {
                    _impactRotation = Quaternion.Slerp(Quaternion.identity, targetImpact, progress);
                }
            );

        }

        private void StartImpactReturn(Quaternion from)
        {
            _impactTimer = TimerSystem.Instance.CreateTimer(
                returnReactionDuration,
                TimerDirection.INCREASE,
                onTimerIncreaseComplete: () =>
                {
                    _impactRotation = Quaternion.identity;
                    _impactTimer = null;
                },
                onTimerIncreaseUpdate: (progress) =>
                {
                    _impactRotation = Quaternion.Slerp(from, Quaternion.identity, progress);
                }
            );
        }

        private void OnDestroy()
        {
            _impactTimer?.StopTimer();
        }
    }
}

