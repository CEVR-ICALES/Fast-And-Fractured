using Unity.Cinemachine;
using UnityEngine;

namespace FastAndFractured
{
    public class MapIntroShot : MonoBehaviour
    {
        [Tooltip("Camera pose at the start of the shot. Move and rotate it in the scene view.")]
        [SerializeField] private Transform from;
        [Tooltip("Camera pose at the end of the shot.")]
        [SerializeField] private Transform to;
        [Tooltip("Camera moved by this shot. The Cinemachine track of the timeline decides which shot is on screen.")]
        [SerializeField] private CinemachineCamera shotCamera;
        [Tooltip("How the camera travels from From to To over the clip (x = clip progress, y = travel).")]
        [SerializeField] private AnimationCurve travel = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private float fieldOfViewFrom = 50f;
        [SerializeField] private float fieldOfViewTo = 50f;

        [Header("Local player shot")]
        [Tooltip("On: when the intro starts this shot is moved onto the local player's car (From/To become offsets around it); the camera orbits from From to To looking at the car.")]
        [SerializeField] private bool followLocalPlayer;
        [Tooltip("Height above the car's pivot the camera looks at.")]
        [SerializeField] private float lookAtHeight = 1.5f;
        [Tooltip("Layers the camera must stay above, so it does not dip under the terrain at low spawn points.")]
        [SerializeField] private LayerMask groundLayers;
        [Tooltip("Minimum height (m) of the camera over the ground.")]
        [SerializeField] private float minHeightOverGround = 2f;
        [Tooltip("Layers that block the view of the car (walls, buildings): the camera moves in front of them.")]
        [SerializeField] private LayerMask obstacleLayers;

        private const float GROUND_PROBE_HEIGHT = 60f;
        private const float OBSTACLE_MARGIN = 0.6f;
        private const int MAX_OBSTACLE_HITS = 16;

        private readonly RaycastHit[] _hits = new RaycastHit[MAX_OBSTACLE_HITS];

        private Transform _target;

        public bool FollowsLocalPlayer => followLocalPlayer;

        public void AttachTo(Transform car)
        {
            if (!followLocalPlayer || car == null) return;
            _target = car;
            transform.SetPositionAndRotation(car.position, Quaternion.Euler(0f, car.eulerAngles.y, 0f));
        }

        public void Evaluate(float progress)
        {
            if (from == null || to == null || shotCamera == null) return;
            float t = travel.Evaluate(Mathf.Clamp01(progress));
            Vector3 position = Vector3.LerpUnclamped(from.position, to.position, t);
            Quaternion rotation = Quaternion.SlerpUnclamped(from.rotation, to.rotation, t);
            if (_target != null)
            {
                Vector3 lookAt = _target.position + Vector3.up * lookAtHeight;
                position = KeepInView(lookAt, KeepAboveGround(Orbit(from.position, to.position, t)));
                if (lookAt != position) rotation = Quaternion.LookRotation(lookAt - position);
            }
            shotCamera.transform.SetPositionAndRotation(position, rotation);
            shotCamera.Lens.FieldOfView = Mathf.LerpUnclamped(fieldOfViewFrom, fieldOfViewTo, t);
        }

        private Vector3 Orbit(Vector3 start, Vector3 end, float t)
        {
            Vector3 center = _target.position;
            Vector3 a = start - center, b = end - center;
            float yaw = Mathf.LerpAngle(Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg, Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
            float distance = Mathf.Lerp(new Vector2(a.x, a.z).magnitude, new Vector2(b.x, b.z).magnitude, t);
            return center + new Vector3(Mathf.Sin(yaw) * distance, Mathf.Lerp(a.y, b.y, t), Mathf.Cos(yaw) * distance);
        }

        private Vector3 KeepAboveGround(Vector3 position)
        {
            Vector3 probe = position + Vector3.up * GROUND_PROBE_HEIGHT;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, GROUND_PROBE_HEIGHT * 2f, groundLayers, QueryTriggerInteraction.Ignore))
                position.y = Mathf.Max(position.y, hit.point.y + minHeightOverGround);
            return position;
        }

        private Vector3 KeepInView(Vector3 lookAt, Vector3 position)
        {
            Vector3 toCamera = position - lookAt;
            float distance = toCamera.magnitude;
            if (distance <= OBSTACLE_MARGIN) return position;
            Vector3 direction = toCamera / distance;
            int count = Physics.RaycastNonAlloc(lookAt, direction, _hits, distance, obstacleLayers, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
                if (!_hits[i].collider.transform.IsChildOf(_target.root) && _hits[i].distance < nearest)
                    nearest = _hits[i].distance;
            return nearest < distance ? lookAt + direction * Mathf.Max(nearest - OBSTACLE_MARGIN, OBSTACLE_MARGIN) : position;
        }

        private void OnDrawGizmos()
        {
            if (from == null || to == null) return;
            Gizmos.color = new Color(1f, 0.78f, 0.35f);
            Gizmos.DrawLine(from.position, to.position);
            DrawPose(from, fieldOfViewFrom);
            DrawPose(to, fieldOfViewTo);
        }

        private static void DrawPose(Transform pose, float fieldOfView)
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 12f, 0.5f, 16f / 9f);
            Gizmos.matrix = previous;
        }
    }
}
