using DG.Tweening;
using FastAndFractured;
using TMPro;
using UnityEngine;

public class GameReadyScript : MonoBehaviour
{
    [SerializeField] TMP_Text evenText;
    private const float downTextDuration = 0.7f;
    void Start()
    {
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
    }
    void StartEventCountdown()
    {
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
                     
                });

            });
        });
      
       

    }
}
