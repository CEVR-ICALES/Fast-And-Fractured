using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Utilities;

public class BrightnessManager : AbstractSingleton<BrightnessManager>
{
    #region Variables
    private float _brightness = 1f;
    private const float MIN_GAMMA_THRESHOLD_VALUE = -0.7f;
    private const float MAX_GAMMA_THRESHOLD_VALUE = 0.7f;
    private const string PLAYER_PREF_BRIGHTNESS_STRING = "Brightness";
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _brightness = PlayerPrefs.GetFloat(PLAYER_PREF_BRIGHTNESS_STRING, _brightness);
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
        PlayerPrefs.SetFloat(PLAYER_PREF_BRIGHTNESS_STRING, _brightness);
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

        float gamma = Mathf.Lerp(MIN_GAMMA_THRESHOLD_VALUE, MAX_GAMMA_THRESHOLD_VALUE, _brightness);

        liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, gamma));
    }
}
