using System;
using UnityEngine;

namespace FastAndFractured
{
    public interface IPreMatchStage
    {
        float Duration { get; }
        float Remaining { get; }
        void Play(Action onFinished);
        void Skip();
        void OnCountdownStarted();
    }

    /// <summary>
    /// Something shown before the 3-2-1, after the map intro (e.g. a mechanic preview). Put it in the map scene and
    /// PreMatchSequence plays it; call Play's callback when done. For a plain timeline use TimelinePreMatchStep.
    /// </summary>
    public abstract class PreMatchStep : MonoBehaviour, IPreMatchStage
    {
        [Tooltip("Steps of the same scene play from the lowest order up, after the map intro.")]
        [SerializeField] private int order;

        public int Order => order;
        public abstract float Duration { get; }
        public abstract float Remaining { get; }
        public abstract void Play(Action onFinished);
        public abstract void Skip();
        public virtual void OnCountdownStarted() { }
    }
}
