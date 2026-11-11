using System;
using FastAndFractured;
using UnityEngine;
using UnityEngine.UI;
using Utilities;

public class AnimatorPanelHandle : PreMatchStep
{
    [SerializeField]
    private Image tutorialImage;
    [SerializeField]
    private Animation fadeOut;

    [SerializeField]
    private AnimationGif[] animationsGif;

    [Tooltip("Online the server makes everyone wait this long before the 3-2-1, so a long or looping tutorial is cut here.")]
    [SerializeField]
    private float maxOnlineSeconds = 8f;

    private AnimationGif _currentAnimationGif;

    private AnimationGifData _currentAnimationGifData;

    private int _animationGifCount = 0;
    private bool _frameEnded = false;
    private bool _playing, _fading, _waitingToStart;
    private Action _onFinished;

    ITimer _animationLoopTimer;

    ITimer _endLoopTimer;
    ITimer _delayTimer;
    ITimer _onlineLimitTimer;
    ITimer _fadeTimer;
    private const string TUTORIAL_TAG = "Tutorial";

    public override bool Skippable => true;
    public override float Duration => IsOnline ? Mathf.Min(TotalSeconds, maxOnlineSeconds) : TotalSeconds;

    public override float Remaining
    {
        get
        {
            if (!_playing) return 0f;
            if (_fading) return TimeLeft(_fadeTimer);
            float left = SecondsLeftInCurrentGif + FadeSeconds;
            for (int i = _animationGifCount + 1; i < animationsGif.Length; i++) left += SecondsOf(animationsGif[i]);
            return _onlineLimitTimer != null ? Mathf.Min(left, TimeLeft(_onlineLimitTimer) + FadeSeconds) : left;
        }
    }

    private float SecondsLeftInCurrentGif
    {
        get
        {
            if (_currentAnimationGifData == null) return 0f;
            float shown = _currentAnimationGifData.Loop ? float.PositiveInfinity : _currentAnimationGifData.EndAnimationTime;
            return _waitingToStart ? TimeLeft(_delayTimer) + shown : _currentAnimationGifData.Loop ? shown : TimeLeft(_endLoopTimer);
        }
    }

    private float FadeSeconds => fadeOut != null && fadeOut.clip != null ? fadeOut.clip.length : 0f;

    private float TotalSeconds
    {
        get
        {
            float total = FadeSeconds;
            if (animationsGif != null)
                foreach (AnimationGif gif in animationsGif) total += SecondsOf(gif);
            return total;
        }
    }

    private static bool IsOnline => FishNet.InstanceFinder.IsServerStarted || FishNet.InstanceFinder.IsClientStarted;

    private static float TimeLeft(ITimer timer) => timer != null && timer.GetData().IsRunning ? timer.GetData().CurrentTime : 0f;

    // A looping gif only ends on a key press.
    private static float SecondsOf(AnimationGif gif)
    {
        AnimationGifData data = gif?.GetAnimationData();
        if (data == null || gif.CheckIfArrayIsEmptyOrNull()) return 0f;
        return data.Loop ? float.PositiveInfinity : data.DelayToStart + data.EndAnimationTime;
    }

    public override void Play(Action onFinished)
    {
        if (!FindPanel() || animationsGif == null || animationsGif.Length == 0)
        {
            Debug.LogWarning("Couldn't show how to play panel. Check if tutorial panel or tutorial images are not null or empty");
            onFinished?.Invoke();
            return;
        }
        _onFinished = onFinished;
        _playing = true;
        _fading = false;
        _animationGifCount = 0;
        tutorialImage.enabled = false;
        if (IsOnline && TotalSeconds > maxOnlineSeconds)
            _onlineLimitTimer = TimerSystem.Instance.CreateTimer(Mathf.Max(0f, maxOnlineSeconds - FadeSeconds), onTimerDecreaseComplete: StartFade);
        StartAnimationGif();
    }

