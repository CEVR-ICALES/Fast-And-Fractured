using UnityEngine;

namespace FastAndFractured
{
    public class VehicleVfxController : MonoBehaviour
    {
        [Header("Car Movement Trail")]
        [SerializeField] private ParticleSystem[] trailVfxs;
        [SerializeField] private float movementThreshold;
        [SerializeField] private float speedToEmitAllParticles;
        private bool _carTrailParticlesActive = true;
        [SerializeField] private float _movementTrailInitialEmmisionRate = 300f; // shopuld always match the emissio nrate if its going to be modified
        private ParticleSystem.EmissionModule[] _trailEmissionModules; // necessary to modify rateOverTime

        [Header("Drift Trail")]
        [SerializeField] private TrailRenderer[] tyreDriftMarksVfx;

        [Header("Impact")]
        [SerializeField] private ParticleSystem collisionVfx;
        [SerializeField] private LayerMask collisionLayers;


        PhysicsBehaviour _physicsBehaviour;
        CarMovementController _carMovementController;
        private float _currentSpeed;

        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            _carMovementController = GetComponent<CarMovementController>();

            _trailEmissionModules = new ParticleSystem.EmissionModule[trailVfxs.Length];

            for (int i = 0; i < trailVfxs.Length; i++)
            {
                _trailEmissionModules[i] = trailVfxs[i].emission;
            }
        }

        private void FixedUpdate()
        {
            _currentSpeed = _physicsBehaviour.Rb.velocity.magnitude;
            HandleTrailParticleSystem();
            HandleBrakeMarks();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collisionLayers == (collisionLayers | (1 << collision.gameObject.layer)))
            {
                HandleCollisionVfx(collision);
            }
        }

        private void HandleCollisionVfx(Collision collision) // if we pool this type of particle since its likely that a lot of impacts are going to happen this will be changed
        {
            ContactPoint contact = collision.GetContact(0);
            collisionVfx.transform.position = contact.point;
            collisionVfx.transform.rotation = Quaternion.LookRotation(contact.normal);

            if (!collisionVfx.isPlaying)
                collisionVfx.Play();
        }

        #region BrakeMarks
        private void HandleBrakeMarks()
        {
            if (_carMovementController.IsBraking)
            {
                StartBrakeMark();
            }
            else
            {
                StopBrakeMark();
            }
        }

        private void StartBrakeMark()
        {
            foreach (TrailRenderer trail in tyreDriftMarksVfx)
            {
                trail.emitting = true;
            }
        }

        private void StopBrakeMark()
        {
            foreach (TrailRenderer trail in tyreDriftMarksVfx)
            {
                trail.emitting = false;
            }
        }

        #endregion

        #region CarTrail

        private void HandleTrailParticleSystem()
        {
            if (!_carMovementController.IsGrounded())
            {
                StopParticles(trailVfxs, ref _carTrailParticlesActive);
                return;
            }

            if (_currentSpeed > movementThreshold)
            {
                if (!_carTrailParticlesActive)
                    EnableParticles(trailVfxs, ref _carTrailParticlesActive);
                UpdateCarTrailMovementEmission();
            }
            else
            {
                if (_carTrailParticlesActive)
                    StopParticles(trailVfxs, ref _carTrailParticlesActive);
            }
        }

        private void UpdateCarTrailMovementEmission()
        {
            float normalizedSpeed = Mathf.Clamp01(Mathf.InverseLerp(movementThreshold, speedToEmitAllParticles / 3.6f, _currentSpeed));
            for (int i = 0; i < _trailEmissionModules.Length; i++) // cant do foreach
            {
                _trailEmissionModules[i].rateOverTime = _movementTrailInitialEmmisionRate * normalizedSpeed;
            }

        }

        #endregion


        private void StopParticles(ParticleSystem[] particleSystems, ref bool boolToChange)
        {
            if (!boolToChange) return;
            foreach (ParticleSystem particle in particleSystems)
            {
                particle.Stop();
            }
            boolToChange = false;
        }

        private void EnableParticles(ParticleSystem[] particleSystems, ref bool boolToChange)
        {
            if (boolToChange) return;
            foreach (ParticleSystem particle in particleSystems)
            {
                particle.Play();
            }

            boolToChange = true;
        }


    }
}

