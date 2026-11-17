using FishNet.Broadcast;
using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>Client to server on connect: the migration token, which identifies a player across hosts (client ids change).</summary>
    public struct MigrationTokenBroadcast : IBroadcast
    {
        public string Token;
        /// <summary>Settings ▸ Multiplayer: this player accepts taking over the room.</summary>
        public bool WillingToHost;
    }

    /// <summary>
    /// Server to clients during a match: what a new host needs to rebuild it. The cast is replayed from the
    /// original start (same seed and players) and each survivor gets its saved state back.
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

        // The map: broken props, platform stages (0 intact, 1 cracked, 2 broken) and the pickups in pool order.
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
