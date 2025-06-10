using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Utilities;

public class BrightnessManager : AbstractSingleton<BrightnessManager>
{
    #region Variables
    private Volume boxVolume;
    private Exposure exposure;
    private float _brightness;
    private float _startingExposure;

    private const string PLAYER_PREF_BRIGHTNESS_STRING = "Brightness";
    private const float DEFAULT_BRIGHTNESS = 0.5f;
    private const float DEFAULT_EXPOSURE = 16f;
    private const float MAX_EXPOSURE_OFFSET = -1.5f;
    private const float MIN_EXPOSURE_OFFSET = 1.5f;
    private const float EXPOSURE_MULTIPLIER = 2f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _brightness = PlayerPrefs.GetFloat(PLAYER_PREF_BRIGHTNESS_STRING, DEFAULT_BRIGHTNESS);
    }

    public void Start()
    {
        boxVolume = VolumeManager.Instance.CurrentVolumeGameObject.transform.GetChild(2).GetComponent<Volume>();
        if (boxVolume == null)
        {
            Debug.LogError("NO VOLUME FOUND. IS THERE A VOLUME? IS IT CALLED \"Box Volume\"????");
        }

        exposure = GetComponentOfTypeOnVolume<Exposure>(boxVolume);
        _startingExposure = exposure.fixedExposure.value;
        ApplyBrightnessToScene();
    }
    static T GetComponentOfTypeOnVolume<T>(Volume vol) where T : VolumeComponent
    {
        T returnVal = null;
        foreach (VolumeComponent v in vol.profile.components)
        {
            if (v.GetType() == typeof(T))
            {
                returnVal = (T)v;
            }
        }

        return returnVal;
    }

    float CalculateExposureFromBrightness(float brightness)
    {
        if (exposure == null)
        {
            return DEFAULT_EXPOSURE;
        }
        //Brightness goes from 0 to 1;
        return _startingExposure + (MIN_EXPOSURE_OFFSET + (brightness * MAX_EXPOSURE_OFFSET * EXPOSURE_MULTIPLIER));
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
        exposure.fixedExposure.value = CalculateExposureFromBrightness(_brightness);
    }
}

