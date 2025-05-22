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
            public string titleLocalizationKey;
            public float sizeMultiplier;
            public List<NameWithSize> names = new List<NameWithSize>();
        }

        public List<CreditsEntry> creditsEntry = new List<CreditsEntry>();
        [System.Serializable]
        public class NameWithSize
        {
            public string name;
            public float sizeMultiplier;
        }
    }
}