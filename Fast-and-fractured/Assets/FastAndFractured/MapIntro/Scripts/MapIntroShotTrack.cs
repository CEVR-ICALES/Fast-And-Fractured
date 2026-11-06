using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FastAndFractured
{
    [TrackColor(1f, 0.78f, 0.35f)]
    [TrackClipType(typeof(MapIntroShotClip))]
    public class MapIntroShotTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<MapIntroShotMixer>.Create(graph, inputCount);

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            foreach (TimelineClip clip in GetClips())
            {
                if (!(clip.asset is MapIntroShotClip shotClip)) continue;
                MapIntroShot shot = shotClip.shot.Resolve(director);
                var shotCamera = shot != null ? shot.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true) : null;
                if (shotCamera == null) continue;
                driver.AddFromName<Transform>(shotCamera.gameObject, "m_LocalPosition");
                driver.AddFromName<Transform>(shotCamera.gameObject, "m_LocalRotation");
                driver.AddFromName(shotCamera, "Lens.FieldOfView");
            }
            base.GatherProperties(director, driver);
        }
    }

    public class MapIntroShotBehaviour : PlayableBehaviour
    {
        public MapIntroShot Shot;
    }

    public class MapIntroShotMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                var input = (ScriptPlayable<MapIntroShotBehaviour>)playable.GetInput(i);
                MapIntroShot shot = input.GetBehaviour().Shot;
                double duration = input.GetDuration();
                if (shot != null && duration > 0) shot.Evaluate((float)(input.GetTime() / duration));
            }
        }
    }
}
