using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace FastAndFractured
{
    public static class ShadowReach
    {
        // Where the authored cascades end (DefaultSettingsVolumeProfile: 1000 m split at 5 %, 15 % and 30 %).
        public static readonly float[] AUTHORED_CASCADE_ENDS = { 50f, 150f, 300f };
        public static readonly float[] DISTANCE_BY_LEVEL = { 150f, 150f, 300f };
        private const float VOLUME_PRIORITY = 1000f;
        private const string VOLUME_NAME = "[ShadowReach]";

        private static Volume _volume;
        private static HDShadowSettings _settings;

        public static bool IsLimited(int level) => level >= 0 && level < DISTANCE_BY_LEVEL.Length;

        public static float Distance(int level) => DISTANCE_BY_LEVEL[level];

        public static int Cascades(int level)
        {
            int cascades = 1;
            foreach (float end in AUTHORED_CASCADE_ENDS)
                if (end < DISTANCE_BY_LEVEL[level]) cascades++;
            return cascades;
        }

        public static float Split(int level, int cascade) =>
            Mathf.Clamp01(AUTHORED_CASCADE_ENDS[cascade] / DISTANCE_BY_LEVEL[level]);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _volume = null;
            _settings = null;
            GraphicsQuality.LevelChanged -= Apply;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GraphicsQuality.LevelChanged -= Apply;
            GraphicsQuality.LevelChanged += Apply;
            Apply(GraphicsQuality.CurrentLevel);
        }

        public static void Apply(int level)
        {
            if (!IsLimited(level))
            {
                if (_volume != null) _volume.enabled = false;
                return;
            }
            EnsureVolume();
            _settings.maxShadowDistance.value = Distance(level);
            _settings.cascadeShadowSplitCount.value = Cascades(level);
            _settings.cascadeShadowSplit0.value = Split(level, 0);
            _settings.cascadeShadowSplit1.value = Split(level, 1);
            _settings.cascadeShadowSplit2.value = Split(level, 2);
            _volume.enabled = true;
        }

        private static void EnsureVolume()
        {
            if (_volume != null) return;
            var go = new GameObject(VOLUME_NAME);
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = VOLUME_PRIORITY;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _settings = profile.Add<HDShadowSettings>();
            _settings.maxShadowDistance.overrideState = true;
            _settings.cascadeShadowSplitCount.overrideState = true;
            _settings.cascadeShadowSplit0.overrideState = true;
            _settings.cascadeShadowSplit1.overrideState = true;
            _settings.cascadeShadowSplit2.overrideState = true;
            _volume.sharedProfile = profile;
        }
    }
}
