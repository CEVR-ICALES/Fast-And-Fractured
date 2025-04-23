using UnityEditorInternal;
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
        [SerializeField] private float _movementTrailInitialEmmisionRate = 300f; // should always match the emission rate if its going to be modified
        private ParticleSystem.EmissionModule[] _trailEmissionModules; // necessary to modify rateOverTime

        [Header("Drift Trail")]
        [SerializeField] private TrailRenderer[] tyreDriftMarksVfx;

        [Header("Impact")]
        [SerializeField] private ParticleSystem collisionVfx;
        [SerializeField] private LayerMask collisionLayers;

        [Header("Dashing")]
        [SerializeField] private ParticleSystem dashSpeedVfx;
        [SerializeField] private ParticleSystem dashTurboVfx;

        [Header("Car Endurance")]
        [SerializeField] private ParticleSystem[] smokeVfx;
        private ParticleSystem.EmissionModule[] _smokeEmmisionModules; // necessary to modify rateOverTime
        private ParticleSystem.MainModule[] _smokeMainModules;
        private bool _carEnduracenParticlesActive = false;
        private const float START_EMMITTING_ENDURANCE_PARTICLES_THRESHOLD = 0.8f;
        private const float HIGH_ENDURANCE_MIN_START_SPEED = 0.8f;
        private const float HIGH_ENDURANCE_MAX_START_SPEED = 1.2f;
        private const float HIGH_ENDURANCE_EMISSION_RATE = 15f;
        private const float ALMOST_HALF_ENDURANCE = 0.55f;
        private const float HALF_ENDURANCE_MIN_START_SPEED = 1.4f;
        private const float HALF_ENDURANCE_MAX_START_SPEED = 1.7f;
        private const float HALF_ENDURANCE_EMISSION_RATE = 40f;
        private const float ENDURANCE_SUPERLOW = 0.3f;
        private const float LOW_ENDURANCE_MIN_START_SPEED = 1.8f;
        private const float LOW_ENDURANCE_MAX_START_SPEED = 2.2f;
        private const float LOW_ENDURANCE_EMISSION_RATE = 80f;


        PhysicsBehaviour _physicsBehaviour;
        CarMovementController _carMovementController;
        private float _currentSpeed;

        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            _carMovementController = GetComponent<CarMovementController>();

            _trailEmissionModules = new ParticleSystem.EmissionModule[trailVfxs.Length];
            _smokeEmmisionModules = new ParticleSystem.EmissionModule[smokeVfx.Length];
            _smokeMainModules = new ParticleSystem.MainModule[smokeVfx.Length];

            for (int i = 0; i < trailVfxs.Length; i++)
            {
                _trailEmissionModules[i] = trailVfxs[i].emission;
            }

            for (int i = 0; i <= smokeVfx.Length; i++)
            {
                _smokeEmmisionModules[i] = smokeVfx[i].emission;
                _smokeMainModules[i] = smokeVfx[i].main;
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
            if (_carMovementController.IsBraking && _carMovementController.IsGrounded())
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

        #region Dash

        public void StartDashVfx()
        {
            dashSpeedVfx.Play();
            dashTurboVfx.Play();
        }

        public void StopDashVfx()
        {
            dashSpeedVfx.Stop();
            dashTurboVfx.Stop();
        }

        #endregion

        #region Endurance
        
        public void HandleOnEnduranceChanged(float enduranceFactor)
        {
            if(enduranceFactor > START_EMMITTING_ENDURANCE_PARTICLES_THRESHOLD)
            {
                if(_carEnduracenParticlesActive)
                    StopParticles(smokeVfx, ref _carEnduracenParticlesActive);
            } else
            {
                if (enduranceFactor <= ENDURANCE_SUPERLOW)
                {
                    UpdateEnduranceVfxValues(LOW_ENDURANCE_MIN_START_SPEED, LOW_ENDURANCE_MAX_START_SPEED, LOW_ENDURANCE_EMISSION_RATE);
                }
                else if (enduranceFactor <= ALMOST_HALF_ENDURANCE)
                {
                    UpdateEnduranceVfxValues(HALF_ENDURANCE_MIN_START_SPEED, HALF_ENDURANCE_MAX_START_SPEED, HALF_ENDURANCE_EMISSION_RATE);
                }
                else if (enduranceFactor <= START_EMMITTING_ENDURANCE_PARTICLES_THRESHOLD)
                {
                    UpdateEnduranceVfxValues(HIGH_ENDURANCE_MIN_START_SPEED, HIGH_ENDURANCE_MAX_START_SPEED, HIGH_ENDURANCE_EMISSION_RATE);
                }
                if(!_carEnduracenParticlesActive)
                    EnableParticles(smokeVfx, ref _carEnduracenParticlesActive);
            } 
        }

        private void UpdateEnduranceVfxValues(float minStartSpeed, float maxStartSpeed, float emmissionRate)
        {
            for(int i = 0; i < _smokeMainModules.Length; i++)
            {
                _smokeMainModules[i].startSpeed = new ParticleSystem.MinMaxCurve(minStartSpeed, maxStartSpeed);
            }

            for(int i = 0; i < _smokeEmmisionModules.Length; i++)
            {
                _smokeEmmisionModules[i].rateOverTime = emmissionRate;
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

