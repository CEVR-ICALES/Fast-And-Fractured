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
        private StatsController statsController;

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
            statsController = controller.GetBehaviour<StatsController>();
        }
        private void Update()
        {
            
        }
        private void AbilityEffect()
        {
           statsController.RecoverEndurance(enduranceRecoveryAmount, false);
           statsController.IsInvulnerable = true;
           EndAbilityEffects();
        }
    }

}