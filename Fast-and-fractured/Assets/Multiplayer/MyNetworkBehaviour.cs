// Ensamblado: Game.Network.Abstractions
using UnityEngine;

public abstract class MyNetworkBehaviour : MonoBehaviour
{
    // --- Identidad de Red y Estado ---
    public abstract bool IsOwner { get; }
    public abstract bool IsServer { get; }
    public abstract bool IsClient { get; } // Si este es un cliente (puede ser host)
    public abstract bool IsHost { get; } // Si es servidor Y cliente localmente
    public abstract int OwnerId { get; }
    public abstract int NetworkObjectId { get; } // Un ID único del objeto en la red

    // --- Abstracción para RPCs (CONCEPTUAL - la implementación es lo complejo) ---
    // Esta es la parte más difícil de abstraer limpiamente entre bibliotecas.
    // Opción 1: Llamadas por nombre de método (más flexible, menos type-safe)
    public delegate void ServerRpcAction<T>(T payload);
    public delegate void ObserversRpcAction<T>(T payload);

    // Necesitaría un registro y despacho de métodos. Por ahora, dejaremos esto más conceptual.
    // O, la implementación concreta (FishNetMyNetworkBehaviour) podría exponer métodos
    // que toman un delegado y lo invocan bajo el capó con los atributos de FishNet.

    // --- Eventos de Ciclo de Vida de Red Abstraídos ---
    // Estos serían invocados por la implementación concreta (ej. FishNetMyNetworkBehaviour)
    public virtual void OnMyNetworkSpawn() { /* Lógica de spawn por defecto o para heredar */ }
    public virtual void OnMyNetworkDespawn() { /* Lógica de despawn por defecto o para heredar */ }
    public virtual void OnOwnershipClientChanged(int prevOwnerId, int newOwnerId) { /* Logica cuando cambia el propietario */ }

    // --- Referencia al IMyNetworkObject ---
    // Podrías requerir que un IMyNetworkObject esté en el mismo GameObject
    // y obtenerlo aquí, o que sea inyectado.
    protected IMyNetworkObject _myNetworkObject;
    protected virtual void Awake() {
        _myNetworkObject = GetComponent<IMyNetworkObject>();
        if (_myNetworkObject == null) {
            //Debug.LogWarning($"MyNetworkBehaviour en {gameObject.name} no encontró un IMyNetworkObject. Algunas funciones pueden no estar disponibles.");
        }
    }
}
