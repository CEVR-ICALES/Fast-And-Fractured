using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Server: points EnemyAIBrain's Player at the nearest human car (LocalPlayer is always the host's).</summary>
    [DisallowMultipleComponent]
    public class NetworkedAiPlayerTargeting : MonoBehaviour
    {
        private const float RETARGET_INTERVAL = 1f;

        private EnemyAIBrain _brain;
        private float _nextRetarget;

        public void Configure(EnemyAIBrain brain) => _brain = brain;

        private void Update()
        {
            if (_brain == null || Time.time < _nextRetarget) return;
            _nextRetarget = Time.time + RETARGET_INTERVAL;

            if (!LevelController.HasInstance) return;

            Vector3 origin = _brain.transform.position;
            GameObject nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (GameObject character in LevelController.Instance.InGameCharacters)
            {
                if (character == null || !character.activeInHierarchy) continue;

                NetworkObject nob = character.GetComponentInParent<NetworkObject>(true);
                if (nob == null || nob.Owner == null || !nob.Owner.IsValid) continue;

                float sqr = (character.transform.position - origin).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = character;
                }
            }

            if (nearest != null && _brain.Player != nearest)
                _brain.Player = nearest;
        }
    }
}
