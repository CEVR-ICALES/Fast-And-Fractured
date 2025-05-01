using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;
namespace FastAndFractured
{
    public class JosefinoCarImpactHandler : CarImpactHandler
    {
        private JosefinoUniqueAbility _josefinoUniqueAbility;

        private void Awake()
        {
            _josefinoUniqueAbility = gameObject.GetComponent<JosefinoUniqueAbility>();
        }

        public override void OnHasBeenPushed(PhysicsBehaviour carPhysicsBehaviour)
        {
            if (_josefinoUniqueAbility.IsAbilityActive)
            {
                _josefinoUniqueAbility.OnInvulnerabilityLost();
                return;
            }

            base.OnHasBeenPushed(carPhysicsBehaviour);
        }

        public override ModifiedCarState CheckForModifiedCarState()
        {
            if (_josefinoUniqueAbility.IsAbilityActive)
                return ModifiedCarState.JOSEFINO_INVULNERABLE;

            return base.CheckForModifiedCarState(); 
        }

        public override float ApplyModifierToPushForceAsPushed(float forceToApply, ModifiedCarState otherCarModifiedState, bool isFrontalHit, bool isOtherCarDashing)
        {
            if(_josefinoUniqueAbility.IsAbilityActive)
            {
                return 0f;
            }

            return forceToApply;
        }

        public override bool HandleIfTomatoeEffect()
        {
            if (_josefinoUniqueAbility.IsAbilityActive)
            {
                _josefinoUniqueAbility.OnInvulnerabilityLost();
                return false;
            } else
            {
                return true;
            }
        }
    }

}

