using UnityEngine;
using Enums;
using Utilities;
namespace FastAndFractured
{
    public class CharacterKinematicReactionsController : MonoBehaviour
    {
        [Header("Rig Tragets")]
        public Transform SpineTarget;
        public Transform RightArmTarget;
        public Transform LeftArmTarget;
        public Transform HeadTarget;

        [SerializeField] private float maxReactionDuration;
        [SerializeField] private float maxAngleOffset;

        private Quaternion _originalSpineRotation;
        private Quaternion _originalRightArmRotation;
        private Quaternion _originalLeftArmRotation;
        private Quaternion _originalHeadArmRotation;
        private Vector3 _currentImpactOffset;

        private ITimer _impactTimer;

        private void Start()
        {
            _originalSpineRotation = SpineTarget.localRotation;
            _originalRightArmRotation = RightArmTarget.localRotation;
            _originalLeftArmRotation = LeftArmTarget.localRotation;
            _originalHeadArmRotation = HeadTarget.localRotation;
        }

        public void ApplyImpactReaction(Vector3 impactDirection, float force, float baseForce) //force is the force being applied, baseForce is the maxForce that can be applioed
        {
            Vector3 localDirection = transform.InverseTransformDirection(impactDirection); //transforms the vector direction considering the local one
            float intensity = Mathf.Clamp01(force / baseForce);

            float tiltX = localDirection.z * maxAngleOffset * intensity;
            float tiltZ = -localDirection.x * maxAngleOffset * intensity;
            Quaternion targetImpactRotation = Quaternion.Euler(tiltX, 0, tiltZ);
            float currentDuration = maxReactionDuration * intensity;

            if(_impactTimer != null)
            {
                _impactTimer.StopTimer();
                _impactTimer = null;
            }

            _impactTimer = TimerSystem.Instance.CreateTimer(currentDuration, onTimerDecreaseComplete: () =>
            {
                SpineTarget.localRotation = Quaternion.identity;
                _impactTimer = null;
            }, onTimerDecreaseUpdate: (progress) =>
            {
                SpineTarget.localRotation = Quaternion.Slerp(Quaternion.identity, targetImpactRotation, progress);
            });

        }
    }
}

