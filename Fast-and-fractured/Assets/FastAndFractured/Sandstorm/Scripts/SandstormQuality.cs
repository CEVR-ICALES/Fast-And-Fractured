using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace FastAndFractured
{
    public sealed class SandstormQuality : MonoBehaviour
    {
        public const int DEFAULT_LOW_RESOLUTION_UP_TO_LEVEL = 3;

        [SerializeField] private Renderer stormRenderer;
        [SerializeField] private int lowResolutionUpToLevel = DEFAULT_LOW_RESOLUTION_UP_TO_LEVEL;

        private Material _sharedMaterial;
        private Material _lowResolutionMaterial;

        public static bool UsesLowResolution(int qualityLevel, int lowResolutionUpToLevel) => qualityLevel <= lowResolutionUpToLevel;

        private void OnEnable()
        {
            GraphicsQuality.LevelChanged += Apply;
            Apply(QualitySettings.GetQualityLevel());
        }

        private void OnDisable()
        {
            GraphicsQuality.LevelChanged -= Apply;
        }

        private void OnDestroy()
        {
            if (_lowResolutionMaterial != null)
                Destroy(_lowResolutionMaterial);
        }

        private void Apply(int qualityLevel)
        {
            if (stormRenderer == null)
                return;
            if (_sharedMaterial == null)
                _sharedMaterial = stormRenderer.sharedMaterial;
            if (_sharedMaterial == null)
                return;

            if (!UsesLowResolution(qualityLevel, lowResolutionUpToLevel))
            {
                stormRenderer.sharedMaterial = _sharedMaterial;
                return;
            }
            if (_lowResolutionMaterial == null)
            {
                _lowResolutionMaterial = new Material(_sharedMaterial) { name = _sharedMaterial.name + " (Low Resolution)" };
                HDMaterial.SetRenderingPass(_lowResolutionMaterial, HDMaterial.RenderingPass.LowResolution);
            }
            stormRenderer.sharedMaterial = _lowResolutionMaterial;
        }
    }
}