    private bool FindPanel()
    {
        if (tutorialImage == null)
            foreach (GameObject panel in GameObject.FindGameObjectsWithTag(TUTORIAL_TAG))
                if (panel.TryGetComponent(out tutorialImage)) break;
        if (tutorialImage == null) return false;
        if (fadeOut == null) tutorialImage.TryGetComponent(out fadeOut);
        return true;
    }

    void Update()
    {
        if (_animationLoopTimer == null)
            return;
        if (_frameEnded)
        {
            _frameEnded = false;
            StartAnimationLoopTimer();
        }
    }

    public void StartAnimationGif()
    {
        _currentAnimationGif = animationsGif[_animationGifCount];
        _currentAnimationGifData = _currentAnimationGif.GetAnimationData();
        if (_currentAnimationGifData == null || _currentAnimationGif.CheckIfArrayIsEmptyOrNull())
        {
            EndAnimationGif();
            return;
        }
        _waitingToStart = true;
        _delayTimer = TimerSystem.Instance.CreateTimer(_currentAnimationGifData.DelayToStart, onTimerDecreaseComplete: () =>
        {
            _waitingToStart = false;
            tutorialImage.enabled = true;
            if (!_currentAnimationGifData.Loop)
            {
                _endLoopTimer = TimerSystem.Instance.CreateTimer(_currentAnimationGifData.EndAnimationTime, onTimerDecreaseComplete: () =>
                {
                    EndAnimationGif();
                });
            }
            tutorialImage.sprite = _currentAnimationGif.GetCurrentFrame();
            StartAnimationLoopTimer();
        });
    }

    private void StartAnimationLoopTimer()
    {
        _animationLoopTimer = TimerSystem.Instance.CreateTimer(_currentAnimationGifData.GetTimeBetweenFrames(), onTimerDecreaseComplete: () =>
        {
            tutorialImage.sprite = _currentAnimationGif.GetNextFrame();
            _frameEnded = true;
        });
    }

    public override void OnSkipPressed()
    {
        if (_playing && !_fading) EndAnimationGif();
    }

    public void EndAnimationGif()
    {
        StopGifTimers();
        int nextAnimationGifCount = _animationGifCount + 1;
        if (nextAnimationGifCount >= animationsGif.Length)
        {
            StartFade();
        }
        else
        {
            _animationGifCount = nextAnimationGifCount;
            tutorialImage.enabled = false;
            StartAnimationGif();
        }
    }

	private void StopGifTimers()
    {
        _frameEnded = false;
        _waitingToStart = false;
        _delayTimer?.StopTimer();
        _delayTimer = null;
        _animationLoopTimer?.StopTimer();
        _animationLoopTimer = null;
        _endLoopTimer?.StopTimer();
        _endLoopTimer = null;
    }

    private void StartFade()
    {
        if (!_playing || _fading) return;
        StopGifTimers();
        _onlineLimitTimer?.StopTimer();
        _onlineLimitTimer = null;
        _fading = true;
        if (fadeOut != null && FadeSeconds > 0f && tutorialImage.enabled)
        {
            fadeOut.Play();
            _fadeTimer = TimerSystem.Instance.CreateTimer(FadeSeconds, onTimerDecreaseComplete: Finish);
        }
        else
        {
            Finish();
        }
    }

    public override void Skip()
    {
        if (!_playing) return;
        if (fadeOut != null) fadeOut.Stop();
        Finish();
    }

    private void Finish()
    {
        if (!_playing) return;
        _playing = false;
        StopGifTimers();
        _onlineLimitTimer?.StopTimer();
        _onlineLimitTimer = null;
        _fadeTimer?.StopTimer();
        _fadeTimer = null;
        if (tutorialImage != null) tutorialImage.enabled = false;
        Action onFinished = _onFinished;
        _onFinished = null;
        onFinished?.Invoke();
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
        return sprites==null || sprites.Length == 0;
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
        return 1f/frameRate;
    }
}
