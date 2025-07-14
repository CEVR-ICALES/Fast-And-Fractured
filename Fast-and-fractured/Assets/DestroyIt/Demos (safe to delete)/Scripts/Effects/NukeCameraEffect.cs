using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Put this script on your main camera if you want camera effects when the nuke explodes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NukeCameraEffect : MonoBehaviour
    {
        [Tooltip("Stores the initial, regular tonemapping settings of the main camera for later use.")]
        public Tonemapping regularTonemapping;
        [Tooltip("Alternate tonemapping settings to make the scene look over-exposed and strange while the nuke is active.")]
        public Tonemapping nukeTonemapping;
        [Tooltip("Optional additional color correction curves to use on the main camera to make the scene look strange while the nuke is active.")]
        public ColorCorrectionCurves nukeColorCorrection;

        public void Start()
        {
            // Use whichever tonemapper is enabled on the scene by default as the "regular" tonemapping.
            // The other one will be used for "nuke effect" tonemapping.
            Tonemapping[] toneMappers = GetComponents<Tonemapping>();
            if (toneMappers != null && toneMappers.Length == 2)
            {
                if (toneMappers[0].enabled)
                {
                    regularTonemapping = toneMappers[0];
                    nukeTonemapping = toneMappers[1];
                }
                else
                {
                    nukeTonemapping = toneMappers[0];
                    regularTonemapping = toneMappers[1];
                }
                nukeTonemapping.enabled = false;
            }

            ColorCorrectionCurves colorCorrection = GetComponent<ColorCorrectionCurves>();
            if (colorCorrection != null && !colorCorrection.enabled)
                nukeColorCorrection = colorCorrection;
        }

        public void OnNukeStart()
        {
            // If the main camera has a ToneMapping component, switch it to the nuke tonemapping settings.
            if (regularTonemapping != null && regularTonemapping.enabled)
            {
                regularTonemapping.enabled = false;
                nukeTonemapping.enabled = true;
            }

            if (nukeColorCorrection != null && !nukeColorCorrection.enabled)
                nukeColorCorrection.enabled = true;
        }

        public void OnNukeEnd()
        {
            // If the main camera has a ToneMapping component, deactivate it.
            if (regularTonemapping != null && !regularTonemapping.enabled && nukeTonemapping != null && nukeTonemapping.enabled)
            {
                nukeTonemapping.enabled = false;
                regularTonemapping.enabled = true;
            }

            if (nukeColorCorrection != null && nukeColorCorrection.enabled)
                nukeColorCorrection.enabled = false;
        }
    }
}