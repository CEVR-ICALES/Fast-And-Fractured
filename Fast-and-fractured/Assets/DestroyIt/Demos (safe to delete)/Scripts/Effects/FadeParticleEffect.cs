using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    /// <summary>This script fades out all the child particle effects for the game object it is attached to.</summary>
    public class FadeParticleEffect : MonoBehaviour
    {
        [Range(0f, 60f)]
        public float delaySeconds = 10f;    // How long to delay before starting the fade.
        [Range(0f, 60f)]
        public float fadeSeconds = 2f;      // How long to spend fading out the particle effect.
        [Range(1, 30)]
        public int updatesPerSecond = 15;   // How often to update the fade effect. Higher values have smoother fade-out, but higher performance cost.

        private float fadeTiming;
        private int stepCounter;
        private float totalFadeSteps;
        private List<ParticleEffectPropertyBag> particleEffectProperties;

        void Start()
        {
            particleEffectProperties = new List<ParticleEffectPropertyBag>();
            fadeTiming = 1 / (float)updatesPerSecond;
            totalFadeSteps = fadeSeconds / fadeTiming;

            Destroy(transform.gameObject, delaySeconds + fadeSeconds);

            if (fadeSeconds > 0f)
                InvokeRepeating("Fade", delaySeconds, fadeTiming);
        }

        void Fade()
        {
            stepCounter += 1;
            if (particleEffectProperties.Count == 0)
            {
                ParticleSystem[] particleSystems = this.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem particleSystem in particleSystems)
                {
                    Material mat = particleSystem.GetComponent<Renderer>().material;
                    if (!mat.HasProperty("_TintColor")) continue;
                    Color tintColorStart = mat.GetColor("_TintColor");
                    particleEffectProperties.Add(new ParticleEffectPropertyBag() { ParticleSystem = particleSystem, TintColorStart = tintColorStart });
                }
            }

            foreach (ParticleEffectPropertyBag particleSystemPropBag in particleEffectProperties)
            {
                Material mat = particleSystemPropBag.ParticleSystem.GetComponent<Renderer>().material;
                if (!mat.HasProperty("_TintColor")) continue;
                Color tintColor = particleSystemPropBag.TintColorStart;

                float fadeAmountPerStep = ((1.0f - particleSystemPropBag.TintColorStart.a) / 1.0f) / totalFadeSteps;
                float newAlphaValue = Mathf.Clamp01(particleSystemPropBag.TintColorStart.a - (fadeAmountPerStep * stepCounter));
                Color newTintColor = new Color(tintColor.r, tintColor.g, tintColor.b, newAlphaValue);
                mat.SetColor("_TintColor", newTintColor);
            }
        }
    }

    public class ParticleEffectPropertyBag
    {
        public ParticleSystem ParticleSystem { get; set; }
        public Color TintColorStart { get; set; }
    }
}