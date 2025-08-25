using UnityEngine;

namespace FastAndFractured
{
    public class TimelineSubtitleBase : MonoBehaviour
    {
        public string textLocalaizeCode;
        public string speakerName;
        public float duration;

        private SubtitleData subtitleData;

        private void OnEnable()
        {
            subtitleData = new SubtitleData
            {
                TextLocalizeCode = textLocalaizeCode,
                Duration = duration,
                SpeakerName = speakerName
            };

            SubtitlesManager.Instance.ShowSubtitle(subtitleData);
        }
    }
}

