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

        #region Orbit Variables
        [Header("Orbit Variables")]
        [Tooltip("Radius of the orbiting spheres")]
        public float orbitRadius = 2f;

        [Tooltip("Height at which the spheres will orbit")]
        public float orbitHeight = 10f; 

        [Tooltip("Speed of rotation of the spheres")]
        public float orbitSpeed = 50f;

        [Tooltip("Object around which the spheres will rotate")]
        public Transform orbitCenter;
        #endregion

        public GameObject croquettePrefab;

        [Header("FMOD Event Reference")]
        public EventReference ssjUltiReference;

        [Header("Stats Controller")]
        [SerializeField] private StatsController _statsController;

        private List<GameObject> _croquetteList = new List<GameObject>();
        private List<float> _croquetteAngleList = new List<float>(); // Ángulos individuales de cada esfera
        #endregion

        private void Start()
        {
            if (orbitCenter == null)
            {
                orbitCenter = transform; // Si no se asigna, usa el propio jugador
            }
        }

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

        private void Update()
        {
            RotateSpheres();
        }

        private void GenerateSpheres(int count)
        {
            ClearSpheres();
            float angleStep = 360f / count; // Espaciado uniforme entre esferas
            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep;
                _croquetteAngleList.Add(angle);
                Vector3 position = CalculateSpherePosition(angle);
                GameObject sphere = Instantiate(croquettePrefab, position, Quaternion.identity);
                sphere.transform.parent = orbitCenter;
                _croquetteList.Add(sphere);
            }
        }

        private Vector3 CalculateSpherePosition(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            float x = orbitCenter.position.x + Mathf.Cos(radians) * orbitRadius;
            float z = orbitCenter.position.z + Mathf.Sin(radians) * orbitRadius;
            float y = orbitCenter.position.y + orbitHeight; //Ajustamos la altura aquí
            return new Vector3(x, y, z);
        }

        private void RotateSpheres()
        {
            for (int i = 0; i < _croquetteList.Count; i++)
            {
                _croquetteAngleList[i] += orbitSpeed * Time.deltaTime;
                Vector3 newPosition = CalculateSpherePosition(_croquetteAngleList[i]);
                _croquetteList[i].transform.position = newPosition;
            }
        }

        public void ConsumeSphere()
        {
            if (_croquetteList.Count > 0)
            {
                GameObject sphere = _croquetteList[0];
                _croquetteList.RemoveAt(0);
                _croquetteAngleList.RemoveAt(0);
                Destroy(sphere);
            }
        }

        private void ClearSpheres()
        {
            foreach (var sphere in _croquetteList)
            {
                Destroy(sphere);
            }
            _croquetteList.Clear();
            _croquetteAngleList.Clear();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("AI"))
            {
                if (_croquetteList.Count > 0)
                {
                    ConsumeSphere();
                    Debug.Log($"Golpeó a {collision.gameObject.name} con más daño y empuje.");
                }
            }
        }

        /// <summary>
        /// Dibuja el círculo de rotación de las esferas en la escena.
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
