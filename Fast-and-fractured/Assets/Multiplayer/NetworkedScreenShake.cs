using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Network adapter for the singleplayer screen-shake system.
    ///
    /// The singleplayer design (ScreenShakeSourceController + CameraBehaviours + Cinemachine
    /// Impulse) is already multiplayer-friendly: an impulse is emitted at a WORLD POSITION and the
    /// camera's CinemachineImpulseListener attenuates it by distance, so every peer that is close
    /// enough naturally feels a nearby blast. What it cannot do on its own is cross the network —
    /// <c>GenerateImpulse</c> only fires on the machine that calls it.
    ///
    /// That leaves two gaps in multiplayer:
    ///   * Weapon fire shakes only the shooter (correct — each owner runs its own shoot handle).
    ///   * EXPLOSIONS shake nobody. NetworkedProjectile deletes the singleplayer ExplosionForce
    ///     components (it does damage/knockback itself via OverlapSphere), and the TNT wrapper only
    ///     activates the hitbox on the server, so the profile call that would shake the screen
    ///     never runs on any client.
    ///
    /// This helper closes that gap without touching singleplayer code: the multiplayer wrappers
    /// already broadcast the explosion to every observer (RpcSpawnExplosionVFX / RpcExplosionTriggered),
    /// so they call in here and each peer emits the impulse locally at the blast position. Cinemachine
    /// then does the distance falloff exactly as it does in singleplayer.
    /// </summary>
    public static class NetworkedScreenShake
    {
        private const float TEMP_SOURCE_LIFETIME = 3f;

        private static readonly FieldInfo ProfilesField = typeof(ScreenShakeSourceController)
            .GetField("screenShakeProfiles", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SourceField = typeof(ScreenShakeSourceController)
            .GetField("cinemachineImpulseSource", BindingFlags.NonPublic | BindingFlags.Instance);

        // Profiles are authored per-emitter (the ExplosionHitbox objects carry the Explosion profile,
        // the PushShoot object carries PushShoot, …) and the assets do NOT live under Resources, so
        // the only runtime way to obtain one is to scan the emitters present in the loaded map.
        private static readonly System.Collections.Generic.Dictionary<ScreenShakeProfileType, ScreenShakeProfile> ProfileCache = new();

        private static ScreenShakeSourceController _referenceController;
        private static bool _loggedMissingSetup;

        // Domain reload is disabled in this project's Enter Play Mode options, so statics survive
        // between play sessions and would keep pointing at destroyed objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _referenceController = null;
            _loggedMissingSetup = false;
            ProfileCache.Clear();
        }

        /// <summary>
        /// Emits a screen-shake impulse at a world position on THIS peer. Safe to call from an
        /// ObserversRpc: every client runs it locally and Cinemachine attenuates by distance, so
        /// players far from the blast feel little or nothing.
        /// </summary>
        public static void PlayAtPosition(ScreenShakeProfileType profileType, Vector3 worldPosition)
        {
            // Never use CameraBehaviours.Instance: the singleton accessor creates a phantom
            // instance when none exists, which then disables the real camera controller.
            var cameraBehaviours = CameraBehaviours.ExistingInstance;
            if (cameraBehaviours == null)
                return; // no local camera yet (still loading, or a dedicated server) — nothing to shake

            ScreenShakeProfile profile = ResolveProfile(profileType);
            if (profile == null)
                return; // that profile simply isn't configured; matches singleplayer's silent skip

            CinemachineImpulseSource source = CreateTemporarySource(ResolveReferenceController(), worldPosition);
            if (source == null)
                return;

            // Reuse the singleplayer path so listener settings and impulse shaping stay identical;
            // the only difference is that our source sits at the blast position instead of on a car.
            cameraBehaviours.ShakeCameraFromProfile(profile, source);

            Object.Destroy(source.gameObject, TEMP_SOURCE_LIFETIME);
        }

        /// <summary>
        /// Borrows any live ScreenShakeSourceController (the local car always has one) purely to
        /// read the configured profiles and the authored impulse shaping. Re-resolved whenever the
        /// cached one dies, e.g. after a match ends and the cars are destroyed.
        /// </summary>
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

        /// <summary>
        /// Finds the authored profile for a shake type by scanning every emitter in the loaded map.
        /// Each ScreenShakeSourceController only carries the profile(s) it needs — the Explosion
        /// profile lives on the ExplosionHitbox objects, not on the car — so looking at just one
        /// controller finds nothing. Cached per type after the first lookup.
        /// </summary>
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

        /// <summary>
        /// Builds a throwaway impulse source at the blast position, inheriting the authored
        /// ImpulseDefinition (dissipation distance, propagation speed, shape…) from the reference
        /// source so a networked explosion travels through the world exactly like a local one.
        /// </summary>
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
