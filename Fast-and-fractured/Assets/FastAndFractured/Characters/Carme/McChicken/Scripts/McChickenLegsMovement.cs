using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace FastAndFractured
{
    public class McChickenLegsMovement : MonoBehaviour
    {
        [Header("Oval settings")]
        [SerializeField] private float ovalXRadius;
        [SerializeField] private float ovalYRadius;
        [SerializeField] private float speed;
        [SerializeField] private float phaseOffeset; // offset between leg targets
        [SerializeField] private float offset;

        [Header("Gizmo")]
        public bool showOvalGizmo = true;
        public Color gizmoColor = Color.green;
        public int gizmoSegments = 32;

        private float _angle;
        private float initialRandomOffset;

        private void Start()
        {
            initialRandomOffset = Random.Range(-offset, offset);
        }

        private void FixedUpdate()
        {
            _angle += speed * Time.deltaTime;

            float currentAngle = _angle + phaseOffeset + initialRandomOffset;

            float z = ovalXRadius * Mathf.Cos(currentAngle);
            float y = ovalYRadius * Mathf.Sin(currentAngle);

            transform.localPosition = new Vector3(transform.localPosition.x, y, z);
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
