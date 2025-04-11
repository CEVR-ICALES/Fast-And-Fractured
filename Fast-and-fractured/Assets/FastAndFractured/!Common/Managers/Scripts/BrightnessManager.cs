using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Utilities;

public class BrightnessManager : AbstractSingleton<BrightnessManager>
{
    #region Variables
    private float _brightness = 1f;
    private const float minGammaValue = 0.3f;
    private const float maxGammaValue = 1.7f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _brightness = PlayerPrefs.GetFloat("Brightness", 1f);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyBrightnessToScene();
    }

    public void SetBrightness(float value)
    {
        _brightness = value;
        PlayerPrefs.SetFloat("Brightness", _brightness);
        PlayerPrefs.Save();

        ApplyBrightnessToScene();
    }

    private void ApplyBrightnessToScene()
    {
        Volume globalVolume = FindObjectOfType<Volume>();

        if (globalVolume == null || globalVolume.profile == null) return;

        LiftGammaGain liftGammaGain;

        if (!globalVolume.profile.TryGet(out liftGammaGain))
            liftGammaGain = globalVolume.profile.Add<LiftGammaGain>(true);

        float gamma = Mathf.Lerp(minGammaValue, maxGammaValue, _brightness);
        liftGammaGain.gamma.overrideState = true;
        liftGammaGain.gamma.value = new Vector4(gamma, gamma, gamma, 1f);
    }
}
