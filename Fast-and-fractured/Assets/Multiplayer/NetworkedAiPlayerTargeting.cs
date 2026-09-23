using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-only. EnemyAIBrain's "go for the player" decision targets
    /// LevelController.LocalPlayer, which on the server is always the host's car - so in a match
    /// every AI focused the host and never treated a client as "the player". Keeps the brain's
    /// Player pointed at the nearest human-controlled car still in the match instead.
    /// </summary>
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
