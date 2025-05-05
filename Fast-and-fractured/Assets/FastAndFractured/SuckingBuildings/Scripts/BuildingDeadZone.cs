using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class BuildingDeadZone : MonoBehaviour
    { 
        private void OnTriggerEnter(Collider other)
        {
            StatsController statsController = other.gameObject.GetComponent<StatsController>();
            if (statsController != null)
            {
                statsController.Dead();
            }
        }
    }
}
