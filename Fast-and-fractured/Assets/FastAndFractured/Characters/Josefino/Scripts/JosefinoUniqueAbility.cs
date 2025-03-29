using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

namespace FastAndFractured
{
    public class JosefinoUniqueAbility : BaseUniqueAbility
    {
        [SerializeField] private float enduranceRecoveryAmount = 0f;

        public override void ActivateAbility()
        {
            base.ActivateAbility();
            AbilityEffect();
        }
        private void AbilityEffect()
        {
            Controller controller = GetComponentInParent<Controller>();
            controller.GetBehaviour<StatsController>().RecoverEndurance(enduranceRecoveryAmount, false);
            controller.GetBehaviour<StatsController>().IsInvulnerable = true;
        }
    }

}