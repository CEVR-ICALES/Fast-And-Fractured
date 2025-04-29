using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class MenuSkipInitialCutscene : MonoBehaviour
{
    public PlayableDirector timeLine;
    [SerializeField] private GameObject skipText;

    private bool alreadySkipped = false;

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
            skipText.SetActive(false);
        }
    }
}
