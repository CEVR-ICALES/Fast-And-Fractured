namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Holds every car still until the countdown says "Start": the server publishes the countdown's start tick and
    /// the state machines only tick from the Start tick on, compared against each peer's server tick.
    /// </summary>
    public static class MatchStartGate
    {
        private static uint _controlsTick;

        public static void LockUntil(uint controlsTick) => _controlsTick = controlsTick;

        public static void Clear() => _controlsTick = 0;

        public static bool IsLocked(uint tick) => _controlsTick != 0 && tick < _controlsTick;
    }
}
