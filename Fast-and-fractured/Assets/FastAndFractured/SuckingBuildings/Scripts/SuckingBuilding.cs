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
        [SerializeField]
        private AnimationCurve distanceCurve;
        public float pullForce = 30f;
        [SerializeField]
        private float y_offset = 0.5f;
        [SerializeField]
        private float pullOverTime = 0.5f;
        [SerializeField]
        private float pullDuration = 0.2f;
        [SerializeField]
        private float maxPosibleSpeedForTheCar = 400f;
        [SerializeField] private ParticleSystem suckingVfx;
        [SerializeField] private ParticleSystem explodeVFX;
        private bool _isPaused = false;
        private List<GameObject> _charactersList = new List<GameObject>();
        private Dictionary<GameObject, ITimer> _timerCharacterList = new Dictionary<GameObject, ITimer>();
        private System.Predicate<GameObject> _pullFilter;
        [SerializeField]
        private ParabolicImpulse parabolicImpulse;



        public void SetPullFilter(System.Predicate<GameObject> pullFilter)
        {
            _pullFilter = pullFilter;
        }

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

        void Start()
        {
            if(parabolicImpulse == null)
                parabolicImpulse = GetComponentInChildren<ParabolicImpulse>();
            parabolicImpulse.onImpulse.AddListener(RemovePlayerFromList);
        }

        void FixedUpdate()
        {
            if (_isPaused)
                return;
            if (_charactersList == null)
                return;
            foreach (GameObject obj in _charactersList)
                {
                    if(obj!=null && (_pullFilter == null || _pullFilter(obj)))
                    {
                        float distance = Vector3.Distance(transform.position, obj.transform.position);
                        PullCharacter(obj, distance, _timerCharacterList[obj]);
                    }
                }
        }

        void LateUpdate()
        {
            HandleVfx();
        }

        private void HandleVfx()
        {
            // Some map dead zones intentionally have no particle effect assigned. Treat VFX as
            // optional instead of throwing every FixedUpdate (exception logging can stall the
            // game completely when several zones are active at once).
            if (suckingVfx == null)
                return;

            if(_charactersList.Count > 0)
            {
                if(!suckingVfx.isPlaying)
                   suckingVfx.Play();
            } 
            else
            {
                if(suckingVfx.isPlaying)
                    suckingVfx.Stop();
            }
        }

        void PullCharacter(GameObject character, float distance,ITimer pullRate)
        {
            PhysicsBehaviour rb = character.GetComponent<PhysicsBehaviour>();
            Vector3 directionToTarget = (character.transform.position - transform.position).normalized;
            float forceMultiplier = distanceCurve.Evaluate((detectionRadius - distance) / detectionRadius);
            Vector3 pullDirection = (transform.position - character.transform.position).normalized + y_offset*Vector3.up;
            if (pullRate == null)
            {
                rb.ApplyImpulse(pullDirection * (pullForce * forceMultiplier), ForceMode.Acceleration, false, pullDuration, false, maxPosibleSpeedForTheCar, false);
                pullRate = TimerSystem.Instance.CreateTimer(pullOverTime, onTimerDecreaseComplete: () => { pullRate = null; });
            }
          
        }

        private void RemovePlayerFromList(GameObject character)
        {
            if(character == null) return;
            if (_charactersList.Contains(character))
            {
                _charactersList.Remove(character);
                if (_timerCharacterList[character] != null)
                    _timerCharacterList[character].StopTimer();
                _timerCharacterList.Remove(character);
                suckingVfx?.Stop();
                explodeVFX.Play();
            }
        }
        private void OnTriggerEnter(Collider other)
        {
            StatsController statsController = other.gameObject.GetComponent<StatsController>();
            if (statsController != null)
            {
                if (!_charactersList.Contains(other.gameObject))
                {
                    _charactersList.Add(other.gameObject);
                    _timerCharacterList.Add(other.gameObject, null);
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
                    if (_timerCharacterList[other.gameObject] != null)
                        _timerCharacterList[other.gameObject].StopTimer();
                    _timerCharacterList.Remove(other.gameObject);
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
