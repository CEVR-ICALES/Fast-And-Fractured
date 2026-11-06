using System;
using UnityEngine;

namespace FastAndFractured
{
    /// <summary>
    /// Game-wide moments other systems can listen to (telemetry, achievements...) without the
    /// gameplay code knowing about them. Raise from the one place where the moment happens.
    /// </summary>
    public static class GameplayEvents
    {
        /// <summary>A match finished for the local player: true = won. Singleplayer and multiplayer.</summary>
        public static event Action<bool> MatchEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => MatchEnded = null;

        public static void RaiseMatchEnded(bool won)
        {
            try { MatchEnded?.Invoke(won); }
            catch (Exception e) { Debug.LogException(e); }   // a listener must never break the game's flow
        }
    }
}
