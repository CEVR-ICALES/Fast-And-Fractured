using System;
using UnityEngine;

namespace FastAndFractured
{
    [CreateAssetMenu(menuName = "Fast And Fractured/Map Intro Library", fileName = "MapIntroLibrary")]
    public class MapIntroLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Scenes that play this intro: the singleplayer map and its multiplayer copy.")]
            public string[] scenes;
            public MapIntro intro;
        }

        public const string RESOURCES_PATH = "MapIntroLibrary";

        [Tooltip("Off = no map plays its intro (the match starts straight with the 3-2-1).")]
        [SerializeField] private bool playIntros = true;
        [SerializeField] private Entry[] entries;

        private static MapIntroLibrary _loaded;

        public bool PlayIntros { get => playIntros; set => playIntros = value; }

        public static MapIntroLibrary Load()
        {
            if (_loaded == null) _loaded = Resources.Load<MapIntroLibrary>(RESOURCES_PATH);
            return _loaded;
        }

        public static MapIntro PrefabFor(string sceneName)
        {
            MapIntroLibrary library = Load();
            if (library == null || !library.playIntros || library.entries == null) return null;
            foreach (Entry entry in library.entries)
                if (entry.scenes != null && Array.IndexOf(entry.scenes, sceneName) >= 0)
                    return entry.intro;
            return null;
        }

        public static float DurationFor(string sceneName)
        {
            MapIntro prefab = PrefabFor(sceneName);
            return prefab != null ? prefab.Duration : 0f;
        }
    }
}
