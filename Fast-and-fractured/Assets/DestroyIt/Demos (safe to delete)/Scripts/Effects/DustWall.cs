using UnityEngine;

namespace DestroyIt
{
    public class DustWall : MonoBehaviour
    {
        public GameObject playerDustPrefab;     // A dust cloud particle effect that surrounds the player when the shockwave hits.
        public float dustDurationSeconds = 10f; // How long (in seconds) to play the dust effect around a player after being hit by the shockwave.
        public float dustStartDistance = 50f;   // How far away from the player the dust effect starts.
        public Vector3 fixedFromPosition;

        void OnTriggerEnter(Collider collider)
        {
            if (collider.tag == "Player")
            {
                if (playerDustPrefab != null)
                {
                    Transform player = collider.gameObject.transform;
                    GameObject dustCloud = Instantiate(playerDustPrefab, player.position, Quaternion.identity) as GameObject;
                    Follow followScript = dustCloud.AddComponent<Follow>();
                    followScript.isPositionFixed = true;
                    followScript.objectToFollow = player;
                    followScript.facingDirection = FacingDirection.FollowedObject;
                    followScript.fixedFromPosition = fixedFromPosition;
                    followScript.fixedDistance = dustStartDistance;
                    FadeParticleEffect fadeParticleEff = dustCloud.AddComponent<FadeParticleEffect>();
                    fadeParticleEff.delaySeconds = dustDurationSeconds - 2f; // allow for a two-second fadeout time.
                }
            }
        }
    }
}