// Ensamblado: Game.Network.Abstractions
public interface IMyNetworkObject
{
    int NetworkObjectId { get; }
    int OwnerId { get; }
    bool IsOwner { get; }
    void Despawn(bool asServer = true); // `asServer` indica si la petición es del servidor
}