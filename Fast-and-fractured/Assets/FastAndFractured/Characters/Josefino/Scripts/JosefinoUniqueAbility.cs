using UnityEngine;
using StateMachine;

namespace FastAndFractured
{
    public class JosefinoUniqueAbility : BaseUniqueAbility
    {
        [SerializeField] private float enduranceRecoveryAmount = 0f;
        private StatsController _statsController;

        public override bool ActivateAbility()
        {
            if (base.ActivateAbility())
            {
                AbilityEffect();
                return true;
            }
       

            return false;
        }

        private void Start()
        {
            StartCooldown();
            _statsController = GetComponent<StatsController>();
        } 
        
        private void OnDestroy()
        {
        }

        private void AbilityEffect()
        {
           _statsController.RecoverEndurance(enduranceRecoveryAmount, false);
        }
        public void OnInvulnerabilityLost()
        {
            Debug.Log("Invulnerability Lost");
            EndAbilityEffects();
        }
    }

}