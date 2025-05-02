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
        private bool _isPaused = false;
        private List<GameObject> _charactersList = new List<GameObject>();
        public float elevationForce = 0.5f;
        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
        }

        void FixedUpdate()
        {
            if (_isPaused)
                return;
            if (_charactersList != null)
            {
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
        
        void PullCharacter2(GameObject character, float distance)
        {
            Rigidbody rb = character.GetComponent<Rigidbody>();
            Vector3 directionToTarget = (character.transform.position - transform.position).normalized;
            float forceMultiplier = Mathf.Clamp01((detectionRadius - distance) / detectionRadius);
            Vector3 pullDirection = (transform.position - character.transform.position).normalized;
            rb.AddForce(pullDirection * (pullForce * forceMultiplier), ForceMode.Acceleration); 
        }

        
        void PullCharacter(GameObject character, float distance)
        {
            Rigidbody rb = character.GetComponent<Rigidbody>();
            Vector3 directionToTarget = (character.transform.position - transform.position).normalized;
            Vector3 pullDirection = (transform.position - character.transform.position).normalized;
            float forceMultiplier = Mathf.Clamp01((detectionRadius - distance) / detectionRadius);
            Vector3 newPosition = rb.position + pullDirection * (pullForce * forceMultiplier) * Time.fixedDeltaTime;
            rb.MovePosition(newPosition);
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
