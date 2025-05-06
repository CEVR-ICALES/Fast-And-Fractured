using System;
using FMODUnity;
using UnityEngine;
using System.Collections.Generic;
using Utilities;

namespace FastAndFractured
{
    public class MariaAntoniaUniqueAbility : BaseUniqueAbility
    {
        private const string MATERIAL_EMISSIVE = "_EmissiveExposureWeight";
        #region Variables
        [Tooltip("Multiplier to reduce the cooldown timers (example: 1.5 increases the cooldown timer by 50%)")]
        public float cooldownReductionMultiplier;

        [Tooltip("Multiplier to increase the stats (example: 1.2 increases the stats by 20%)")]
        public float statBoostMultiplier;

        [Tooltip("Unique ability duration in seconds")]
        public float uniqueAbilityDuration;

        [Tooltip("Number of croquettes to spawn around the player")]
        public int numberOfCroquettes = 3;

        [Tooltip("Radius of the orbiting croquettes")]
        public float orbitRadius = 2f;

        [Tooltip("Height at which the croquettes will orbit")]
        public float orbitHeight = 2f;

        [Tooltip("Speed of rotation of the croquettes")]
        public float orbitSpeed = 50f;

        [Tooltip("Speed of vertical oscillation")]
        public float verticalOscillationSpeed = 2f;

        [Tooltip("Amplitude of vertical oscillation")]
        public float verticalOscillationAmplitude = 0.5f;

        [Tooltip("Speed of rotation around the X-axis")]
        public float xRotationSpeed = 40f;

        [Tooltip("Object around which the croquettes will rotate")]
        public Transform orbitCenter;

        [Tooltip("Parent object that contains all particle systems for the VFX")]
        [SerializeField] private GameObject vfxParent;

        [Tooltip("The material which will have its emissive texture modified")]
        [SerializeField] private Material hairMaterial;


        // Store the original Exposure Weight value
        private float _originalExposureWeight;

        // Value to set when the ultimate ability is active
        private float _activeExposureWeight = 0f;

        public GameObject croquettePrefab;

        public EventReference ssjUltiReference;

        [SerializeField] private StatsController statsController;

        private ITimer _timer;

        private List<GameObject> _croquetteList = new List<GameObject>();
        private List<float> _croquetteAngleList = new List<float>();
        #endregion

        /// <summary>
        /// Sets the orbit center to the player's transform if not assigned.
        /// Also stores the original exposure weight from the hair material.
        /// </summary>
        private void Start()
        {
            if (orbitCenter == null)
                orbitCenter = transform;

            if (hairMaterial != null)
                _originalExposureWeight = hairMaterial.GetFloat(MATERIAL_EMISSIVE);
                
            StartCooldown();
        }

        private void Update()
        {
            RotateCroquettes();
        }

        public override bool ActivateAbility()
        {
            if (!base.ActivateAbility())
                return false;

            base.ActivateAbility();

            SoundManager.Instance.PlayOneShot(ssjUltiReference, transform.position);

            if (statsController == null)
            {
                Debug.LogError("Stats Controller not Found");
                return false;
            }

            statsController.TemporalProductStat(Enums.Stats.COOLDOWN_SPEED, cooldownReductionMultiplier, uniqueAbilityDuration);
            statsController.TemporalProductStat(Enums.Stats.MAX_SPEED, statBoostMultiplier, uniqueAbilityDuration);
            statsController.TemporalProductStat(Enums.Stats.ACCELERATION, statBoostMultiplier, uniqueAbilityDuration);
            statsController.TemporalProductStat(Enums.Stats.NORMAL_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
            statsController.TemporalProductStat(Enums.Stats.PUSH_FORCE, statBoostMultiplier, uniqueAbilityDuration);

            GenerateCroquettes(numberOfCroquettes);
            StartVFX();
            ActivateHairEmission(true); 
            _timer = TimerSystem.Instance.CreateTimer(
                duration: uniqueAbilityDuration,
                onTimerDecreaseComplete: () =>
                {
                    StopVFX();
                    ActivateHairEmission(false);
                    SoundManager.Instance.StopSound(ssjUltiReference);
                    EndAbilityEffects();
                });
            return true;
        }

        private void OnDestroy()
        {
            SoundManager.Instance?.StopSound(ssjUltiReference);
        }

        #region Croquette Methods
        /// <summary>
        /// Generates a set number of croquettes around the player.
        /// </summary>
        /// <param name="count">Number of croquettes that will be instantiated</param>
        private void GenerateCroquettes(int count)
        {
            ClearCroquettes();
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep;
                _croquetteAngleList.Add(angle);
                Vector3 position = CalculateCroquettePosition(angle, 0);
                GameObject croquette = Instantiate(croquettePrefab, position, Quaternion.identity);
                croquette.transform.parent = orbitCenter;
                _croquetteList.Add(croquette);
            }
        }

