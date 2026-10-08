using FastAndFractured;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Playables;
using Utilities;

public class MenuSkipInitialCutscene : AbstractSingleton<MenuSkipInitialCutscene>
{
    public PlayableDirector timeLine;
    public GameObject skipText;

    // make it get set for alreadty skipped
    public bool AlreadySkipped
    {
        get => _alreadySkipped;
        set => _alreadySkipped = value;
    }
    private bool _alreadySkipped;
    private BaseInputModule _suppressedInputModule;

    protected override void Construct()
    {
        base.Construct();
        _alreadySkipped = false;
    }
    protected override void Initialize()
    {
        
    }
    private void Update()
    {
        RestoreInputModuleAfterButtonRelease();

        if(Input.anyKeyDown && !AlreadySkipped)
        {
            _alreadySkipped = true;
            SkipTimeline();
        }
    }

    private void SkipTimeline()
    {
        if (timeLine != null) 
        {
            SuppressInputModuleUntilButtonRelease();
            timeLine.time = timeLine.duration;
            timeLine.Evaluate();
            if(timeLine.gameObject.name == "PlayableDirector") WinLoseScreenBehaviour.Instance.ShowMenu();
            
            skipText.SetActive(false);
        }
    }

    private void SuppressInputModuleUntilButtonRelease()
    {
        EventSystem eventSystem = EventSystem.current;
        _suppressedInputModule = eventSystem != null ? eventSystem.currentInputModule : null;
        if (_suppressedInputModule == null || !_suppressedInputModule.enabled)
        {
            _suppressedInputModule = null;
            return;
        }

        _suppressedInputModule.enabled = false;
    }

    private void RestoreInputModuleAfterButtonRelease()
    {
        if (_suppressedInputModule == null || AnyButtonIsPressed())
            return;

        _suppressedInputModule.enabled = true;
        _suppressedInputModule = null;
    }

    private static bool AnyButtonIsPressed()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            foreach (InputControl control in device.allControls)
            {
                if (control is ButtonControl button && button.isPressed)
                    return true;
            }
        }

        return false;
    }
}
