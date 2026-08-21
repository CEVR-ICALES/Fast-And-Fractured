using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class SuckingBuilding : MonoBehaviour, IPausable
    {
        public float detectionRadius = 40f;
        public float pullForce = 30f;
        [SerializeField] private ParticleSystem vfx;
        private bool _isPaused = false;
        private List<GameObject> _charactersList = new List<GameObject>();
        void OnEnable()
        {
            PauseManager.TryRegisterPausable(this);
        }

        void OnDisable()
        {
            if (PauseManager.HasInstance)
            {
                PauseManager.TryUnregisterPausable(this);
            }
        }

        void FixedUpdate()
        {
            if (_isPaused)
                return;
            if (_charactersList != null)
            {
                HandleVfx();
                foreach (GameObject obj in _charactersList)
                {
                    if(obj!=null)
                    {
                        float distance = Vector3.Distance(transform.position, obj.transform.position);
                        if(distance<=detectionRadius)
                        {
                            PullCharacter(obj, distance);
                        }
                    }
                }
            }
        }

        private void HandleVfx()
        {
            // Some map dead zones intentionally have no particle effect assigned. Treat VFX as
            // optional instead of throwing every FixedUpdate (exception logging can stall the
            // game completely when several zones are active at once).
            if (vfx == null)
                return;

            if(_charactersList.Count > 0)
            {
                if(!vfx.isPlaying)
                   vfx.Play();
            } else
            {
                if(vfx.isPlaying)
                    vfx.Stop();
            }
        }

        void PullCharacter(GameObject character, float distance)
        {
            Rigidbody rb = character.GetComponent<Rigidbody>();
            Vector3 directionToTarget = (character.transform.position - transform.position).normalized;
            float forceMultiplier = Mathf.Clamp01((detectionRadius - distance) / detectionRadius);
            Vector3 pullDirection = (transform.position - character.transform.position).normalized;
            rb.AddForce(pullDirection * (pullForce * forceMultiplier), ForceMode.Acceleration); 
        }
        private void OnTriggerEnter(Collider other)
        {
            StatsController statsController = other.gameObject.GetComponent<StatsController>();
            if (statsController != null)
            {
                if (!_charactersList.Contains(other.gameObject))
                {
                    _charactersList.Add(other.gameObject);
                }
            }
        }
        private void OnTriggerExit(Collider other)
        {
            StatsController statsController = other.gameObject.GetComponent<StatsController>();
            if (statsController != null)
            {
                if (_charactersList.Contains(other.gameObject))
                {
                    _charactersList.Remove(other.gameObject);
                }
            }
        }
        public void OnPause()
        {
            _isPaused = true;
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }
}
