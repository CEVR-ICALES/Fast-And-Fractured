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

    private bool alreadySkipped;

    protected override void Awake()
    {
        base.Awake();

        alreadySkipped = false;
    }

    private void Update()
    {
        if(Input.anyKeyDown && !alreadySkipped)
        {
            SkipTimeline();
            alreadySkipped = true;
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
