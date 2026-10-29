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
    protected override void Construct()
    {
        base.Construct();
        _brightness = PlayerPrefs.GetFloat(PLAYER_PREF_BRIGHTNESS_STRING, DEFAULT_BRIGHTNESS);
    }
   
    protected override void Initialize()
    {
        if (TryBindVolume())
            ApplyBrightnessToScene();
    }

    private GameObject _boundVolume;

    /// <summary>The volume can be picked (or changed) after this initialized: follow it.</summary>
    private void Update()
    {
        if (!_isInitialized || !VolumeManager.HasInstance) return;
        GameObject current = VolumeManager.Instance.CurrentVolumeGameObject;
        if (current == null || current == _boundVolume) return;
        exposure = null;
        ApplyBrightnessToScene();
    }

    /// <summary>
    /// Binds the current volume's exposure; the VolumeManager may not have picked a volume yet, so this is retried
    /// on every apply and volume change.
    /// </summary>
    private bool TryBindVolume()
    {
        if (exposure != null) return true;
        if (!VolumeManager.HasInstance) return false;
        GameObject current = VolumeManager.Instance.CurrentVolumeGameObject;
        if (current == null || current.transform.childCount <= 2) return false;
        _boundVolume = current;

        boxVolume = current.transform.GetChild(2).GetComponent<Volume>();
        if (boxVolume == null)
        {
            Debug.LogError("NO VOLUME FOUND. IS THERE A VOLUME? IS IT CALLED \"Box Volume\"????");
            return false;
        }

        exposure = GetComponentOfTypeOnVolume<Exposure>(boxVolume);
        if (exposure == null) return false;
        _startingExposure = exposure.fixedExposure.value;
        return true;
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
        if (!TryBindVolume()) return;
        exposure.fixedExposure.value = CalculateExposureFromBrightness(_brightness);
    }

}

