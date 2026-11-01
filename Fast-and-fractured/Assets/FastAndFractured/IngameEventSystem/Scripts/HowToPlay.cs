using System;
using UnityEngine;
using UnityEngine.UI;
using Utilities;

public class HowToPlayPanel : MonoBehaviour
{
    [SerializeField]
    private Image tutorialPanel;
    [SerializeField]
    private Animation fadeOut;
    [SerializeField]
    private Sprite[] tutorialImages;

    private int _imageCount = 0;

    [SerializeField]
    private float delayToStart = 0.3f;

    [SerializeField]
    private float endTutorialTime = 5f;

    [SerializeField]
    private float frameRate = 10;

    private bool _frameEnded = false;

    ITimer _animationLoopTimer;
    private float _timeBetweenFrames;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(tutorialPanel==null||tutorialImages==null||tutorialImages==Array.Empty<Sprite>()){
        Debug.LogWarning("Couldn't show how to play panel. Check if tutorial panel or tutorial images are not null or empty");
        return;
        }
        StartAnimationGif(false);
    }

    void Update()
    {
        if (_animationLoopTimer == null)
            return;
        if (Input.anyKeyDown)
        {
        EndAnimationGif();
        }
        if(_frameEnded)
        {
        _frameEnded=false;
        StartAnimationLoopTimer();
        }
    }

    public void StartAnimationGif(bool loop)
    {
        _timeBetweenFrames = frameRate*Time.deltaTime;
        tutorialPanel.enabled = true;
        TimerSystem.Instance.CreateTimer(delayToStart, onTimerDecreaseComplete: () =>
        {
        tutorialPanel.sprite = tutorialImages[_imageCount];
        if(!loop){
        ITimer endLoopTimer = TimerSystem.Instance.CreateTimer(endTutorialTime, onTimerDecreaseComplete: () =>
        {
          EndAnimationGif();
        });
        }
        StartAnimationLoopTimer();
        });
    }

    private void StartAnimationLoopTimer()
    {
          _animationLoopTimer = TimerSystem.Instance.CreateTimer(_timeBetweenFrames, onTimerDecreaseComplete: () =>
        {   
            _imageCount = (_imageCount+1)<tutorialImages.Length ? _imageCount+1 : 0;
            tutorialPanel.sprite = tutorialImages[_imageCount];
            _frameEnded = true;
        });
    }

    public void EndAnimationGif()
    {
        _animationLoopTimer.StopTimer();
        _animationLoopTimer = null;
        if (fadeOut != null)
            fadeOut.Play();
        else
            tutorialPanel.enabled = false;
    }
}
