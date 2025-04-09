using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class ColorBlindModeController : MonoBehaviour
{
    [Header("Volume Settings")]
    [SerializeField] private Volume globalVolume;
    private ChannelMixer _channelMixer;

    private const string COLORBLIND_KEY = "ColorBlindMode";

    private void Awake()
    {
        UpdateColorBlindMode(PlayerPrefs.GetInt(COLORBLIND_KEY, 0) == 1);
    }

    public void UpdateColorBlindMode(bool isEnabled)
    {
        if (!globalVolume.profile.TryGet(out _channelMixer))
        {
            Debug.LogError("Channel Mixer not found in HDRP Volume Profile!");
            return;
        }

        if (!isEnabled) 
        {
            ResetChannels();
            return;
        }
        
        // if we decide to add more colorblind modes we can do a switch case
        SetProtanopiaChannels();

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
        _channelMixer.redOutRedIn.value = 56.7f;   // ↓ Red contribution
        _channelMixer.redOutGreenIn.value = 43.3f; // ↑ Green compensates
        _channelMixer.redOutBlueIn.value = 0f;

        _channelMixer.greenOutRedIn.value = 55.8f;
        _channelMixer.greenOutGreenIn.value = 44.2f;
        _channelMixer.greenOutBlueIn.value = 0f;

        _channelMixer.blueOutRedIn.value = 0f;
        _channelMixer.blueOutGreenIn.value = 24.2f;
        _channelMixer.blueOutBlueIn.value = 75.8f;
    }


}
