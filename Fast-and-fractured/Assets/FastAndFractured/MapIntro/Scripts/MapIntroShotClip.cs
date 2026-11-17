using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FastAndFractured
{
    [Serializable]
    public class MapIntroShotClip : PlayableAsset, ITimelineClipAsset
    {
        public ExposedReference<MapIntroShot> shot;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MapIntroShotBehaviour>.Create(graph);
            playable.GetBehaviour().Shot = shot.Resolve(graph.GetResolver());
            return playable;
        }
    }
}
