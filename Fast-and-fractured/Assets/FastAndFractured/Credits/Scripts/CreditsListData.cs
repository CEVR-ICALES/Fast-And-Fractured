using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "CreditsListData", menuName = "Credits/CreditsListData", order = 1)]
    public class CreditsListData : ScriptableObject
    {
        [System.Serializable]
        public class CreditsEntry
        {
            public string title;
            public List<string> names = new List<string>();
        }

        public List<CreditsEntry> creditsEntry = new List<CreditsEntry>();
    }
}