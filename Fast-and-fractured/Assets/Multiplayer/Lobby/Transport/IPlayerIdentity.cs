using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Abstractions
{
    public interface IPlayerIdentity
    {
        string DisplayName { get; }
        string PlatformId { get; }
        Task<Texture2D> GetAvatarAsync(CancellationToken ct = default);
    }

    public interface IPlayerIdentityProvider
    {
        Task<IPlayerIdentity> GetLocalIdentityAsync(CancellationToken ct = default);
        Task<IPlayerIdentity> GetIdentityAsync(string platformId, CancellationToken ct = default);
    }
}
