using System;
using System.IO;
using Enums;
using FastAndFractured;
using FastAndFractured.Multiplayer.Abstractions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.Lobby
{
    /// <summary>
    /// Reuses the singleplayer character and map screens for the lobby: the map button's scene load is intercepted as
    /// a map vote and confirms the character (browsing does not: a match may start mid-selection). Singleplayer is unaffected.
    /// </summary>
    public static class MultiplayerMenuRouter
    {
        public static bool IsRoutingForLobby { get; private set; }

        // The map button activates this loading canvas before our override runs: it is hidden again.
        private const string LOADING_CANVAS_NAME = "LosingScreenCanvas";
        private const float SCREEN_TRANSITION_SECONDS = 0.5f;

        public static void BeginLobbyFlow()
        {
            if (IsRoutingForLobby) return;
            IsRoutingForLobby = true;

            LoadingManager.SceneLoadOverride = HandleSceneLoadAttempt;
            EnsureGuard();
        }

        public enum GuardAction { None, ReturnToRoom, EndFlow }

        // Back on the character screen led to the mode menu with the player still connected: a ghost seat in a room it could no longer find.
        public static GuardAction GuardFor(bool routing, bool connected, bool hasScreen, ScreensType screen)
        {
            if (!routing) return GuardAction.None;
            if (!connected) return GuardAction.EndFlow;
            if (!hasScreen) return GuardAction.None;
            bool leftTheRoom = screen == ScreensType.GAMEMODE_SELECTION || screen == ScreensType.MAIN_MENU ||
                               screen == ScreensType.MULTIPLAYER_HUB || screen == ScreensType.MULTIPLAYER_BROWSER ||
                               screen == ScreensType.MULTIPLAYER_DIRECT_JOIN || screen == ScreensType.MULTIPLAYER_HOST_SETUP;
            return leftTheRoom ? GuardAction.ReturnToRoom : GuardAction.None;
        }

        private static Guard _guard;

        private static void EnsureGuard()
        {
            if (_guard != null) return;
            var go = new GameObject("[MultiplayerMenuRouter]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _guard = go.AddComponent<Guard>();
        }

        private sealed class Guard : MonoBehaviour
        {
            private void Update()
            {
                if (!IsRoutingForLobby) return;
                MainMenuManager menu = MainMenuManager.ExistingInstance;
                MenuScreen screen = menu != null ? menu.CurrentScreen : null;
                bool connected = FishNet.InstanceFinder.IsClientStarted || FishNet.InstanceFinder.IsServerStarted;
                switch (GuardFor(true, connected, screen != null, screen != null ? screen.screenType : default))
                {
                    case GuardAction.ReturnToRoom:
                        EndLobbyFlow();
                        ReturnToLobbyRoom();
                        break;
                    case GuardAction.EndFlow:
                        EndLobbyFlow();
                        break;
                }
            }
        }

        public static void EndLobbyFlow()
        {
            if (!IsRoutingForLobby) return;
            IsRoutingForLobby = false;

            LoadingManager.SceneLoadOverride = null;
        }

        /// <summary>LoadingManager.SceneLoadOverride: turns a map load into a lobby vote; false when the scene is not a map.</summary>
        private static bool HandleSceneLoadAttempt(int sceneBuildIndex)
        {
            if (!IsRoutingForLobby) return false;

            int mapId = ResolveMapIdFromBuildIndex(sceneBuildIndex);
            if (mapId < 0)
            {
                Debug.LogWarning($"[MultiplayerMenuRouter] LoadScene({sceneBuildIndex}) intercepted but " +
                                 "scene is not in MapCatalog — letting the load through.");
                return false;
            }

            var lobby = LobbyLocator.GetLobbyManager();
            if (lobby != null)
            {
                lobby.ServerVoteMap(mapId);

                if (CharacterSelectorManager.HasInstance)
                {
                    string code = CharacterSelectorManager.Instance.CurrentCharacterCode;
                    if (!string.IsNullOrEmpty(code))
                    {
                        NetworkedLobbyManager.SavePreferredCharacterCode(code);
                        lobby.ServerSelectCharacter(code);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[MultiplayerMenuRouter] No NetworkedLobbyManager available — vote not submitted.");
            }

            HideLoadingCanvas();
            ReturnToLobbyRoom();
            EndLobbyFlow();
            return true;
        }

        private static int ResolveMapIdFromBuildIndex(int sceneBuildIndex)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
            if (string.IsNullOrEmpty(scenePath)) return -1;

            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var catalog = MapCatalogLoader.Get();
            if (catalog == null) return -1;

            foreach (var entry in catalog.Entries)
            {
                if (entry.SceneName == sceneName)           return entry.Id;
                if (!string.IsNullOrEmpty(entry.NetworkedSceneName) &&
                    entry.NetworkedSceneName == sceneName)  return entry.Id;
            }
            return -1;
        }

        private static void HideLoadingCanvas()
        {
            var byName = GameObject.Find(LOADING_CANVAS_NAME);
            if (byName != null) { byName.SetActive(false); return; }

            var active = SceneManager.GetActiveScene();
            foreach (var root in active.GetRootGameObjects())
            {
                if (TryHideRecursive(root.transform)) return;
            }
        }

        private static bool TryHideRecursive(Transform t)
        {
            if (t.gameObject.activeSelf && (
                    t.name.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("losing",  StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (t.GetComponent<LoadingManager>() != null)
                {
                    t.gameObject.SetActive(false);
                    return true;
                }
            }
            for (int i = 0; i < t.childCount; i++)
                if (TryHideRecursive(t.GetChild(i))) return true;
            return false;
        }

        private static void ReturnToLobbyRoom()
        {
            if (!MainMenuManager.HasInstance)
            {
                Debug.LogWarning("[MultiplayerMenuRouter] No MainMenuManager — cannot transition back to LOBBY_ROOM.");
                return;
            }
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, SCREEN_TRANSITION_SECONDS);
        }
    }
}
