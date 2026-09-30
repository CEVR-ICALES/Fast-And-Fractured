using FishNet;
using FishNet.Managing.Scened;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Dynamically spawns the multiplayer manager when a networked map scene loads on the server.
    /// LevelController auto-init is disabled at editor time by NetworkedSceneMigrationTool, so
    /// LC sits dormant until the wrapper calls PerformConstruct/PerformInitialize via the manager.
    /// </summary>
    public static class MultiplayerLevelControllerAutoInitGate
    {
        public const string SPAWNER_PREFAB_RESOURCES_PATH = "MultiplayerLevelControllerSpawnable";

        private static bool _unityHookRegistered;

        // The FishNet SceneManager is destroyed and recreated every Play session (it lives on the
        // NetworkManager GameObject, which does not survive stopping Play). A plain "have we ever
        // subscribed" bool - the previous version of this field - stayed true forever once domain
        // reload is disabled, so the SECOND Play session in the same editor domain skipped
        // subscribing to the NEW SceneManager entirely: OnFishNetLoadEnd never fired again, no
        // NetworkedLevelControllerManager ever spawned, and nothing logged why - exactly the "first
        // match works, every match after it doesn't" symptom. Tracking the actual instance we are
        // subscribed to, instead of a bool, re-subscribes whenever that instance changes.
        private static FishNet.Managing.Scened.SceneManager _subscribedSceneManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            if (_unityHookRegistered) return;
            _unityHookRegistered = true;
            // Unity's sceneLoaded is a static delegate that keeps firing across Play sessions
            // without a domain reload, so this subscription itself only needs to happen once ever.
            // It hooks the FishNet SceneManager, checked fresh on every fire, below.
            UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        private static void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var sm = InstanceFinder.SceneManager;
            if (sm == null || sm == _subscribedSceneManager) return;
            sm.OnLoadEnd += OnFishNetLoadEnd;
            _subscribedSceneManager = sm;
        }

        private static void OnFishNetLoadEnd(SceneLoadEndEventArgs args)
        {
            // Cheap and permanent: the two guards below used to fail silently in both directions,
            // which is why a broken match start left zero trace of whether this even ran.
            Debug.Log($"[MultiplayerLevelControllerAutoInitGate] OnLoadEnd fired. " +
                      $"isServerStarted={InstanceFinder.IsServerStarted} asServer={args.QueueData.AsServer}");
            if (!InstanceFinder.IsServerStarted) return;
            if (!args.QueueData.AsServer) return;

            LevelController lc = null;
            if (args.LoadedScenes != null)
            {
                foreach (var s in args.LoadedScenes)
                {
                    lc = FindLevelControllerInScene(s);
                    if (lc != null) break;
                }
            }
            if (lc == null)
            {
                // This used to return silently, which is exactly why a failed match start looked
                // identical to "nothing happened yet": zero cars, zero log line, no signal at all
                // about whether this callback even ran. Log the scene names so a missing/renamed
                // networked map scene shows up here instead of as a spawn that never happens.
                string sceneNames = (args.LoadedScenes == null || args.LoadedScenes.Length == 0)
                    ? "(none)"
                    : string.Join(", ", System.Array.ConvertAll(args.LoadedScenes, s => s.name));
                Debug.LogWarning($"[MultiplayerLevelControllerAutoInitGate] OnLoadEnd fired as server " +
                                  $"but no LevelController was found in the loaded scene(s): {sceneNames}. " +
                                  "NetworkedLevelControllerManager will NOT spawn and no player cars will appear.");
                return;
            }

            var prefab = Resources.Load<GameObject>(SPAWNER_PREFAB_RESOURCES_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[MultiplayerLevelControllerAutoInitGate] " +
                               $"Resources/{SPAWNER_PREFAB_RESOURCES_PATH} missing. " +
                               "Run Tools > Fast And Fractured > Lobby Setup Wizard > SETUP EVERYTHING.");
                return;
            }

            SpawnManager(prefab);
        }

        private static LevelController FindLevelControllerInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var lc = root.GetComponentInChildren<LevelController>(true);
                if (lc != null) return lc;
            }
            return null;
        }

        private static void SpawnManager(GameObject prefab)
        {
            var existing = Object.FindAnyObjectByType<NetworkedLevelControllerManager>();
            if (existing != null && existing.IsSpawned) return;

            try
            {
                var instance = Object.Instantiate(prefab);
                InstanceFinder.ServerManager.Spawn(instance);
                Debug.Log("[MultiplayerLevelControllerAutoInitGate] Spawned NetworkedLevelControllerManager.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MultiplayerLevelControllerAutoInitGate] Manager spawn / game start threw: " +
                               $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