        /// <summary>
        /// Calculates the position of a croquette based on its angle and time for sinusoidal movement.
        /// </summary>
        private Vector3 CalculateCroquettePosition(float angle, float time)
        {
            float radians = angle * Mathf.Deg2Rad;
            float x = orbitCenter.position.x + Mathf.Cos(radians) * orbitRadius;
            float z = orbitCenter.position.z + Mathf.Sin(radians) * orbitRadius;
            float y = orbitCenter.position.y + orbitHeight + Mathf.Sin(time * verticalOscillationSpeed) * verticalOscillationAmplitude;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Rotates the croquettes around the player with sinusoidal vertical movement.
        /// </summary>
        private void RotateCroquettes()
        {
            float time = Time.time;
            for (int i = 0; i < _croquetteList.Count; i++)
            {
                _croquetteAngleList[i] += orbitSpeed * Time.deltaTime;
                Vector3 newPosition = CalculateCroquettePosition(_croquetteAngleList[i], time);
                _croquetteList[i].transform.position = newPosition;
                _croquetteList[i].transform.Rotate(Vector3.right * xRotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Consumes a croquette upon collision, increasing damage and push force.
        /// </summary>
        public void ConsumeCroquette()
        {
            if (_croquetteList.Count > 0)
            {
                GameObject croquette = _croquetteList[0];
                _croquetteList.RemoveAt(0);
                _croquetteAngleList.RemoveAt(0);
                Destroy(croquette);
            }
        }

        /// <summary>
        /// Clears all spawned croquettes.
        /// </summary>
        private void ClearCroquettes()
        {
            foreach (var croquette in _croquetteList)
            {
                Destroy(croquette);
            }
            _croquetteList.Clear();
            _croquetteAngleList.Clear();
        }

        /// <summary>
        /// Draws a gizmo representing the orbit path of the croquettes.
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
        #endregion

        #region VFX and Particles Methods
        /// <summary>
        /// Starts the special ability visual effects.
        /// </summary>
        public void StartVFX()
        {
            if (vfxParent != null)
            {
                foreach (ParticleSystem ps in vfxParent.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Play();
                }
            }
        }

        /// <summary>
        /// Stops the visual effects of the special ability when it ends.
        /// </summary>
        public void StopVFX()
        {
            if (vfxParent != null)
            {
                foreach (ParticleSystem ps in vfxParent.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Stop();
                }
            }
        }


        /// <summary>
        /// Activates or deactivates the hair emissive effect by modifying the Exposure Weight.
        /// When activated, sets Exposure Weight to activeExposureWeight (should be 1), and when deactivated, restores the original value.
        /// </summary>
        private void ActivateHairEmission(bool isActive)
        {
            if (hairMaterial != null)
            {
                if (isActive)
                {
                    hairMaterial.SetFloat(MATERIAL_EMISSIVE, _activeExposureWeight);
                    DynamicGI.UpdateEnvironment();
                }
                else
                {
                    hairMaterial.SetFloat(MATERIAL_EMISSIVE, _originalExposureWeight);
                    DynamicGI.UpdateEnvironment();
                }
            }
        }
        #endregion
    }
}
