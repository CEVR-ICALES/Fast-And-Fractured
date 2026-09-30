using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public readonly struct LobbyHandle : IEquatable<LobbyHandle>
    {
        public readonly string PlatformId;
        public readonly string Address;
        public readonly ushort Port;

        public LobbyHandle(string platformId, string address, ushort port)
        {
            PlatformId = platformId;
            Address = address;
            Port = port;
        }

        public static LobbyHandle FromAddress(string ip, ushort port) =>
            new LobbyHandle($"{ip}:{port}", ip, port);

        public bool Equals(LobbyHandle other) => PlatformId == other.PlatformId;
        public override bool Equals(object obj) => obj is LobbyHandle h && Equals(h);
        public override int GetHashCode() => PlatformId?.GetHashCode() ?? 0;
        public static bool operator ==(LobbyHandle a, LobbyHandle b) => a.Equals(b);
        public static bool operator !=(LobbyHandle a, LobbyHandle b) => !a.Equals(b);
        public override string ToString() => PlatformId ?? string.Empty;
    }

    public sealed class LobbyCreateOptions
    {
        public string RoomName = "Room";
        public ushort Port = 7777;
        public int MaxPlayers = 8;
        public int InitialAiSlots = 0;
        public bool IsPublic = true;
        public bool HostAsServer = false;
        public IReadOnlyDictionary<string, string> Metadata = new Dictionary<string, string>();
    }

    public sealed class LobbyFilter
    {
        public string GameVersion;
        public IReadOnlyDictionary<string, string> RequiredMetadata = new Dictionary<string, string>();

        public static LobbyFilter Default => new LobbyFilter { GameVersion = Application.version };
    }

    public sealed class LobbyListing
    {
        public LobbyHandle Handle;
        public string RoomName;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int PingMs;
        public IReadOnlyDictionary<string, string> Metadata = new Dictionary<string, string>();
    }

    public interface ILobbyTransport
    {
        Task<LobbyHandle> CreateLobbyAsync(LobbyCreateOptions options, CancellationToken ct = default);
        Task<bool> JoinLobbyAsync(LobbyHandle handle, CancellationToken ct = default);
        Task LeaveLobbyAsync(CancellationToken ct = default);
        Task<IReadOnlyList<LobbyListing>> ListLobbiesAsync(LobbyFilter filter, CancellationToken ct = default);

        void SetLobbyMetadata(string key, string value);
        string GetLobbyMetadata(string key);

        event Action<LobbyHandle> OnLobbyMemberJoined;
        event Action<LobbyHandle> OnLobbyMemberLeft;
        event Action OnLobbyClosed;
    }
}
