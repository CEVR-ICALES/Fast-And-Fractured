using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Replicates explosion screen shake: the multiplayer wrappers already broadcast each explosion, so every peer
    /// emits the Cinemachine impulse locally at the blast and the listener attenuates it by distance.
    /// </summary>
    public static class NetworkedScreenShake
    {
        private const float TEMP_SOURCE_LIFETIME = 3f;

        private static readonly FieldInfo ProfilesField = typeof(ScreenShakeSourceController)
            .GetField("screenShakeProfiles", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SourceField = typeof(ScreenShakeSourceController)
            .GetField("cinemachineImpulseSource", BindingFlags.NonPublic | BindingFlags.Instance);

        // Profiles are per emitter and not under Resources: scan the emitters in the loaded map.
        private static readonly System.Collections.Generic.Dictionary<ScreenShakeProfileType, ScreenShakeProfile> ProfileCache = new();

        private static ScreenShakeSourceController _referenceController;
        private static bool _loggedMissingSetup;

        // Domain reload is off: statics would keep pointing at destroyed objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _referenceController = null;
            _loggedMissingSetup = false;
            ProfileCache.Clear();
        }

        /// <summary>Emits a shake impulse at a world position on this peer; safe from an ObserversRpc.</summary>
        public static void PlayAtPosition(ScreenShakeProfileType profileType, Vector3 worldPosition)
        {
            // Not CameraBehaviours.Instance: it creates a phantom that disables the real camera controller.
            var cameraBehaviours = CameraBehaviours.ExistingInstance;
            if (cameraBehaviours == null)
                return; // no local camera yet (still loading, or a dedicated server) — nothing to shake

            ScreenShakeProfile profile = ResolveProfile(profileType);
            if (profile == null)
                return; // that profile simply isn't configured; matches singleplayer's silent skip

            CinemachineImpulseSource source = CreateTemporarySource(ResolveReferenceController(), worldPosition);
            if (source == null)
                return;

            cameraBehaviours.ShakeCameraFromProfile(profile, source);

            Object.Destroy(source.gameObject, TEMP_SOURCE_LIFETIME);
        }

        /// <summary>Any live ScreenShakeSourceController, borrowed for its profiles and impulse shaping.</summary>
        private static ScreenShakeSourceController ResolveReferenceController()
        {
            if (_referenceController != null)
                return _referenceController;

            _referenceController = Object.FindFirstObjectByType<ScreenShakeSourceController>(FindObjectsInactive.Include);

            if (_referenceController == null && !_loggedMissingSetup)
            {
                _loggedMissingSetup = true;
                Debug.LogWarning("[NetworkedScreenShake] No ScreenShakeSourceController in the scene; " +
                                 "networked explosion shake is disabled this match.");
            }

            return _referenceController;
        }

        /// <summary>The profile for a shake type from any emitter in the map (each carries only its own); cached per type.</summary>
        private static ScreenShakeProfile ResolveProfile(ScreenShakeProfileType profileType)
        {
            if (ProfileCache.TryGetValue(profileType, out ScreenShakeProfile cached) && cached != null)
                return cached;

            var controllers = Object.FindObjectsByType<ScreenShakeSourceController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (ScreenShakeSourceController controller in controllers)
            {
                if (ProfilesField?.GetValue(controller) is not ScreenShakeProfile[] profiles)
                    continue;

                foreach (ScreenShakeProfile profile in profiles)
                {
                    if (profile == null || profile.profileType != profileType)
                        continue;

                    ProfileCache[profileType] = profile;
                    return profile;
                }
            }

            return null;
        }

        /// <summary>A throwaway impulse source at the blast, with the reference source's ImpulseDefinition.</summary>
        private static CinemachineImpulseSource CreateTemporarySource(ScreenShakeSourceController reference, Vector3 worldPosition)
        {
            var go = new GameObject("[NetworkedScreenShake]");
            go.transform.position = worldPosition;

            var source = go.AddComponent<CinemachineImpulseSource>();

            if (reference == null)
                return source; // no emitter to copy shaping from; Cinemachine defaults still work

            if (SourceField?.GetValue(reference) is CinemachineImpulseSource referenceSource && referenceSource != null)
            {
                if (referenceSource.ImpulseDefinition != null)
                    source.ImpulseDefinition = referenceSource.ImpulseDefinition;
                source.DefaultVelocity = referenceSource.DefaultVelocity;
            }

            return source;
        }
    }
}
