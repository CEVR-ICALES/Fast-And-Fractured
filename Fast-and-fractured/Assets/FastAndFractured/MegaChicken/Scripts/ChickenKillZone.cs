using UnityEngine;
using Utilities;
using Enums;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class ChickenKillZone : MonoBehaviour, IPausable
    {
        public ChickenBrain brain;
        private ChickenPushZone _pushZone;
        public ParticleSystem groundImpactEffect;
        private bool _avoidMultipleExecutions = false;
        private bool _isPaused = false;
        private const string GROUND_LAYER_NAME = "Ground";
        private const string ANIMATION_JUMP_NAME = "Jump";
        private const string ANIMATION_LAYING_EGG_NAME = "LayEgg";
        private const float ANIMATION_JUMP_HIT_TIME = 0.5f;
        private const float TIME_TO_AVOID_MULTIPLE_EXECUTIONS = 2f;
        private const float ANIMATION_LAYING_EGG_KILL_TIME = 0.4f;
        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }
        void Start()
        {
            _pushZone = groundImpactEffect.GetComponent<ChickenPushZone>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer(GROUND_LAYER_NAME) && brain.IsJumping)
            {
                if (_avoidMultipleExecutions) return;
                groundImpactEffect.Play();
                _avoidMultipleExecutions = true;
                _pushZone.ActivatePushZone();
                TimerSystem.Instance.CreateTimer(TIME_TO_AVOID_MULTIPLE_EXECUTIONS, onTimerDecreaseComplete: () =>
                {
                    _avoidMultipleExecutions = false;
                });
            }

            if (brain.IsJumping)
            {
                AnimatorStateInfo stateInfo = brain.Animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName(ANIMATION_JUMP_NAME) && stateInfo.normalizedTime >= ANIMATION_JUMP_HIT_TIME)
                {
                    if (other.TryGetComponent<StatsController>(out StatsController statsController))
                    {
                        statsController.Dead();
                    }
                }
            }
            if (brain.IsLayingEgg)
            {
                AnimatorStateInfo stateInfo = brain.Animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName(ANIMATION_LAYING_EGG_NAME) && stateInfo.normalizedTime <= ANIMATION_LAYING_EGG_KILL_TIME)
                {
                    if (other.TryGetComponent<StatsController>(out StatsController statsController))
                    {
                        statsController.Dead();
                    }
                }
            }
        }
        public void OnPause()
        {
            _isPaused = true;
            if (groundImpactEffect != null && groundImpactEffect.isPlaying)
            {
                groundImpactEffect.Pause();
            }
        }

        public void OnResume()
        {
            _isPaused = false;
            if (groundImpactEffect != null && groundImpactEffect.isPaused)
            {
                groundImpactEffect.Play();
            }
        }
    }
}