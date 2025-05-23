using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class ColorBlindModeController : MonoBehaviour
{
    [Header("Volume Settings")] 
    private ChannelMixer _channelMixer;
    private int _colorblindModeIndex = 0;

    private const string COLORBLIND_KEY = "ColorBlindMode";
    private const string COLORBLIND_INDEX_KEY = "ColorBlindModeIndex";

    private void Start()
    {
        _colorblindModeIndex = PlayerPrefs.GetInt(COLORBLIND_INDEX_KEY);
        UpdateColorBlindMode(PlayerPrefs.GetInt(COLORBLIND_KEY, 0) == 1);
    }

    public void UpdateColorBlindMode(bool isEnabled)
    {   
        Volume currentVolume = VolumeManager.Instance.CurrentVolumeComponent;
        if (!currentVolume.profile.TryGet(out _channelMixer))
        {
            Debug.LogError("Channel Mixer not found in HDRP Volume Profile!");
            return;
        } else
        {
            EnableOverrideColorblind();
        }

        if (!isEnabled) 
        {
            ResetChannels();
            return;
        }
        
        
        switch(_colorblindModeIndex)
        {
            case 0:
                SetProtanopiaChannels();
                break;
            case 1:
                SetDeuteranopiaChannels();
                break;
            case 2:
                SetTritanopiaChannels();
                break;
        }

    }

    public void UpdateColorblindModeIndex(int index)
    {
        _colorblindModeIndex = index;
    }
    private void ResetChannels()
    {
        // Reset to default (normal vision)
        _channelMixer.redOutRedIn.overrideState = true;
        _channelMixer.redOutGreenIn.overrideState = true;
        _channelMixer.redOutBlueIn.overrideState = true;
        _channelMixer.redOutRedIn.value = 100f;    // HDRP uses 0-200 scale (100 = neutral)
        _channelMixer.redOutGreenIn.value = 0f;
        _channelMixer.redOutBlueIn.value = 0f;

        _channelMixer.greenOutRedIn.overrideState = true;
        _channelMixer.greenOutGreenIn.overrideState = true;
        _channelMixer.greenOutBlueIn.overrideState = true;
        _channelMixer.greenOutRedIn.value = 0f;
        _channelMixer.greenOutGreenIn.value = 100f;
        _channelMixer.greenOutBlueIn.value = 0f;

        _channelMixer.blueOutRedIn.overrideState = true;
        _channelMixer.blueOutGreenIn.overrideState = true;
        _channelMixer.blueOutBlueIn.overrideState = true;
        _channelMixer.blueOutRedIn.value = 0f;
        _channelMixer.blueOutGreenIn.value = 0f;
        _channelMixer.blueOutBlueIn.value = 100f;
    }

    private void SetProtanopiaChannels()
    {
        // Protanopia (Red-blindness) - Values empirically adjusted
        _channelMixer.redOutRedIn.value = 56.7f;   
        _channelMixer.redOutGreenIn.value = 43.3f; 
        _channelMixer.redOutBlueIn.value = 0f;

        _channelMixer.greenOutRedIn.value = 55.8f;
        _channelMixer.greenOutGreenIn.value = 44.2f;
        _channelMixer.greenOutBlueIn.value = 0f;

        _channelMixer.blueOutRedIn.value = 0f;
        _channelMixer.blueOutGreenIn.value = 24.2f;
        _channelMixer.blueOutBlueIn.value = 75.8f;
    }

    private void SetDeuteranopiaChannels()
    {
        // Deuteranopia (Green-blindness)
        _channelMixer.redOutRedIn.value = 62.5f;
        _channelMixer.redOutGreenIn.value = 37.5f;
        _channelMixer.redOutBlueIn.value = 0f;

        _channelMixer.greenOutRedIn.value = 70f;
        _channelMixer.greenOutGreenIn.value = 30f;
        _channelMixer.greenOutBlueIn.value = 0f;

        _channelMixer.blueOutRedIn.value = 0f;
        _channelMixer.blueOutGreenIn.value = 30f;
        _channelMixer.blueOutBlueIn.value = 70f;
    }

    private void SetTritanopiaChannels()
    {
        // Tritanopia (Blue-blindness)
        _channelMixer.redOutRedIn.value = 95f;
        _channelMixer.redOutGreenIn.value = 5f;
        _channelMixer.redOutBlueIn.value = 0f;

        _channelMixer.greenOutRedIn.value = 0f;
        _channelMixer.greenOutGreenIn.value = 43.3f;
        _channelMixer.greenOutBlueIn.value = 56.7f;

        _channelMixer.blueOutRedIn.value = 0f;
        _channelMixer.blueOutGreenIn.value = 47.5f;
        _channelMixer.blueOutBlueIn.value = 52.5f;
    }

    private void EnableOverrideColorblind()
    {
        _channelMixer.redOutRedIn.overrideState = true;
        _channelMixer.redOutGreenIn.overrideState = true;
        _channelMixer.redOutBlueIn.overrideState = true;

        _channelMixer.greenOutRedIn.overrideState = true;
        _channelMixer.greenOutGreenIn.overrideState = true;
        _channelMixer.greenOutBlueIn.overrideState = true;

        _channelMixer.blueOutRedIn.overrideState = true;
        _channelMixer.blueOutGreenIn.overrideState = true;
        _channelMixer.blueOutBlueIn.overrideState = true;
    }
}
