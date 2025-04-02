using FMODUnity;
using UnityEngine;
using System.Collections.Generic;
using Utilities;

namespace FastAndFractured
{
    public class MariaAntoniaUniqueAbility : BaseUniqueAbility
    {
        #region Variables
        [Tooltip("Multiplier to reduce the cooldown timers (example: 1.5 increases the cooldown timer by 50%")]
        public float cooldownReductionMultiplier;

        [Tooltip("Multiplier to increase the stats (example: 1.2 increases the stats by 20%")]
        public float statBoostMultiplier;

        [Tooltip("Unique ability duration in seconds")]
        public float uniqueAbilityDuration;

        [Tooltip("Number of spheres to spawn around the player")]
        public int numberOfSpheres = 3;

        [Tooltip("Radius of the orbiting spheres")]
        public float orbitRadius = 2f;

        [Tooltip("Height at which the spheres will orbit")]
        public float orbitHeight = 2f;

        [Tooltip("Speed of rotation of the spheres")]
        public float orbitSpeed = 50f;

        [Tooltip("Speed of vertical oscillation")]
        public float verticalOscillationSpeed = 2f;

        [Tooltip("Amplitude of vertical oscillation")]
        public float verticalOscillationAmplitude = 0.5f;

        [Tooltip("Speed of rotation around the X-axis")]
        public float xRotationSpeed = 40f;

        [Tooltip("Object around which the spheres will rotate")]
        public Transform orbitCenter;

        public GameObject spherePrefab;

        public EventReference ssjUltiReference;

        [SerializeField] private StatsController _statsController;

        private List<GameObject> spheres = new List<GameObject>();
        private List<float> sphereAngles = new List<float>();
        #endregion

        /// <summary>
        /// Sets the orbit center to the player's transform if not assigned.
        /// </summary>
        private void Start()
        {
            if (orbitCenter == null)
            {
                orbitCenter = transform;
            }
        }

        /// <summary>
        /// Activates the unique ability, boosting stats and spawning orbiting spheres.
        /// </summary>
        public override void ActivateAbility()
        {
            if (IsAbilityActive || IsOnCooldown)
                return;

            base.ActivateAbility();

            SoundManager.Instance.PlayOneShot(ssjUltiReference, transform.position);

            if (_statsController == null)
            {
                Debug.LogError("Stats Controller not Found");
                return;
            }

            _statsController.TemporalProductStat(Enums.Stats.COOLDOWN_SPEED, cooldownReductionMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.MAX_SPEED, statBoostMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.ACCELERATION, statBoostMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.NORMAL_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.PUSH_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);

            GenerateSpheres(numberOfSpheres);
        }

        /// <summary>
        /// Updates the rotation and movement of the orbiting spheres.
        /// </summary>
        private void Update()
        {
            RotateSpheres();
        }

        /// <summary>
        /// Generates a set number of spheres around the player.
        /// </summary>
        private void GenerateSpheres(int count)
        {
            ClearSpheres();
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep;
                sphereAngles.Add(angle);
                Vector3 position = CalculateSpherePosition(angle, 0);
                GameObject sphere = Instantiate(spherePrefab, position, Quaternion.identity);
                sphere.transform.parent = orbitCenter;
                spheres.Add(sphere);
            }
        }

        /// <summary>
        /// Calculates the position of a sphere based on its angle and time for sinusoidal movement.
        /// </summary>
        private Vector3 CalculateSpherePosition(float angle, float time)
        {
            float radians = angle * Mathf.Deg2Rad;
            float x = orbitCenter.position.x + Mathf.Cos(radians) * orbitRadius;
            float z = orbitCenter.position.z + Mathf.Sin(radians) * orbitRadius;
            float y = orbitCenter.position.y + orbitHeight + Mathf.Sin(time * verticalOscillationSpeed) * verticalOscillationAmplitude;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Rotates the spheres around the player with sinusoidal vertical movement.
        /// </summary>
        private void RotateSpheres()
        {
            float time = Time.time;
            for (int i = 0; i < spheres.Count; i++)
            {
                sphereAngles[i] += orbitSpeed * Time.deltaTime;
                Vector3 newPosition = CalculateSpherePosition(sphereAngles[i], time);
                spheres[i].transform.position = newPosition;
                spheres[i].transform.Rotate(Vector3.right * xRotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Consumes a sphere upon collision, increasing damage and push force.
        /// </summary>
        public void ConsumeSphere()
        {
            if (spheres.Count > 0)
            {
                GameObject sphere = spheres[0];
                spheres.RemoveAt(0);
                sphereAngles.RemoveAt(0);
                Destroy(sphere);
            }
        }

        /// <summary>
        /// Clears all spawned spheres.
        /// </summary>
        private void ClearSpheres()
        {
            foreach (var sphere in spheres)
            {
                Destroy(sphere);
            }
            spheres.Clear();
            sphereAngles.Clear();
        }

        /// <summary>
        /// Detects collision with players or AI and consumes a sphere to increase damage and push force.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("AI"))
            {
                if (spheres.Count > 0)
                {
                    ConsumeSphere();
                    Debug.Log($"Hit {collision.gameObject.name} with increased damage and push force.");
                }
            }
        }

        /// <summary>
        /// Draws a gizmo representing the orbit path of the spheres.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (orbitCenter == null) return;

            Gizmos.color = Color.green;
            Vector3 prevPoint = orbitCenter.position + new Vector3(orbitRadius, orbitHeight, 0);
            int segments = 32;

            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f;
                Vector3 newPoint = orbitCenter.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * orbitRadius, orbitHeight, Mathf.Sin(angle * Mathf.Deg2Rad) * orbitRadius);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
    }
}
