using Enums;
using UnityEngine;

namespace FastAndFractured
{
    public class MariaAntoniaCarImpactHandler : CarImpactHandler
    {

        private MariaAntoniaUniqueAbility _uniqueAbilityController;

        private float forceMod = 1.3f; // testing value

        private void Awake()
        {
            _uniqueAbilityController = gameObject.GetComponent<MariaAntoniaUniqueAbility>();
        }

        public override ModifiedCarState CheckForModifiedCarState()
        {
            return ModifiedCarState.SUPER_MARIA_ANTONIA;
        }

        public override void OnHasPushedOtherCar(PhysicsBehaviour carPhysicsBehaviour)
        {
            base.OnHasPushedOtherCar(carPhysicsBehaviour);
            
            _uniqueAbilityController.ConsumeCroquette();
        }


        public override float ApplyModifierToPushForceAsAttacker(float forceToApply, ModifiedCarState otherCarModifiedState, bool isFrontalHit, bool isOtherCarDahsing)
        {
            if(_uniqueAbilityController.IsAbilityActive)
            {
                return forceToApply * forceMod;
            }

            return forceToApply;
        }
    }
}

