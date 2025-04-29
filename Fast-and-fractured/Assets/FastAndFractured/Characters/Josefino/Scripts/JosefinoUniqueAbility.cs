using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using UnityEngine.InputSystem.XR;

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
            Controller controller = GetComponentInParent<Controller>();
            _statsController = controller.GetBehaviour<StatsController>();
            _statsController.onInvulnerabilityLost.AddListener(OnInvulnerabilityLost);
        } 
        
        private void OnDestroy()
        {
            _statsController.onInvulnerabilityLost.RemoveListener(OnInvulnerabilityLost);
        }

        private void AbilityEffect()
        {
           _statsController.RecoverEndurance(enduranceRecoveryAmount, false);
           _statsController.ActivateInvulnerability();
        }
        public void OnInvulnerabilityLost()
        {
            Debug.Log("Invulnerability Lost");
            EndAbilityEffects();
            
        }
    }

}