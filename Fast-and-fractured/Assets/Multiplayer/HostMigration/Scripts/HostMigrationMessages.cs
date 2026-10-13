using FishNet.Broadcast;
using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Client → server, on every connect: this process's migration token. FishNet client ids
    /// change when players reconnect to a new host, and two test editors share one Steam
    /// account, so neither identifies a player across a migration; the token does.
    /// </summary>
    public struct MigrationTokenBroadcast : IBroadcast
    {
        public string Token;
    }

    /// <summary>
    /// Server → clients, several times per second during a match: everything a new host needs
    /// to rebuild the match. The cast is rebuilt by replaying the original start (same seed,
    /// same human characters in the same order - the singleplayer spawner is deterministic),
    /// then each surviving character gets its saved state back.
    /// </summary>
    public struct MatchSnapshotBroadcast : IBroadcast
    {
        public bool MigrationEnabled;
        public string SceneName;

        // Replay of the original start.
        public int Seed;
        public int TotalCharacters;
        public string[] HumanTokens;   // original human order
        public string[] HumanCodes;

        // Who takes over, in order, and where to reach them.
        public string[] SuccessorTokens;
        public string[] SuccessorAddresses;
        public ushort Port;

        public SnapshotCharacter[] Characters;

        public bool StormStarted;
        public float SecondsUntilStorm;
        public float StormGrowth;
        public Vector3 StormSpawnPoint;
        public Vector3 StormMirrorPoint;

        // The map: props broken so far (NetworkedWorldBreakables indices), how far each
        // breakable platform got (NetworkedBreakablePlatform: 0 intact, 1 cracked, 2 broken, in
        // hierarchy order) and the pickups on the map in their pool order (raw indices into
        // InteractableHandler.interactablesToToggle; the first ones are the visible ones).
        public int[] BrokenWorldProps;
        public byte[] PlatformStages;
        public int[] InteractablePool;

        // Lighting (VolumeManager volume) the match was using; -1 if unknown.
        public int VolumeIndex;

        // Golf ball positions, in NetworkedGolfBall.AllInHierarchyOrder order.
        public Vector3[] GolfBallPositions;
    }

    public struct SnapshotCharacter
    {
        public int OriginalIndex;   // index in the replayed spawn list
        public bool Alive;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public float Endurance;
    }
}
