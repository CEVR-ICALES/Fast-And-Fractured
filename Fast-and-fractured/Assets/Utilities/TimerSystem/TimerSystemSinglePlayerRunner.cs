using UnityEngine;

namespace Utilities
{ 
    public class TimerSystemSinglePlayerRunner : MonoBehaviour
    {
        private void Update()
        {
          TimerSystem.Instance.Tick(Time.deltaTime);
        }
    }
}
