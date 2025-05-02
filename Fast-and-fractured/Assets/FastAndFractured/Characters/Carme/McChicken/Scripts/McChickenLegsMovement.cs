using UnityEngine;
using UnityEngine.Animations.Rigging;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class McChickenLegsMovement : MonoBehaviour, IPausable
    {
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
        private bool _isPaused = false;

        private void Start()
        {
            initialRandomOffset = Random.Range(-offset, offset);
        }

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
        }

        private void FixedUpdate()
        {
            if (_isPaused)
                return;

            if(_isWalking)
            {
                _angle += speed * Time.deltaTime;

                float currentAngle = _angle + phaseOffeset + initialRandomOffset;

                float z = ovalXRadius * Mathf.Cos(currentAngle);
                float y = ovalYRadius * Mathf.Sin(currentAngle);

                transform.localPosition = new Vector3(transform.localPosition.x, y, z);
            } else
            {
                
            }
            
        }

        public void SetIsWalking(bool isWalking)
        {
            _isWalking = isWalking;
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

        public void OnPause()
        {
            _isPaused = true;
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }

}
