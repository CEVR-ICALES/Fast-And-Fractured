using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FastAndFractured
{
    public class McChickenLegsMovement : MonoBehaviour
    {
        [Header("Rigs")]
        [SerializeField] private TwoBoneIKConstraint walkingLegRig;
        [SerializeField] private Transform walkingTarget;
        [SerializeField] private Transform flyingTarget;


        [Header("Oval settings")]
        [SerializeField] private float ovalXRadius;
        [SerializeField] private float ovalYRadius;
        [SerializeField] private float speed;
        [SerializeField] private float phaseOffeset; // offset between legs targets
        [SerializeField] private float offset;

        [Header("Gizmo")]
        public bool showOvalGizmo = true;
        public Color gizmoColor = Color.green;
        public int gizmoSegments = 32;

        private bool _isWalking;
        private float _angle;
        private float initialRandomOffset;

        private void Start()
        {
            initialRandomOffset = Random.Range(-offset, offset);
        }

        private void FixedUpdate()
        {
            if(_isWalking)
            {
                _angle += speed * Time.deltaTime;

                float currentAngle = _angle + phaseOffeset + initialRandomOffset;

                float z = ovalXRadius * Mathf.Cos(currentAngle);
                float y = ovalYRadius * Mathf.Sin(currentAngle);

                transform.localPosition = new Vector3(transform.localPosition.x, y, z);
            }
            
        }

        public void SetIsWalking(bool isWalking)
        {
            _isWalking = isWalking;
            var rigData = walkingLegRig.data;
            if(_isWalking)
            {
                rigData.target = walkingTarget;
            }
            else
            {
                rigData.target = flyingTarget;
            }

            walkingLegRig.data = rigData;
        }

        void OnDrawGizmos()
        {
            if (!showOvalGizmo) return;

            Gizmos.color = gizmoColor;
            Vector3 center = transform.parent != null ? transform.parent.position : Vector3.zero;
            Vector3 prevPoint = Vector3.zero;

            for (int i = 0; i <= gizmoSegments; i++)
            {
                float segmentAngle = (float)i / gizmoSegments * Mathf.PI * 2f;
                float z = ovalXRadius * Mathf.Cos(segmentAngle);
                float y = ovalYRadius * Mathf.Sin(segmentAngle);
                Vector3 currentPoint;
                currentPoint = center + new Vector3(0, y, z);

                if (i > 0)
                {
                    Gizmos.DrawLine(prevPoint, currentPoint);
                }
                prevPoint = currentPoint;
            }
        }
    }

}
