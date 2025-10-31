using TMPro;
using UnityEngine;

namespace FastAndFractured
{
    public class CarSpeedOverlay : MonoBehaviour
    {

        public TextMeshProUGUI speedOverlayText;
        private PhysicsBehaviour _physicsBehaviour;
        const float SPEED_TO_METERS_PER_SECOND = 3.6f;

        private void Start()
        {
            _physicsBehaviour = GetComponent<PhysicsBehaviour>();
        }

        private void Update()
        {
            if (_physicsBehaviour != null) UpdateSpeedOverlay();
        }
        public void UpdateSpeedOverlay()
        {
            float speedZ = Mathf.Abs(_physicsBehaviour.Rb.linearVelocity.magnitude);
            float speedKmh = speedZ * SPEED_TO_METERS_PER_SECOND;
            if (speedOverlayText != null)
                speedOverlayText.text = speedKmh.ToString("F1");
        }
    }

}
