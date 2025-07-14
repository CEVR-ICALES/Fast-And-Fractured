using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Utilities;

public class NetworkedLocalSingletonPreserver : NetworkBehaviour
{
    [SerializeField] AbstractAutoInitializableMonoBehaviour singletonToPreserve;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!base.IsOwner) return;

        IOverwritableSingleton overwritableSingleton = singletonToPreserve.GetComponent<IOverwritableSingleton>();
        if (overwritableSingleton != null)
        {
            overwritableSingleton.ClaimSingletonOwnership();
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
