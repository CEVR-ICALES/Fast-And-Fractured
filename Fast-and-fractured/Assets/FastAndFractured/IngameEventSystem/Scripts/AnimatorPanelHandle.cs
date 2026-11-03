using System;
using UnityEngine;
using UnityEngine.UI;
using Utilities;

public class AnimatorPanelHandle : MonoBehaviour
{
    [SerializeField]
    private Image tutorialImage;
    [SerializeField]
    private Animation fadeOut;

    [SerializeField]
    private AnimationGif[] animationsGif;

    private AnimationGif _currentAnimationGif;

    private AnimationGifData _currentAnimationGifData;

    private int _animationGifCount = 0;
    private bool _frameEnded = false;

    ITimer _animationLoopTimer;

    ITimer _endLoopTimer;
    private const string TUTORIAL_TAG = "Tutorial";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (tutorialImage == null)
        {

         GameObject tutorialPanel = GameObject.FindGameObjectWithTag(TUTORIAL_TAG);
         tutorialPanel.TryGetComponent(out tutorialImage);
         tutorialPanel.TryGetComponent(out fadeOut);   
        }
        if(animationsGif==Array.Empty<AnimationGif>()||animationsGif==null){
        Debug.LogWarning("Couldn't show how to play panel. Check if tutorial panel or tutorial images are not null or empty");
        return;
        }
        tutorialImage.enabled = false;
        StartAnimationGif();
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

    public void StartAnimationGif()
    {
        
        _currentAnimationGif = animationsGif[_animationGifCount];
        _currentAnimationGifData = _currentAnimationGif.GetAnimationData();
        if(_currentAnimationGifData==null||_currentAnimationGif.CheckIfArrayIsEmptyOrNull())
        return;
        tutorialImage.enabled = true;
        TimerSystem.Instance.CreateTimer(_currentAnimationGifData.DelayToStart, onTimerDecreaseComplete: () =>
        {
        if(!_currentAnimationGifData.Loop){
        _endLoopTimer = TimerSystem.Instance.CreateTimer(_currentAnimationGifData.EndAnimationTime, onTimerDecreaseComplete: () =>
        {
          EndAnimationGif();
        });
        }
        StartAnimationLoopTimer();
        });
    }

    private void StartAnimationLoopTimer()
    {
        tutorialImage.sprite = _currentAnimationGif.GetCurrentFrame();
          _animationLoopTimer = TimerSystem.Instance.CreateTimer(_currentAnimationGifData.GetTimeBetweenFrames(), onTimerDecreaseComplete: () =>
        {   
            _currentAnimationGif.CountNextFrame();
            _frameEnded = true;
        });
    }

    public void EndAnimationGif()
    {
        _animationLoopTimer.StopTimer();
        _animationLoopTimer = null;
        _endLoopTimer.StopTimer();
        _endLoopTimer = null;
        int nextAnimationGifCount = _animationGifCount+1;
        if(nextAnimationGifCount==animationsGif.Length){
        if (fadeOut != null)
            fadeOut.Play();
        else
            tutorialImage.enabled = false;
        }
        else
        {
            _animationGifCount=nextAnimationGifCount;
            StartAnimationGif();
        }
    }
}

[Serializable]
public class AnimationGif
{
    [SerializeField]
    private Sprite[] sprites;

    private int _spriteCount = 0;

    [SerializeField]
    private AnimationGifData animationGifData;
    

    public AnimationGifData GetAnimationData()
    {
        return animationGifData;
    }
    public bool CheckIfArrayIsEmptyOrNull()
    {
        return sprites==null || sprites == Array.Empty<Sprite>();
    }

    public bool CheckIfIsOutOfRange(int spriteNum)
    {
        return spriteNum < sprites.Length;
    }

    public Sprite GetCurrentFrame()
    {
           if(CheckIfArrayIsEmptyOrNull())
            return null;
        return sprites[_spriteCount];
    }
    public Sprite GetNextFrame()
    {
        if(CheckIfArrayIsEmptyOrNull())
            return null;
        int nextSpriteCount = _spriteCount +1;
        _spriteCount = CheckIfIsOutOfRange(nextSpriteCount) ? nextSpriteCount:0;
        return sprites[_spriteCount];
    }

    public void CountNextFrame()
    {
        int nextSpriteCount = _spriteCount +1;
        _spriteCount = CheckIfIsOutOfRange(nextSpriteCount) ? nextSpriteCount:0;
    }

    public Sprite GetSpecificFrame(int spriteNum)
    {
           if(CheckIfArrayIsEmptyOrNull())
            return null;
        return CheckIfIsOutOfRange(spriteNum) ? sprites[spriteNum] : null;
    }
}

[Serializable]
public class AnimationGifData
{
    [SerializeField]
    private float delayToStart = 0.3f;
    [SerializeField]
    private float frameRate = 10f;
    [SerializeField]
    private float endAnimationTime = 50f;

    [SerializeField]
    private bool loop = false;

    public float DelayToStart {get=>delayToStart;}

    public float FrameRate {get=> frameRate;}

    public float EndAnimationTime {get=>endAnimationTime;}

    public bool Loop {get=>loop; set=> loop = value;}

    public float GetTimeBetweenFrames()
    {
        return frameRate*Time.deltaTime;
    }
}
