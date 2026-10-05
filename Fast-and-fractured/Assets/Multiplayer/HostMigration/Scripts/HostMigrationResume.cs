namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// The match a new host has to rebuild. Set by HostMigrationNetwork before it reloads the map,
    /// consumed by NetworkedLevelControllerManager when the map is up.
    /// </summary>
    public static class HostMigrationResume
    {
        public static MatchSnapshotBroadcast? Pending { get; set; }
    }
}
