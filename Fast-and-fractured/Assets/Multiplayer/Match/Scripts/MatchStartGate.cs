namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Holds every car (players and AI) still until the countdown says "Start".
    ///
    /// The cars are spawned after the singleplayer start timer has already run on the host, so the
    /// server gave control the moment each car's state machine bound - on the host the cars could
    /// drive and the AI shoot while the 3-2-1 was still on screen, and a remote client saw its own
    /// countdown at a different time (playtest feedback 2026-09-30). The server publishes the tick
    /// the countdown starts on (NetworkedLevelControllerManager); the state machines only tick from
    /// the "Start" tick on, evaluated on the replicated tick so reconcile replays agree.
    /// </summary>
    public static class MatchStartGate
    {
        private static uint _controlsTick;

        public static void LockUntil(uint controlsTick) => _controlsTick = controlsTick;

        public static void Clear() => _controlsTick = 0;

        public static bool IsLocked(uint tick) => _controlsTick != 0 && tick < _controlsTick;
    }
}
