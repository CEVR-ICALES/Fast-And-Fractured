namespace FastAndFractured.Multiplayer
{
    /// <summary>Counters of the server's verdicts on predicted shots and ultimates (a rejection is a shot that vanished).</summary>
    public static class ShootingNetworkStats
    {
        public static int ProjectilesAccepted;
        public static int ProjectilesRejected;
        public static int AbilityObjectsAccepted;
        public static int AbilityObjectsRejected;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            ProjectilesAccepted = 0;
            ProjectilesRejected = 0;
            AbilityObjectsAccepted = 0;
            AbilityObjectsRejected = 0;
        }

        public static string Describe() =>
            $"projectiles accepted={ProjectilesAccepted} rejected={ProjectilesRejected}; " +
            $"ultimate objects accepted={AbilityObjectsAccepted} rejected={AbilityObjectsRejected}";
    }
}
