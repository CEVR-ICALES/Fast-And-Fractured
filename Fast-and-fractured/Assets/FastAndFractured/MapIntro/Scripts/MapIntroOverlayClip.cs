using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FastAndFractured
{
    // The clip's ease in / ease out in the timeline is the animation: the part shows at the clip's weight.
    [Serializable]
    public class MapIntroOverlayClip : PlayableAsset, ITimelineClipAsset
    {
        public MapIntroOverlayPart part;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MapIntroOverlayBehaviour>.Create(graph);
            playable.GetBehaviour().Part = part;
            return playable;
        }
    }
}
