using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Utilities;

public class BrightnessManager : AbstractSingleton<BrightnessManager>
{
    #region Variables
    [SerializeField] private Volume boxVolume;
    private LiftGammaGain _liftGammaGain;
    private float _brightness;

    private const float MIN_GAMMA_MODIFIER = 0.27f;
    private const float MAX_MAX_MODIFIER = 1.27f;
    private const float DEFAULT_MIDTONES_MODIFIER = 0.77f;
    private const float NEGATIVE_GAMMA_OFFESET = 0.06f;
    private const float POSITIVE_GAMMA_OFFESET = 0.07f;
    private const string PLAYER_PREF_BRIGHTNESS_STRING = "Brightness";
    private readonly Vector3 _defaultGammaRGB = new Vector3(0.84f, 0.77f, 0.71f);
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _brightness = PlayerPrefs.GetFloat(PLAYER_PREF_BRIGHTNESS_STRING, DEFAULT_MIDTONES_MODIFIER);
        SceneManager.sceneLoaded += OnSceneLoaded;
        if(boxVolume.profile.TryGet(out _liftGammaGain))
        {

        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //ApplyBrightnessToScene();
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
        Debug.Log(_brightness);
        float value = Mathf.Clamp(_brightness, MIN_GAMMA_MODIFIER, MAX_MAX_MODIFIER);
        Debug.Log(value);
        _liftGammaGain.gamma.overrideState = true;
        _liftGammaGain.gamma.Override(new Vector4(
            value - POSITIVE_GAMMA_OFFESET,  // R (0.84)
            value,  // G (0.77)
            value + POSITIVE_GAMMA_OFFESET,  // B (0.71)
            1f         // Alpha (gamma ajustable)
        ));
    }
}

