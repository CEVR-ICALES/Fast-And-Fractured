using Unity.Cinemachine;
using UnityEngine;

namespace FastAndFractured
{
    public struct DebugFreeCameraPose
    {
        public Vector3 position;
        public float yaw;
        public float pitch;

        public Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);
    }

    public sealed class DebugFreeCamera
    {
        public const float DEFAULT_SPEED = 20f;
        public const float MIN_SPEED = 1f;
        public const float MAX_SPEED = 500f;
        public const float FAST_FACTOR = 4f;
        public const float SLOW_FACTOR = 0.25f;
        public const float MAX_PITCH = 89f;
        public const float MIN_FIELD_OF_VIEW = 10f;
        public const float MAX_FIELD_OF_VIEW = 120f;
        private const float SPEED_STEP = 1.2f;
        private const float HALF_TURN = 180f;
        private const float FULL_TURN = 360f;

        private Camera _camera;
        private Behaviour _brain;
        private bool _brainWasEnabled;
        private float _authoredFieldOfView;
        private DebugFreeCameraPose _pose;

        public bool Active { get; private set; }
        public float Speed { get; set; } = DEFAULT_SPEED;
        public float FieldOfView { get; set; }
        public DebugFreeCameraPose Pose => _pose;

        public bool Enter(Camera camera)
        {
            if (Active || camera == null) return false;
            _camera = camera;
            _brain = camera.GetComponent<CinemachineBrain>();
            _brainWasEnabled = _brain != null && _brain.enabled;
            if (_brain != null) _brain.enabled = false;
            _authoredFieldOfView = camera.fieldOfView;
            FieldOfView = camera.fieldOfView;
            _pose = PoseOf(camera.transform);
            Active = true;
            return true;
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            if (_camera != null) _camera.fieldOfView = _authoredFieldOfView;
            if (_brain != null) _brain.enabled = _brainWasEnabled;
            _camera = null;
            _brain = null;
        }

        public void Tick(Vector3 move, Vector2 look, float speedFactor, float seconds)
        {
            if (!Active) return;
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                Exit();
                return;
            }
            _pose = Fly(_pose, move, look, Speed * speedFactor, seconds);
            _camera.transform.SetPositionAndRotation(_pose.position, _pose.Rotation);
            _camera.fieldOfView = Mathf.Clamp(FieldOfView, MIN_FIELD_OF_VIEW, MAX_FIELD_OF_VIEW);
        }

        public void ChangeSpeed(float notches) => Speed = SpeedAfter(Speed, notches);

        public static DebugFreeCameraPose PoseOf(Transform transform)
        {
            Vector3 angles = transform.eulerAngles;
            float pitch = angles.x > HALF_TURN ? angles.x - FULL_TURN : angles.x;
            return new DebugFreeCameraPose
            {
                position = transform.position,
                yaw = angles.y,
                pitch = Mathf.Clamp(pitch, -MAX_PITCH, MAX_PITCH)
            };
        }

        public static DebugFreeCameraPose Fly(DebugFreeCameraPose pose, Vector3 move, Vector2 look, float speed, float seconds)
        {
            pose.yaw = Mathf.Repeat(pose.yaw + look.x, FULL_TURN);
            pose.pitch = Mathf.Clamp(pose.pitch - look.y, -MAX_PITCH, MAX_PITCH);
            Quaternion rotation = pose.Rotation;
            Vector3 direction = rotation * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y;
            pose.position += Vector3.ClampMagnitude(direction, 1f) * (speed * seconds);
            return pose;
        }

        public static float SpeedAfter(float speed, float notches) =>
            Mathf.Clamp(speed * Mathf.Pow(SPEED_STEP, notches), MIN_SPEED, MAX_SPEED);

        public static float SpeedFactor(bool fast, bool slow) => fast ? FAST_FACTOR : slow ? SLOW_FACTOR : 1f;
    }
}
