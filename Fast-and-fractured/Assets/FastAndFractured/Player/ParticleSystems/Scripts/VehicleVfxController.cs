using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class VehicleVfxController : MonoBehaviour
    {
        [Header("Car Movement Trail")]
        [SerializeField] private ParticleSystem[] trailParticleSystems;
        [SerializeField] private float movementThreshold;
        [SerializeField] private float speedToEmitAllParticles;
        private bool _carTrailParticlesActive = true;
        private float _movementTrailInitialEmmisionRate = 300f; // shopuld always match the emissio nrate if its going to be modified
        private ParticleSystem.EmissionModule[] _trailEmissionModules; // necessary to modify rateOverTime

        [Header("Drift Trail")]
        [SerializeField] private TrailRenderer[] tyreDriftMarks;


        PhysicsBehaviour _physicsBehaviour;
        CarMovementController _carMovementController;
        private float _currentSpeed;

        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
            _carMovementController = GetComponent<CarMovementController>();

            _trailEmissionModules = new ParticleSystem.EmissionModule[trailParticleSystems.Length];

            for (int i = 0; i < trailParticleSystems.Length; i++)
            {
                _trailEmissionModules[i] = trailParticleSystems[i].emission;
            }
        }

        private void FixedUpdate()
        {
            _currentSpeed = _physicsBehaviour.Rb.velocity.magnitude;
            HandleTrailParticleSystem();
            HandleBrakeMarks();
        }

        #region BrakeMarks
        private void HandleBrakeMarks()
        {
            if(_carMovementController.IsBraking)
            {
                StartBrakeMark();
            } else
            {
                StopBrakeMark();
            }
        }

        private void StartBrakeMark()
        {
            foreach(TrailRenderer trail in tyreDriftMarks)
            {
                trail.emitting = true;
            }
        }

        private void StopBrakeMark()
        {
            foreach (TrailRenderer trail in tyreDriftMarks)
            {
                trail.emitting = false;
            }
        }

        #endregion

        #region CarTrail

        private void HandleTrailParticleSystem()
        {
            if(!_carMovementController.IsGrounded())
            {
                StopParticles(trailParticleSystems, ref _carTrailParticlesActive);
                return;
            }

            if(_currentSpeed > movementThreshold)
            {
                if(!_carTrailParticlesActive)
                    EnableParticles(trailParticleSystems, ref _carTrailParticlesActive);
                UpdateCarTrailMovementEmission();
            } else
            {
                if(_carTrailParticlesActive)
                    StopParticles(trailParticleSystems, ref _carTrailParticlesActive);
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
            foreach(ParticleSystem particle in particleSystems)
            {
                particle.Stop();
            }
            boolToChange = false;
        }

        private void EnableParticles(ParticleSystem[] particleSystems, ref bool boolToChange)
        {
            if (boolToChange) return;
            foreach(ParticleSystem particle in particleSystems)
            {
                particle.Play();
            }

            boolToChange = true;
        }

        
    }
}

