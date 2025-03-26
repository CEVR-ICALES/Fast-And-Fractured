using UnityEngine;

namespace FastAndFractured
{
    public class CarmenUniqueAbilitie : BaseUniqueAbility
    {
        public GameObject chickenPrefab;
        public Transform uniqueAbilityShootPoint;

        public override void ActivateAbility()
        {
            base.ActivateAbility();
            if(!IsOnCooldown)
            {
                GameObject uniqueAbility = Instantiate(chickenPrefab, uniqueAbilityShootPoint.position, Quaternion.identity);
                Vector3 aimDirection = GetComponent<ShootingHandle>().CurrentShootDirection;
                uniqueAbility.GetComponent<McChicken>().InitializeChicken(aimDirection);
            }

        }


        
    }
}

