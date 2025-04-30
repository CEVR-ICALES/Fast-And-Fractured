using UnityEngine;

namespace FastAndFractured
{
    public class MariaAntoniaCarImpactHandler : CarImpactHandler
    {

        private MariaAntoniaUniqueAbility _uniqueAbilityController;



        private void Awake()
        {
            _uniqueAbilityController = gameObject.GetComponent<MariaAntoniaUniqueAbility>();
        }

        public override void OnHasPushedOtherCar(PhysicsBehaviour otherCarPhysicsBehaviour)
        {
            base.OnHasPushedOtherCar(otherCarPhysicsBehaviour);

            _uniqueAbilityController.ConsumeCroquette();
        }

        //public override float ApplyModifierToPushForce(float forceToApply)
        //{
        //    if(_uniqueAbilityController.IsAbilityActive)
        //    {

        //    }
             
        //}
    }
}

