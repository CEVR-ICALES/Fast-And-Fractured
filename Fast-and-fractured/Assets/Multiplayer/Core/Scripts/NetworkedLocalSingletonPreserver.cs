using FishNet.Object;
using UnityEngine;
using Utilities;
using FastAndFractured;

/// <summary>
/// Keeps the singletons on a network-spawned player prefab dormant until ownership is known: the server
/// constructs them normally, the owner claims them in OnStartClient and remote copies stay disabled.
/// Scene singletons are MultiplayerSingletonGuard's job.
/// </summary>
[DefaultExecutionOrder(-500)]
public class NetworkedLocalSingletonPreserver : NetworkBehaviour
{
    [SerializeField] AbstractAutoInitializableMonoBehaviour singletonToPreserve;
    [SerializeField] bool forceConstruct;
    [SerializeField] bool forceInitialize;
    
    public AbstractAutoInitializableMonoBehaviour SingletonToPreserve => singletonToPreserve;
    
    private bool _isSceneObject;
    private bool _suppressedPlayerHUD;
    private static readonly System.Reflection.BindingFlags FLAGS = 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    private static readonly System.Reflection.BindingFlags STATIC_FLAGS = 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

    private void Awake()
    {
        if (singletonToPreserve == null) return;
        
        var nob = GetComponentInParent<NetworkObject>();
        _isSceneObject = (nob != null && nob.IsSceneObject);
        if (_isSceneObject) return;
        
        // Runs before the singleton's Awake (-222): with auto-construct off it never claims s_Instance.
        SuppressAutoInit(singletonToPreserve);
        
        SetDuplicateResolution(singletonToPreserve, SingletonDuplicateResolution.DISABLE_COMPONENT);
        
        // PlayerHUD is not a singleton, but auto-initialized it binds this car's cooldowns to the local HUDManager.
        if (!_suppressedPlayerHUD)
        {
            var playerHUD = nob.GetComponentInChildren<PlayerHUD>(true);
            if (playerHUD != null)
            {
                SuppressAutoInit(playerHUD);
                _suppressedPlayerHUD = true;
            }
        }
        
        // The server needs the singletons (CarInjector.Install); IsServerInitialized is not set yet in Awake.
        var nm = FishNet.Managing.NetworkManager.Instances.Count > 0 
            ? FishNet.Managing.NetworkManager.Instances[0] : null;
        if (nm != null && nm.IsServerStarted)
        {
            RestoreAutoInit(singletonToPreserve);
            var playerHUD = nob.GetComponentInChildren<PlayerHUD>(true);
            if (playerHUD != null)
            {
                RestoreAutoInit(playerHUD);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (singletonToPreserve == null || _isSceneObject) return;

        if (!base.IsOwner)
        {
            ForceClearSingleton(singletonToPreserve);
            
            ClearConstructionFlags(singletonToPreserve);
            singletonToPreserve.enabled = false;

            // Remove remote PlayerHUDs so their onLevelPreStart callbacks cannot reach the local HUD.
            if (singletonToPreserve is HUDManager)
            {
                DisableAndDestroyRemotePlayerHUDs();
            }
            
            var canvas = singletonToPreserve.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvas.enabled = false;
            }
            return;
        }

        bool isPureClient = !base.IsServerInitialized;
        
        if (isPureClient)
        {
            SuppressAutoInit(singletonToPreserve);
        }
        
        singletonToPreserve.gameObject.SetActive(true);
        singletonToPreserve.enabled = true;
        
        var ownerCanvas = singletonToPreserve.GetComponentInParent<Canvas>(true);
        if (ownerCanvas != null)
        {
            ownerCanvas.enabled = true;
        }
        
        if (isPureClient)
        {
            // Pure client: set s_Instance directly; ClaimSingletonOwnership would construct from stale state.
            ClearConstructionFlags(singletonToPreserve);
            SetSingletonInstance(singletonToPreserve);
        }
        else
        {
            // Host: the server already constructed it.
            IOverwritableSingleton overwritable = singletonToPreserve.GetComponent<IOverwritableSingleton>();
            if (overwritable != null)
            {
                overwritable.ClaimSingletonOwnership();
            }
        }
    }

    private void DisableAndDestroyRemotePlayerHUDs()
    {
        var nob = GetComponentInParent<NetworkObject>(true);
        if (nob == null || nob.IsOwner)
            return;

        var playerHuds = nob.GetComponentsInChildren<PlayerHUD>(true);
        foreach (var playerHud in playerHuds)
        {
            if (playerHud == null)
                continue;

            playerHud.enabled = false;
            Destroy(playerHud);
        }
    }
    
    #region Reflection Helpers
    
    private void SuppressAutoInit(AbstractAutoInitializableMonoBehaviour target)
    {
        var t = typeof(AbstractAutoInitializableMonoBehaviour);
        t.GetField("_autoConstructOnAwake", FLAGS)?.SetValue(target, false);
        t.GetField("_autoInitializeOnStart", FLAGS)?.SetValue(target, false);
        t.GetField("_autoInitializeOnEnable", FLAGS)?.SetValue(target, false);
    }
    
    private void RestoreAutoInit(AbstractAutoInitializableMonoBehaviour target)
    {
        var t = typeof(AbstractAutoInitializableMonoBehaviour);
        t.GetField("_autoConstructOnAwake", FLAGS)?.SetValue(target, true);
        t.GetField("_autoInitializeOnStart", FLAGS)?.SetValue(target, true);
        t.GetField("_autoInitializeOnEnable", FLAGS)?.SetValue(target, true);
    }
    
    private void ClearConstructionFlags(AbstractAutoInitializableMonoBehaviour target)
    {
        var t = typeof(AbstractAutoInitializableMonoBehaviour);
        t.GetField("_isConstructed", FLAGS)?.SetValue(target, false);
        t.GetField("_isInitialized", FLAGS)?.SetValue(target, false);
    }
    
    private void SetDuplicateResolution(Component target, SingletonDuplicateResolution resolution)
    {
        var baseType = target.GetType();
        while (baseType != null && baseType != typeof(MonoBehaviour))
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(AbstractSingleton<>))
            {
                baseType.GetField("singletonDuplicateResolution", FLAGS)?.SetValue(target, resolution);
                break;
            }
            baseType = baseType.BaseType;
        }
    }
    
    /// <summary>The AbstractSingleton&lt;T&gt; base type of a component and its s_Instance field.</summary>
    private System.Reflection.FieldInfo GetInstanceField(Component target)
    {
        var baseType = target.GetType();
        while (baseType != null && baseType != typeof(MonoBehaviour))
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(AbstractSingleton<>))
            {
                return baseType.GetField("s_Instance", STATIC_FLAGS);
            }
            baseType = baseType.BaseType;
        }
        return null;
    }
    
    private void SetSingletonInstance(Component target)
    {
        var field = GetInstanceField(target);
        if (field != null)
        {
            field.SetValue(null, target);
        }
    }
    
    /// <summary>Clears s_Instance when it points at the given target.</summary>
    private void ForceClearSingleton(Component target)
    {
        var field = GetInstanceField(target);
        if (field != null)
        {
            var current = field.GetValue(null);
            // Also when it is null or on another of our GameObjects (the host's singleton can claim it during Awake).
            if (current != null && ReferenceEquals(current, target))
            {
                field.SetValue(null, null);
            }
        }
    }
    
    #endregion
}
