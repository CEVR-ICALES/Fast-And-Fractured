using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class MenuSkipInitialCutscene : MonoBehaviour
{
    public PlayableDirector timeLine;
    private void Update()
    {
        if(Input.anyKeyDown)
        {
            SkipTimeline();
        }
    }

    private void SkipTimeline()
    {
        if (timeLine != null)
            timeLine.time = timeLine.duration;
    }
}
