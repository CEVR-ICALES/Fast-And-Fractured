using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Utilities;

public class BrightnessManager : AbstractSingleton<BrightnessManager>
{
    #region Variables
    [SerializeField] private Volume boxVolume;
    private ShadowsMidtonesHighlights _shadowMitonesHighlights;
    private float _brightness;

    private const float MIN_MIDTONE_MODIFIER = -0.55f;
    private const float MAX_MIDTONE_MODIFIER = 0.55f;
    private const float DEFAULT_MIDTONES_MODIFIER = 1f;
    private const string PLAYER_PREF_BRIGHTNESS_STRING = "Brightness";
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _brightness = PlayerPrefs.GetFloat(PLAYER_PREF_BRIGHTNESS_STRING, DEFAULT_MIDTONES_MODIFIER);
        SceneManager.sceneLoaded += OnSceneLoaded;
        if(boxVolume.profile.TryGet(out _shadowMitonesHighlights))
        {
            _shadowMitonesHighlights.active = true;
            ApplyBrightnessToScene();
        }
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
        Debug.Log(value);
        _brightness = value;
        PlayerPrefs.SetFloat(PLAYER_PREF_BRIGHTNESS_STRING, _brightness);
        PlayerPrefs.Save();
       ApplyBrightnessToScene();
    }

    private void ApplyBrightnessToScene()
    {

        float midtonesValue = Mathf.Lerp(MIN_MIDTONE_MODIFIER, MAX_MIDTONE_MODIFIER, _brightness);
        _shadowMitonesHighlights.midtones.overrideState = true;
        _shadowMitonesHighlights.midtones.value = new Vector4(
            1f,
            1f,
            1f,
            midtonesValue
            );
    }

}
