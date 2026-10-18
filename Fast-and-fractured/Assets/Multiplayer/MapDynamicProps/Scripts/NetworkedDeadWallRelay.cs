using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Forwards one legacy dead-wall collider's triggers to the server-authoritative NetworkedDeadWallsBehaviour.</summary>
    [DisallowMultipleComponent]
    internal sealed class NetworkedDeadWallRelay : MonoBehaviour
    {
        private NetworkedDeadWallsBehaviour _owner;
        private float _killTime = 1.5f;
        private float _frontAngle = 180f;

        private static readonly System.Reflection.FieldInfo FrontAngleField =
            typeof(DeadWallsBehaivour).GetField("frontAngle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        internal float KillTime => _killTime;

        internal void Configure(NetworkedDeadWallsBehaviour owner, DeadWallsBehaivour wall)
        {
            _owner = owner;
            if (wall == null)
                return;

            // Copied before the legacy component is destroyed (disabled components still get trigger callbacks).
            _killTime = wall.KillTime;
            if (FrontAngleField != null)
                _frontAngle = (float)FrontAngleField.GetValue(wall);
        }

        internal bool IsCharacterInFront(Transform target)
        {
            if (target == null)
                return false;

            Vector3 directionToTarget = (target.position - transform.position).normalized;
            float dotProduct = Mathf.Clamp(Vector3.Dot(transform.forward, directionToTarget), -1f, 1f);
            float angleToTarget = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
            return angleToTarget < (_frontAngle * 0.5f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null)
                _owner.HandleWallEnter(this, other, GetInstanceID());
        }

        private void OnTriggerExit(Collider other)
        {
            if (_owner != null)
                _owner.HandleWallExit(this, other, GetInstanceID());
        }
    }
}
