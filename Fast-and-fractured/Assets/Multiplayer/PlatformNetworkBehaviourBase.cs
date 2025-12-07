// Ensamblado: Game.Network.FishNetImplementation (o donde tenga sentido para tu juego)
using FishNet.Object;
using FishNet.Connection; // Para NetworkConnection
using UnityEngine;

// Esta clase es tu base para scripts que usan FishNet y necesitan
// una capa común de lógica o acceso simplificado.
// HEREDA DIRECTAMENTE DE NetworkBehaviour DE FISHNET.
public abstract class PlatformNetworkBehaviourBase : NetworkBehaviour
{
    // --- Acceso a propiedades comunes de FishNet de forma más directa ---
    // (No es estrictamente necesario, ya que NetworkBehaviour las tiene, pero es por consistencia si vienes de la idea de MyNetworkBehaviour)
    public bool IsServerStarted => base.IsServer; // Renombrado para claridad respecto a InstanceFinder.ServerManager.Started
    public bool IsClientStarted => base.IsClient; // El 'IsClient' de FishNet NetworkBehaviour indica si este objeto es cliente o no
    public bool IsHostStarted => base.IsHost;   // Indica si es Server y Client


    // --- Eventos de Ciclo de Vida Personalizados (opcional, para extender) ---
    // Estos los puedes llamar desde los OnStart/OnStop de FishNet en las clases derivadas si quieres una capa extra.
    public virtual void OnPlatformNetworkSpawn() { /* Código común de spawn */ }
    public virtual void OnPlatformNetworkDespawn() { /* Código común de despawn */ }
    public virtual void OnPlatformOwnershipClientChanged(NetworkConnection prevOwner) { /* Código común cambio de propietario */ }


    // --- Los Callbacks de FishNet ---
    // Las clases derivadas pueden sobreescribir estos Y LLAMAR a los de la base
    // Y luego a tus métodos de ciclo de vida personalizados.
    public override void OnStartServer()
    {
        base.OnStartServer(); // Siempre llamar a la base
        // Lógica específica de PlatformNetworkBehaviourBase al iniciar en servidor
        OnPlatformNetworkSpawn(); // Llama a nuestro método "abstraído"
    }

    public override void OnStartClient()
    {
        base.OnStartClient(); // Siempre llamar a la base
        // Lógica específica de PlatformNetworkBehaviourBase al iniciar en cliente
        OnPlatformNetworkSpawn(); // Llama a nuestro método "abstraído"
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        // Lógica específica de PlatformNetworkBehaviourBase al detener en servidor
        OnPlatformNetworkDespawn();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        // Lógica específica de PlatformNetworkBehaviourBase al detener en cliente
        OnPlatformNetworkDespawn();
    }

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    {
        base.OnOwnershipClient(prevOwner);
        // Lógica específica de PlatformNetworkBehaviourBase al cambiar propietario
        OnPlatformOwnershipClientChanged(prevOwner);
    }
}
