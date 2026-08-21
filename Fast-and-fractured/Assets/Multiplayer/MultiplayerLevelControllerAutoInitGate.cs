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
        private static bool _fishnetHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            if (_unityHookRegistered) return;
            _unityHookRegistered = true;
            // We hook Unity sceneLoaded only to attach the FishNet hook once InstanceFinder.SceneManager
            // is up; the actual spawn runs from FishNet OnLoadEnd (post-FishNet-sync timing).
            UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        private static void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_fishnetHookRegistered) return;
            var sm = InstanceFinder.SceneManager;
            if (sm == null) return;
            sm.OnLoadEnd += OnFishNetLoadEnd;
            _fishnetHookRegistered = true;
        }

        private static void OnFishNetLoadEnd(SceneLoadEndEventArgs args)
        {
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
            if (lc == null) return;

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
