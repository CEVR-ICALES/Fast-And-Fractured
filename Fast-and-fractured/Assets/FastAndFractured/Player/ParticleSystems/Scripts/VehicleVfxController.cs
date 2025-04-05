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


        PhysicsBehaviour _physicsBehaviour;

        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
        }

        private void FixedUpdate()
        {
            HandleTrailParticleSystem();
        }

        private void HandleTrailParticleSystem()
        {
            if(_physicsBehaviour.Rb.velocity.magnitude > movementThreshold)
            {
                EnableParticles(trailParticleSystems);
            } else
            {
                StopParticles(trailParticleSystems);
            }
        }


        private void StopParticles(ParticleSystem[] particleSystems)
        {
            foreach(ParticleSystem particle in particleSystems)
            {
                particle.Stop();
            }
        }

        private void EnableParticles(ParticleSystem[] particleSystems)
        {
            foreach(ParticleSystem particle in particleSystems)
            {
                particle.Play();
            }
        }
    }
}

