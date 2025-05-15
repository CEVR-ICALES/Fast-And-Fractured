using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class BuildingDeadZone : MonoBehaviour
    { 
        public StatsController lastDead;
        private void OnTriggerEnter(Collider other)
        {
            StatsController statsController = other.gameObject.GetComponent<StatsController>();
            if (statsController != null)
            {
                if (lastDead != statsController)
                {
                    statsController.Dead();
                    lastDead = statsController;
                    Debug.Log("Dead: " + statsController.gameObject.name);
                }
            }
        }
    }
}
