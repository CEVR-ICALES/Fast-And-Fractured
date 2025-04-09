using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SubtitleTest : MonoBehaviour
    {
        [SerializeField] private string textLocalizeCode;
        [SerializeField] private string speakerName;

        private SubtitleData subtitleData;

        public void SendSubtitle()
        {
            subtitleData = new SubtitleData
            {
                TextLocalizeCode = textLocalizeCode,
                Duration = 5f,
                TextColor = Color.white,
                SpeakerName = speakerName
            };
            SubtitlesManager.Instance.ShowSubtitle(subtitleData);
        }
    }
}

