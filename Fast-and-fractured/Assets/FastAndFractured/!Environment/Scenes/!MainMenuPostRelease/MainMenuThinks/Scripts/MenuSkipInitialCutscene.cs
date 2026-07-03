using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using UnityEngine.InputSystem;
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
        if(Input.anyKeyDown && !AlreadySkipped)
        {
            SkipTimeline();
            _alreadySkipped = true;
        }
    }

    private void SkipTimeline()
    {
        if (timeLine != null) 
        {
            timeLine.time = timeLine.duration;
            timeLine.Evaluate();
            if(timeLine.gameObject.name == "PlayableDirector") WinLoseScreenBehaviour.Instance.ShowMenu();
            
            skipText.SetActive(false);
        }
    }
}
