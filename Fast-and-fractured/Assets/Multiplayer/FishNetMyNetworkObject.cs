// Ensamblado: Game.Network.FishNetImplementation
using FishNet.Object;
using UnityEngine;

public class FishNetMyNetworkObject : MonoBehaviour, IMyNetworkObject
{
    private NetworkObject _fnNetworkObject;

    void Awake()
    {
        _fnNetworkObject = GetComponent<NetworkObject>();
        if (_fnNetworkObject == null)
        {
            Debug.LogError($"FishNetMyNetworkObject en {gameObject.name} requiere un NetworkObject de FishNet.", this);
            enabled = false; // Deshabilitar si no hay NetworkObject de FishNet
        }
    }

    public int NetworkObjectId => _fnNetworkObject != null ? _fnNetworkObject.ObjectId : -1;
    public int OwnerId => _fnNetworkObject != null ? _fnNetworkObject.OwnerId : -1;
    public bool IsOwner => _fnNetworkObject != null && _fnNetworkObject.IsOwner;

    public void Despawn(bool asServer = true)
    {
        if (_fnNetworkObject == null) return;

        if (asServer)
        {
            if (FishNet.InstanceFinder.ServerManager.Started) // Sólo el servidor puede despawnear
                FishNet.InstanceFinder.ServerManager.Despawn(_fnNetworkObject);
        }
        else // Un cliente solicita despawn (FishNet no lo permite directamente, el servidor debe hacerlo)
        {
            if (IsOwner && FishNet.InstanceFinder.ClientManager.Started)
            {
                 // Podrías necesitar un RPC para solicitar al servidor que despawnee este objeto.
                 // Por ahora, sólo el servidor puede despawnear directamente con la lógica anterior.
                Debug.LogWarning("El cliente no puede despawnear objetos directamente en FishNet. El servidor debe manejar esto.");
            }
        }
    }
    // Nota: El Spawn es manejado por IMyNetworkManager.Spawn(prefab) y devuelve IMyNetworkObject
}