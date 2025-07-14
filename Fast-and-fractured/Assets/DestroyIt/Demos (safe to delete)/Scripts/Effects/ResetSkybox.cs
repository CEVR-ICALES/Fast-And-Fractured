using UnityEngine;

namespace DestroyIt
{
    public class ResetSkybox : MonoBehaviour
    {
        void Start()
        {
            // Reset the skybox blend amount to zero. This is only used for the demo scene.
            if (RenderSettings.skybox.HasProperty("_Blend"))
                RenderSettings.skybox.SetFloat("_Blend", 0f);

            Destroy(this);
        }
    }
}