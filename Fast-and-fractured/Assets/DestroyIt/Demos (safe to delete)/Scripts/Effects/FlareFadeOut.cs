using UnityEngine;

namespace DestroyIt
{
    public class FlareFadeOut : MonoBehaviour
    {

        [Range(0f, 10f)]
        public float flareFadeSeconds = 5f;

        private float startBrightness;
        private LensFlare flare;

        // Use this for initialization
        void Start()
        {
            flare = GetComponent<LensFlare>();
            startBrightness = flare.brightness;
        }

        // Update is called once per frame
        void Update()
        {
            flare.brightness -= Mathf.Clamp01(Time.deltaTime / (flareFadeSeconds / startBrightness));

            if (flare.brightness <= 0f)
            {
                Destroy(flare);
                Destroy(this);
            }
        }
    }
}