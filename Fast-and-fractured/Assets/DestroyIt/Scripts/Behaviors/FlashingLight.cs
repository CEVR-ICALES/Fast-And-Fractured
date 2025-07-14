using UnityEngine;
using System.Collections;

namespace DestroyIt
{
    public class FlashingLight : MonoBehaviour
    {
        public float flashInterval = 0.5f;

        private Light _flashingLight;

        public void Start()
        {
            _flashingLight = GetComponent<Light>();
            StartCoroutine(Flashing());
        }

        private IEnumerator Flashing()
        {
            while (true)
            {
                yield return new WaitForSeconds(flashInterval);
                _flashingLight.enabled = !_flashingLight.enabled;
            }
        }
    }
}