using System.Collections;
using Enums;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Single exit from a multiplayer map to MainMenu: closes the curtain, prepares the abilities, waits a frame for
    /// their deferred destroys, then unloads. The connection stays (MultiplayerRoomReturn).
    /// </summary>
    internal static class MultiplayerSceneTeardown
    {
        private static bool _mainMenuLoadPending;
        private static ScreensType? _screenAfterLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _mainMenuLoadPending = false;
            _screenAfterLoad = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Loads the main menu behind the curtain, opening <paramref name="openScreen"/> if given.</summary>
        public static void LoadMainMenu(ScreensType? openScreen = null)
        {
            if (_mainMenuLoadPending) return;
            _mainMenuLoadPending = true;
            _screenAfterLoad = openScreen;

            SceneCurtain.Close(() =>
            {
                NetworkedUniqueAbility.PrepareAllForSceneTeardown();
                var runnerObject = new GameObject("[MultiplayerSceneTeardown]");
                Object.DontDestroyOnLoad(runnerObject);
                runnerObject.AddComponent<SceneTeardownRunner>().Begin();
            });
        }

        /// <summary>Host: remote players leave the map scene first, or FishNet keeps it in their scene list.</summary>
        private static void DetachClientsFromMapScenes()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || !nm.IsServerStarted) return;
            var clients = new FishNet.Connection.NetworkConnection[nm.ServerManager.Clients.Count];
            nm.ServerManager.Clients.Values.CopyTo(clients, 0);
            try { nm.SceneManager.RemoveConnectionsFromNonGlobalScenes(clients); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_screenAfterLoad is ScreensType screen)
                MultiplayerRoomReturn.OpenScreenWhenMenuReady(screen);
            _screenAfterLoad = null;
        }

        private sealed class SceneTeardownRunner : MonoBehaviour
        {
            public void Begin()
            {
                StartCoroutine(LoadMainMenuAfterAbilityDestroy());
            }

            private IEnumerator LoadMainMenuAfterAbilityDestroy()
            {
                // Destroy runs at the end of the frame: Maria's OnDestroy still finds the map's SoundManager.
                yield return null;

                DetachClientsFromMapScenes();
                RelaxClientTimeout();
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX);

                // Keep the duplicate-load guard armed through the actual scene transition.
                yield return null;
                _mainMenuLoadPending = false;

                // Let the menu's first heavy frames pass before the short migration timeout applies again.
                yield return new WaitForSecondsRealtime(TIMEOUT_GRACE_SECONDS);
                RestoreClientTimeout();
                Destroy(gameObject);
            }

            private const float TIMEOUT_GRACE_SECONDS = 5f;

            /// <summary>
            /// Loading the menu blocks the main thread for seconds while the transport keeps counting: with host migration
            /// on, the 5 s client timeout would drop the player on the way to the room.
            /// </summary>
            private static void RelaxClientTimeout()
            {
                if (InstanceFinder.IsClientStarted)
                    MultiplayerTransport.SetTimeout(MultiplayerTransport.GetTimeout(asServer: true), asServer: false);
            }

            private static void RestoreClientTimeout()
            {
                if (InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted && HostMigration.HostMigrationSettings.Enabled)
                    MultiplayerTransport.SetTimeout(HostMigration.HostMigrationNetwork.CLIENT_TIMEOUT_SECONDS, asServer: false);
            }
        }
    }
}
