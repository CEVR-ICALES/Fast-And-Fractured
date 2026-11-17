using System;
using UnityEngine;

namespace FastAndFractured
{
    [CreateAssetMenu(menuName = "Fast And Fractured/Audio Presentation Profile", fileName = "AudioPresentationProfile")]
    public sealed class AudioPresentationProfile : ScriptableObject
    {
        public const string RESOURCES_PATH = "AudioPresentationProfile";

        [SerializeField] private VehicleAudioSettings vehicle = new VehicleAudioSettings();
        [SerializeField] private UiAudioSettings ui = new UiAudioSettings();
        [SerializeField] private AbilityAudioSettings abilities = new AbilityAudioSettings();
        [SerializeField] private ChickenAudioSettings chicken = new ChickenAudioSettings();
        [SerializeField] private AudioZoneSettings zones = new AudioZoneSettings();

        private static AudioPresentationProfile _loaded;
        private static bool _usingFallback;

        public VehicleAudioSettings Vehicle => vehicle;
        public UiAudioSettings Ui => ui;
        public AbilityAudioSettings Abilities => abilities;
        public ChickenAudioSettings Chicken => chicken;
        public AudioZoneSettings Zones => zones;

        public static AudioPresentationProfile Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<AudioPresentationProfile>(RESOURCES_PATH);
            if (_loaded != null) return _loaded;
            Debug.LogWarning("Audio Presentation Profile is missing from Resources. Using the default presentation settings.");
            _loaded = CreateInstance<AudioPresentationProfile>();
            _loaded.hideFlags = HideFlags.HideAndDontSave;
            _usingFallback = true;
            return _loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            if (_usingFallback && _loaded != null)
            {
                if (Application.isPlaying) Destroy(_loaded);
                else DestroyImmediate(_loaded);
            }
            _loaded = null;
            _usingFallback = false;
        }
    }

    [Serializable]
    public sealed class VehicleAudioSettings
    {
        #region Engine
        [Header("Engine mix")]
        [Min(0f)] [SerializeField] private float initialEngineVolume = 0.8f;
        [Min(0f)] [SerializeField] private float localEngineVolume = 0.8f;
        [Min(0f)] [SerializeField] private float remoteEngineVolume = 0.45f;
        [Min(0f)] [SerializeField] private float minimumEnginePitch = 0.85f;
        [Min(0f)] [SerializeField] private float maximumEnginePitch = 1.25f;
        [Tooltip("Speed in world units per second at which the engine reaches maximum pitch.")]
        [Min(float.Epsilon)] [SerializeField] private float fullPitchSpeed = 25f;
        [Tooltip("Speed in world units per second above which the engine enters Running.")]
        [Min(0f)] [SerializeField] private float engineRunningSpeed = 0.5f;

        public float InitialEngineVolume => initialEngineVolume;
        public float LocalEngineVolume => localEngineVolume;
        public float RemoteEngineVolume => remoteEngineVolume;
        public float EngineRunningSpeed => engineRunningSpeed;

        public float CalculateEnginePitch(float speed) =>
            Mathf.Lerp(minimumEnginePitch, maximumEnginePitch, Mathf.Clamp01(speed / fullPitchSpeed));
        #endregion

        #region Collision
        [Header("Collision response")]
        [Min(0f)] [SerializeField] private float collisionCooldownSeconds = 0.18f;
        [Tooltip("Minimum relative speed in world units per second that produces collision audio.")]
        [Min(0f)] [SerializeField] private float minimumCollisionSpeed = 2f;
        [Min(0f)] [SerializeField] private float minimumCollisionVolume = 0.2f;
        [Min(0f)] [SerializeField] private float maximumCollisionVolume = 1f;
        [Tooltip("Lower mass bound in kilograms used by the audio response, not by physics.")]
        [Min(0f)] [SerializeField] private float minimumCollisionMass = 1f;
        [Tooltip("Reference mass in kilograms for the collision audio response.")]
        [Min(float.Epsilon)] [SerializeField] private float collisionReferenceMass = 100f;
        [Tooltip("Reference relative speed in world units per second for full collision gain.")]
        [Min(float.Epsilon)] [SerializeField] private float collisionReferenceSpeed = 15f;

        public float CollisionCooldownSeconds => collisionCooldownSeconds;
        public float MinimumCollisionSpeed => minimumCollisionSpeed;

        public float CalculateCollisionVolume(float speed, float mass) =>
            Mathf.Lerp(minimumCollisionVolume, maximumCollisionVolume,
                Mathf.Clamp01(speed * Mathf.Sqrt(Mathf.Max(mass, minimumCollisionMass) / collisionReferenceMass) / collisionReferenceSpeed));
        #endregion

        #region Feedback
        [Header("Hurt and falling feedback")]
        [Min(0f)] [SerializeField] private float hurtCooldownSeconds = 1.2f;
        [Min(0f)] [SerializeField] private float fallingDelaySeconds = 0.45f;
        [Tooltip("Vertical speed in world units per second below which falling wind can start.")]
        [SerializeField] private float fallingVerticalSpeed = -5f;

        public float HurtCooldownSeconds => hurtCooldownSeconds;
        public float FallingDelaySeconds => fallingDelaySeconds;
        public float FallingVerticalSpeed => fallingVerticalSpeed;
        #endregion
    }

    [Serializable]
    public sealed class UiAudioSettings
    {
        [Min(0f)] [SerializeField] private float clickVolume = 0.7f;
        [Min(0f)] [SerializeField] private float hoverVolume = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float pitchVariation = 0.06f;
        [Min(0f)] [SerializeField] private float hoverCooldownSeconds = 0.08f;

        public float ClickVolume => clickVolume;
        public float HoverVolume => hoverVolume;
        public float HoverCooldownSeconds => hoverCooldownSeconds;

        public float CalculatePitch(double sample) => 1f + ((float)sample * 2f - 1f) * pitchVariation;
    }

    [Serializable]
    public sealed class AbilityAudioSettings
    {
        [Header("Tomato layers (shared by singleplayer and multiplayer)")]
        [Min(0f)] [SerializeField] private float tomatoLaunchVolume = 1.25f;
        [Min(0f)] [SerializeField] private float tomatoImpactVolume = 1.2f;
        [Min(0f)] [SerializeField] private float tomatoSplatVolume = 0.6f;
        [Header("Chicken launch")]
        [Min(0f)] [SerializeField] private float chickenLaunchVolume = 1.25f;
        [Min(0f)] [SerializeField] private float chickenLaunchPitch = 1.2f;

        public float TomatoLaunchVolume => tomatoLaunchVolume;
        public float TomatoImpactVolume => tomatoImpactVolume;
        public float TomatoSplatVolume => tomatoSplatVolume;
        public float ChickenLaunchVolume => chickenLaunchVolume;
        public float ChickenLaunchPitch => chickenLaunchPitch;
    }

    [Serializable]
    public sealed class ChickenAudioSettings
    {
        [Min(0f)] [SerializeField] private float voiceVolume = 0.4f;
        [Min(0f)] [SerializeField] private float stepsVolume = 0.5f;
        [Min(0f)] [SerializeField] private float stepsDelaySeconds = 1.2f;
        [Tooltip("Minimum horizontal speed in world units per second for footsteps.")]
        [Min(0f)] [SerializeField] private float minimumWalkingSpeed = 0.6f;
        [Min(0f)] [SerializeField] private float cryCooldownSeconds = 0.4f;

        public float VoiceVolume => voiceVolume;
        public float StepsVolume => stepsVolume;
        public float StepsDelaySeconds => stepsDelaySeconds;
        public float MinimumWalkingSpeed => minimumWalkingSpeed;
        public float CryCooldownSeconds => cryCooldownSeconds;
    }

    [Serializable]
    public sealed class AudioZoneSettings
    {
        [Tooltip("Horizontal padding per side, in world units.")]
        [Min(0f)] [SerializeField] private float marketPadding = 15f;
        [Min(0f)] [SerializeField] private float constructionPadding = 35f;
        [Min(0f)] [SerializeField] private float bridgePadding = 1f;
        [Tooltip("Total additional vertical size in world units, not padding per side.")]
        [Min(0f)] [SerializeField] private float verticalExpansion = 20f;
        [Min(0f)] [SerializeField] private float volume = 0.4f;

        public float MarketPadding => marketPadding;
        public float ConstructionPadding => constructionPadding;
        public float BridgePadding => bridgePadding;
        public float Volume => volume;

        public Vector3 ExpansionFor(float horizontalPadding) => new Vector3(horizontalPadding * 2f, verticalExpansion, horizontalPadding * 2f);
    }
}
