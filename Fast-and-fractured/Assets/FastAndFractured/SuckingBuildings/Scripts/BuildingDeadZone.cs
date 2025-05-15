using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class BuildingDeadZone : MonoBehaviour
    { 
        private StatsController _lastDead = null;
        private void OnTriggerEnter(Collider other)
        {
            StatsController statsController = other.gameObject.GetComponent<StatsController>();
            if (statsController != null)
            {
                if (_lastDead != statsController)
                {
                    statsController.Dead();
                    _lastDead = statsController;
                    Debug.Log("Dead: " + statsController.gameObject.name);
                }
            }
        }
    }
}
