using FishNet;
using FishNet.Managing.Scened;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Spawns the multiplayer manager when a networked map loads on the server (LevelController waits dormant for it).</summary>
    public static class MultiplayerLevelControllerAutoInitGate
    {
        public const string SPAWNER_PREFAB_RESOURCES_PATH = "MultiplayerLevelControllerSpawnable";

        private static bool _unityHookRegistered;

        // The SceneManager instance we are subscribed to: it is recreated every Play session, and with domain reload
        // off a plain bool skipped subscribing to the new one, so every match after the first never started.
        private static FishNet.Managing.Scened.SceneManager _subscribedSceneManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            if (_unityHookRegistered) return;
            _unityHookRegistered = true;
            // sceneLoaded survives Play sessions without a domain reload: subscribe once.
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
                // Logged so a missing or renamed networked map shows up here.
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
