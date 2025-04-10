using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Utilities;

namespace FastAndFractured
{
    public abstract class BaseUniqueAbility : MonoBehaviour, ITimeSpeedModifiable
    {
        #region VARIABLES

        [SerializeField] protected AbilityData abilityData;
        private StatsController statsController;
        private bool _isAbilityActive = false;
        private bool _isAI;
        private GameObject _uniqueUIPrefabInstance;
        private GameObject _particleEffectPrefabInstance;
        public UnityEvent onAbilityActivated;
        public UnityEvent onAbilityEnded;
        private bool _isOnCooldown = false; 
        private float _currentCooldownTime = 0f;  
        private ITimer _cooldownTimer;
        #endregion
        public bool IsAbilityActive => _isAbilityActive;
        public bool IsOnCooldown => _isOnCooldown;

        private void Awake()
        {
            if (abilityData == null)
            {
                Debug.LogError("Ability Data Is Null:  ", this);
                return;
            }

            _isAbilityActive = false;
            
            _isAI = GetComponentInParent<EnemyAIBrain>();
            statsController = GetComponent<StatsController>();
            abilityData.CooldownDuration = statsController.UniqueCooldown;
        }


        public virtual void ActivateAbility()
        {
            if (_isAbilityActive || _isOnCooldown)
            {
                return;
            }

            _isAbilityActive = true;
            PlayActivateAbilitySound();
            StartAbilityEffects();
            onAbilityActivated?.Invoke();
            StartCooldown();
        }  
        private void StartCooldown()
        {
            _currentCooldownTime = abilityData.CooldownDuration;  
            _cooldownTimer = TimerSystem.Instance.CreateTimer(abilityData.CooldownDuration, onTimerDecreaseComplete:StopCooldown) ;
            ModifySpeedOfExistingTimer(statsController.CooldownSpeed);
            _isOnCooldown = true;
        }

        protected virtual void PlayActivateAbilitySound()
        {
            //if (abilityData == null || abilityData.FMODEventPath.IsNull)
            //{
            //    return;
            //}
            //SoundManager.Instance.PlaySound3D(abilityData.FMODEventPath.Path, this.transform.position);
        }

        protected virtual void StartAbilityEffects()
        {
            GameObject uniqueUIPrefab = abilityData.UniqueUIPrefab;
            if (uniqueUIPrefab != null && !_isAI)
            {
                _uniqueUIPrefabInstance = Instantiate(uniqueUIPrefab);
            }

            GameObject particleEffectPrefab = abilityData.ParticleEffectPrefab;

            if (particleEffectPrefab != null)
            {
                _particleEffectPrefabInstance =
                    Instantiate(particleEffectPrefab, this.transform.position, this.transform.rotation);
            }
        }


        protected virtual void EndAbilityEffects()
        {
            DestroySpawnedGameObjectsInstances();
            _isAbilityActive = false;
            onAbilityEnded?.Invoke();
        } 
        
        private void DestroySpawnedGameObjectsInstances()
        {
            Destroy(_particleEffectPrefabInstance);
            Destroy(_uniqueUIPrefabInstance);
        }
       
        public float GetNormalizedCooldownProgress()
        {
            if (_cooldownTimer== null)
            {
                return 1;
            }
            return _cooldownTimer.GetData().NormalizedProgress;
        }
 
        protected void StopCooldown()  
        {
            if (_cooldownTimer != null && TimerSystem.Instance.HasTimer(_cooldownTimer))
            {
                TimerSystem.Instance.StopTimer(_cooldownTimer.GetData().ID);  
            }
            _cooldownTimer = null; 
            _isOnCooldown = false;
            _currentCooldownTime = 0f;
        }

        public void ModifySpeedOfExistingTimer(float newTimerSpeed)
        {
            if (_cooldownTimer != null)
            {
                TimerSystem.Instance.ModifyTimer(_cooldownTimer, speedMultiplier: newTimerSpeed);
            }
        }
    }
}