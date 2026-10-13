using System.Collections;
using Enums;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Single exit gate for multiplayer map -> MainMenu transitions. It closes the scene curtain,
    /// prepares networked abilities, gives Unity one frame to execute their deferred
    /// Destroy/OnDestroy callbacks, and only then unloads the map and its scene services.
    ///
    /// The connection is NOT touched here: after a match everyone stays in the room
    /// (<see cref="MultiplayerRoomReturn"/>). On the host, the remote players are taken out of
    /// the map scene first, so the server stops sending them its objects.
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

        /// <summary>
        /// Loads the main menu behind the curtain. <paramref name="openScreen"/>: menu screen to
        /// show instead of the menu's own start (the win/lose screen opens by itself through
        /// GameEndData).
        /// </summary>
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

        /// <summary>
        /// Host: remote players leave the map scene before it is unloaded. Unity unloading the
        /// scene under FishNet clears the scene's observers but leaves the stale scene in each
        /// connection's scene list, which would make the next match think they had already
        /// loaded its map.
        /// </summary>
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
                // Object.Destroy executes at the end of the current frame. Waiting one frame
                // guarantees MariaAntoniaUniqueAbility.OnDestroy sees the map SoundManager,
                // instead of running amid unordered scene destruction.
                yield return null;

                DetachClientsFromMapScenes();
                RelaxClientTimeout();
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX);

                // Keep the duplicate-load guard armed through the actual scene transition.
                yield return null;
                _mainMenuLoadPending = false;

                // The menu has loaded; give its first heavy frames time before the short
                // host-migration timeout applies again.
                yield return new WaitForSecondsRealtime(TIMEOUT_GRACE_SECONDS);
                RestoreClientTimeout();
                Destroy(gameObject);
            }

            private const float TIMEOUT_GRACE_SECONDS = 5f;

            /// <summary>
            /// Loading the menu blocks the main thread for seconds (12 s in the editor), while the
            /// transport keeps counting on its own thread. With host migration on, clients time out
            /// after 5 s (HostMigrationNetwork.CLIENT_TIMEOUT_SECONDS) - the player who stays in
            /// the room after the match lost its connection on the way there.
            /// </summary>
            private static void RelaxClientTimeout()
            {
                var transport = InstanceFinder.TransportManager != null ? InstanceFinder.TransportManager.Transport : null;
                if (transport != null && InstanceFinder.IsClientStarted)
                    transport.SetTimeout(transport.GetTimeout(asServer: true), asServer: false);
            }

            private static void RestoreClientTimeout()
            {
                var transport = InstanceFinder.TransportManager != null ? InstanceFinder.TransportManager.Transport : null;
                if (transport != null && InstanceFinder.IsClientStarted && !InstanceFinder.IsServerStarted &&
                    HostMigration.HostMigrationSettings.Enabled)
                    transport.SetTimeout(HostMigration.HostMigrationNetwork.CLIENT_TIMEOUT_SECONDS, asServer: false);
            }
        }
    }
}
