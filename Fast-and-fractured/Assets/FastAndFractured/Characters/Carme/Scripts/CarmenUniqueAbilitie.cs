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
            
            GameObject uniqueAbility = Instantiate(chickenPrefab, uniqueAbilityShootPoint.position, Quaternion.identity);
            Vector3 aimDirection = GetComponent<ShootingHandle>().CurrentShootDirection;
            Vector3 endPos = uniqueAbilityShootPoint.position + aimDirection.normalized * 5f;
            Debug.Log(aimDirection.normalized);
            Debug.DrawLine(uniqueAbility.transform.position, endPos, Color.green, 5f);
            uniqueAbility.GetComponent<McChicken>().InitializeChicken(aimDirection);
            
        }


        
    }
}

