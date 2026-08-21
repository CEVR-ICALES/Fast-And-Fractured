using FishNet.Object;
using UnityEngine;
using Utilities;
using FastAndFractured;

/// <summary>
/// Preserves singletons on network-spawned player prefabs.
/// 
/// Design:
/// AWAKE (-500): Runs BEFORE singleton Awake (-222). Prevents ALL player-spawned singletons
/// from auto-constructing (suppresses _autoConstructOnAwake). Also changes duplicate resolution
/// to DISABLE_COMPONENT so duplicates aren't destroyed. On the SERVER, singletons are re-enabled
/// immediately since the server needs them. On CLIENTS, singletons stay dormant until OnStartClient.
/// 
/// OnStartClient:
/// - Non-owner (remote player): Ensure singleton is disabled, clear s_Instance if it points here.
/// - Owner (local player): Set s_Instance to our component via direct reflection (skip ClaimSingletonOwnership 
///   entirely to avoid any auto-construct). Actual construct/init happens in ConstructPlayerSingletonsInOrder().
/// 
/// Scene-level singletons are managed by MultiplayerSingletonGuard, not this component.
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
        
        // --- KEY: Prevent this singleton from auto-constructing during its own Awake ---
        // This preserver runs at -500, singletons at -222. By suppressing auto-init NOW,
        // when the singleton's Awake fires it will NOT call Construct() ->InitializeSingleton()
        // ->won't set s_Instance ->won't trigger HandleDuplicate. All singletons stay dormant.
        SuppressAutoInit(singletonToPreserve);
        
        // Also change duplicate resolution as a safety net
        SetDuplicateResolution(singletonToPreserve, SingletonDuplicateResolution.DISABLE_COMPONENT);
        
        // Also suppress PlayerHUD auto-init on this player. PlayerHUD is NOT a singleton,
        // NOT managed by any preserver, but its Initialize() adds an onLevelPreStart listener
        // that binds cooldown events to HUDManager. If the HOST's PlayerHUD auto-initializes,
        // it subscribes the HOST's car cooldowns to our HUDManager (HUD shows host's data).
        // We suppress ALL PlayerHUDs here and only restore the owner's in OnStartClient.
        if (!_suppressedPlayerHUD)
        {
            var playerHUD = nob.GetComponentInChildren<PlayerHUD>(true);
            if (playerHUD != null)
            {
                SuppressAutoInit(playerHUD);
                _suppressedPlayerHUD = true;
            }
        }
        
        // On the SERVER, we need singletons to work normally (CarInjector.Install needs them).
        // Re-enable auto-construct so the singleton constructs in its own Awake.
        // We can't check IsServerInitialized in Awake (FishNet hasn't set it yet), but we CAN
        // check if a NetworkManager exists and is already running as server.
        var nm = FishNet.Managing.NetworkManager.Instances.Count > 0 
            ? FishNet.Managing.NetworkManager.Instances[0] : null;
        if (nm != null && nm.IsServerStarted)
        {
            // Server: allow normal auto-init for both singleton and PlayerHUD
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
            // REMOTE PLAYER: disable everything
            // Forcefully clear s_Instance if it points to this remote component
            ForceClearSingleton(singletonToPreserve);
            
            // Reset state and disable
            ClearConstructionFlags(singletonToPreserve);
            singletonToPreserve.enabled = false;

            // HUDManager path: fully remove remote PlayerHUD components to prevent
            // remote onLevelPreStart callbacks from leaking into the local HUD.
            if (singletonToPreserve is HUDManager)
            {
                DisableAndDestroyRemotePlayerHUDs();
            }
            
            // Disable Canvas for remote player's UI
            var canvas = singletonToPreserve.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvas.enabled = false;
            }
            return;
        }

        // LOCAL PLAYER (owner): prepare singleton for later explicit construction
        bool isPureClient = !base.IsServerInitialized;
        
        if (isPureClient)
        {
            // Ensure auto-init stays suppressed (should already be from Awake, but be safe)
            SuppressAutoInit(singletonToPreserve);
        }
        
        // Activate the GameObject and component
        singletonToPreserve.gameObject.SetActive(true);
        singletonToPreserve.enabled = true;
        
        // Enable Canvas for local player's HUD
        var ownerCanvas = singletonToPreserve.GetComponentInParent<Canvas>(true);
        if (ownerCanvas != null)
        {
            ownerCanvas.enabled = true;
        }
        
        if (isPureClient)
        {
            // PURE CLIENT: Set s_Instance directly via reflection. Do NOT use ClaimSingletonOwnership
            // because it calls Construct()/Initialize() based on old state. We want to control
            // construction order explicitly in ConstructPlayerSingletonsInOrder().
            ClearConstructionFlags(singletonToPreserve);
            SetSingletonInstance(singletonToPreserve);
        }
        else
        {
            // HOST: Server already constructed this singleton. Just claim ownership normally.
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
    
    /// <summary>
    /// Suppress all auto-init flags so the singleton won't construct/initialize
    /// during its Awake/OnEnable/Start lifecycle.
    /// </summary>
    private void SuppressAutoInit(AbstractAutoInitializableMonoBehaviour target)
    {
        var t = typeof(AbstractAutoInitializableMonoBehaviour);
        t.GetField("_autoConstructOnAwake", FLAGS)?.SetValue(target, false);
        t.GetField("_autoInitializeOnStart", FLAGS)?.SetValue(target, false);
        t.GetField("_autoInitializeOnEnable", FLAGS)?.SetValue(target, false);
    }
    
    /// <summary>
    /// Restore auto-init flags to true (used on server where singletons must work normally).
    /// </summary>
    private void RestoreAutoInit(AbstractAutoInitializableMonoBehaviour target)
    {
        var t = typeof(AbstractAutoInitializableMonoBehaviour);
        t.GetField("_autoConstructOnAwake", FLAGS)?.SetValue(target, true);
        t.GetField("_autoInitializeOnStart", FLAGS)?.SetValue(target, true);
        t.GetField("_autoInitializeOnEnable", FLAGS)?.SetValue(target, true);
    }
    
    /// <summary>
    /// Clear _isConstructed and _isInitialized flags on a component.
    /// </summary>
    private void ClearConstructionFlags(AbstractAutoInitializableMonoBehaviour target)
    {
        var t = typeof(AbstractAutoInitializableMonoBehaviour);
        t.GetField("_isConstructed", FLAGS)?.SetValue(target, false);
        t.GetField("_isInitialized", FLAGS)?.SetValue(target, false);
    }
    
    /// <summary>
    /// Set duplicate resolution mode via reflection.
    /// </summary>
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
    
    /// <summary>
    /// Find the AbstractSingleton&lt;T&gt; base type and its s_Instance field for a given component.
    /// </summary>
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
    
    /// <summary>
    /// Set s_Instance to this component, unconditionally. No side effects.
    /// </summary>
    private void SetSingletonInstance(Component target)
    {
        var field = GetInstanceField(target);
        if (field != null)
        {
            field.SetValue(null, target);
        }
    }
    
    /// <summary>
    /// Clear s_Instance unconditionally (set to null). Used for remote player cleanup.
    /// Only clears if s_Instance points to the given target.
    /// </summary>
    private void ForceClearSingleton(Component target)
    {
        var field = GetInstanceField(target);
        if (field != null)
        {
            var current = field.GetValue(null);
            // Clear if it points to this remote component OR if it's null
            // Also clear if the current instance is on a different GameObject that we own
            // (edge case: host's singleton became s_Instance during Awake)
            if (current != null && ReferenceEquals(current, target))
            {
                field.SetValue(null, null);
            }
        }
    }
    
    #endregion
}
