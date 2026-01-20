using UnityEngine;
using Enums;
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

        private Quaternion _originalSpineRotation;
        private Quaternion _originalRightArmRotation;
        private Quaternion _originalLeftArmRotation;
        private Quaternion _originalHeadArmRotation;
        private Vector3 _currentImpactOffset;

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
        }
    }
}

