using DG.Tweening;
using FastAndFractured;
using FMODUnity;
using TMPro;
using UnityEngine;
using Utilities;

public class GameReadyScript : AbstractAutoInitializableMonoBehaviour
{
    [SerializeField] TMP_Text evenText;
    [SerializeField] EventReference soundReference;
    [SerializeField] EventReference soundReady;

    private const float downTextDuration = 0.7f;

    protected override void Initialize()
    {
        base.Initialize();
        if (evenText == null) return;
        Invoke(nameof(StartEventCountdown),IngameEventsManager.EVENT_START_DELAY);
    
    }
    private const float TRANSITION_DURATION = 1;
    // Update is called once per frame
    void Update()
    {

    }
    void AnimateText()
    {
        evenText.rectTransform.DOLocalMoveY(300, downTextDuration).From();
        evenText.DOFade(0, TRANSITION_DURATION).From();
        var playerReference = LevelControllerButBetter.Instance.LocalPlayer;
        if(playerReference){
            SoundManager.Instance.PlayOneShot(soundReference,LevelControllerButBetter.Instance.LocalPlayer.transform.position);
        }
    }
    void StartEventCountdown()
    {
        if (evenText == null) return;

        AnimateText();
        IngameEventsManager.Instance.CreateEvent("3", TRANSITION_DURATION, () =>
        {
            AnimateText();
            IngameEventsManager.Instance.CreateEvent("2", TRANSITION_DURATION, () =>
            {
                AnimateText();
                IngameEventsManager.Instance.CreateEvent("1", TRANSITION_DURATION, () =>
                {
                    AnimateText();
                    IngameEventsManager.Instance.CreateEvent("Events.Start", 5f, () =>
                    {
                        TimerSystem.Instance.CreateTimer(TRANSITION_DURATION, onTimerDecreaseComplete: () =>
                        {
                            soundReference = soundReady;
                        });
                    });
                });

            });
        });
      
       

    }

    protected override void Construct()
    {
     }
}
