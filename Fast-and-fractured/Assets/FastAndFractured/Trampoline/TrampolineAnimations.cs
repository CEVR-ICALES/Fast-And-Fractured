using FastAndFractured;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

public class TrampolineAnimations : MonoBehaviour
{
    [SerializeField]
    private Animation trampolineAnimations;
    [SerializeField]
    private AnimationClip[] trampolines = new AnimationClip[2];
    public UnityEvent<float> releaseAnimation;
    private ITimer countdownTimer = null;
    [SerializeField]
    private landingCheck landingCheck;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       if(trampolineAnimations== null)
        {
            trampolineAnimations = transform.parent.GetComponentInChildren<Animation>();
        }
       releaseAnimation.AddListener(TrampolineReleaseAnimation);
    }

    private void AnimateClip(AnimationClip clip)
    {
        trampolineAnimations.clip = clip;
        trampolineAnimations.Play();
    }
   
    private void TrampolineReleaseAnimation(float countdownTime)
    {
        AnimateClip(trampolines[1]);
        countdownTimer = TimerSystem.Instance.CreateTimer(countdownTime,
            onTimerDecreaseComplete: () =>
            {
                countdownTimer = null;
            });
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject != landingCheck.gameObject)
        {
            if (countdownTimer == null)
            {
                AnimateClip(trampolines[0]);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(countdownTimer==null)
        {
            AnimateClip(trampolines[1]);
        }
    }
}
