using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Utilities;

public class NetworkedLocalSingletonPreserver : NetworkBehaviour
{
    [SerializeField] AbstractAutoInitializableMonoBehaviour singletonToPreserve;
    [SerializeField] bool forceConstruct;
    [SerializeField] bool forceInitialize;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!base.IsOwner) return;

        singletonToPreserve.gameObject.SetActive(true);
        IOverwritableSingleton overwritableSingleton = singletonToPreserve.GetComponent<IOverwritableSingleton>();
       
        if (overwritableSingleton != null)
        {
            overwritableSingleton.ClaimSingletonOwnership();
            if (forceConstruct)
            {
                overwritableSingleton.ForceConstruct();
            }
            if (forceInitialize)
            {
                overwritableSingleton.ForceInitialize();
            }
        }
        if (forceConstruct)
        {
            singletonToPreserve.PerformConstruct();
        }
        if (forceInitialize)
        {
            singletonToPreserve.PerformInitialize();
        }
    }    
}
