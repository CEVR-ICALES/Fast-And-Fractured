using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FastAndFractured
{
    public enum MapIntroOverlayPart
    {
        LETTERBOX,
        TITLE
    }

    [TrackColor(0.35f, 0.62f, 1f)]
    [TrackClipType(typeof(MapIntroOverlayClip))]
    [TrackBindingType(typeof(MapIntroOverlay))]
    public class MapIntroOverlayTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<MapIntroOverlayMixer>.Create(graph, inputCount);
    }

    public class MapIntroOverlayBehaviour : PlayableBehaviour
    {
        public MapIntroOverlayPart Part;
    }

    public class MapIntroOverlayMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is MapIntroOverlay overlay)) return;
            float letterbox = 0f, title = 0f;
            bool hasLetterbox = false, hasTitle = false, titleLeaving = false;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                var input = (ScriptPlayable<MapIntroOverlayBehaviour>)playable.GetInput(i);
                float weight = playable.GetInputWeight(i);
                if (input.GetBehaviour().Part == MapIntroOverlayPart.TITLE)
                {
                    hasTitle = true;
                    if (weight <= title) continue;
                    title = weight;
                    titleLeaving = input.GetTime() > input.GetDuration() * 0.5;
                }
                else
                {
                    hasLetterbox = true;
                    letterbox = Mathf.Max(letterbox, weight);
                }
            }
            if (hasLetterbox) overlay.SetLetterbox(letterbox);
            if (hasTitle) overlay.SetTitle(title, titleLeaving);
        }
    }
}
