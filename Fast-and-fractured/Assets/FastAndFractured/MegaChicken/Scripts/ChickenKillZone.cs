using UnityEngine;
using Utilities;
using Enums;
namespace FastAndFractured
{
    public class ChickenKillZone : MonoBehaviour
    {
        public ChickenBrain brain;
        public ParticleSystem groundImpactEffect;
        private bool avoidMultipleExecutions = false;
        private const string GROUND_LAYER_NAME = "Ground";
        private const string ANIMATION_JUMP_NAME = "Jump";
        private const float ANIMATION_JUMP_HIT_TIME = 0.5f;
        private const float TIME_TO_AVOID_MULTIPLE_EXECUTIONS = 2f;
        void Start()
        {
            
        }

        void Update()
        {
            
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer(GROUND_LAYER_NAME))
            {
                if (avoidMultipleExecutions) return;
                groundImpactEffect.Play();
                avoidMultipleExecutions = true;
                TimerSystem.Instance.CreateTimer(TIME_TO_AVOID_MULTIPLE_EXECUTIONS, onTimerDecreaseComplete: () =>
                {
                    avoidMultipleExecutions = false;
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
        }
    }
}