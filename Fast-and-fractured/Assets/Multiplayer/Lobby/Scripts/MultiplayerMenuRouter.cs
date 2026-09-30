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
    /// Routes the existing singleplayer character + map selection flow into the multiplayer lobby
    /// without duplicating those screens.
    ///
    /// Flow when active (started by clicking SELECT CHARACTER &amp; MAP in LOBBY_ROOM):
    ///   1. Transition to CHARACTER_SELECTION screen — singleplayer UI runs unchanged.
    ///   2. While the flow is active, CharacterSelectorManager.OnCharacterCodeChanged is forwarded
    ///      to lobby.ServerSelectCharacter so other players see live updates as the user cycles.
    ///   3. Transition to MAP_SELECTION screen — same prefab buttons as singleplayer.
    ///   4. When a map button click reaches LoadingManager.LoadScene(idx), the registered
    ///      SceneLoadOverride intercepts: lookup mapId via MapCatalog, ServerVoteMap, transition
    ///      back to LOBBY_ROOM, end flow.
    ///
    /// Singleplayer is unaffected — when no flow is active the override returns false and
    /// LoadingManager loads the scene normally.
    /// </summary>
    public static class MultiplayerMenuRouter
    {
        public static bool IsRoutingForLobby { get; private set; }

        // Name of the canvas GameObject hardcoded into the map button OnClick (SetActive(true) is
        // executed before our override fires). We hunt it down by name after intercepting and
        // disable it so the user doesn't see a brief loading flash before the lobby fades back in.
        private const string LOADING_CANVAS_NAME = "LosingScreenCanvas";

        public static void BeginLobbyFlow()
        {
            if (IsRoutingForLobby) return;
            IsRoutingForLobby = true;

            LoadingManager.SceneLoadOverride = HandleSceneLoadAttempt;

            if (CharacterSelectorManager.HasInstance)
            {
                // Push the current character to the lobby immediately, then keep streaming updates.
                var inst = CharacterSelectorManager.Instance;
                string current = inst.CurrentCharacterCode;
                if (!string.IsNullOrEmpty(current)) ForwardCharacter(current);

                inst.OnCharacterCodeChanged += ForwardCharacter;
            }
        }

        public static void EndLobbyFlow()
        {
            if (!IsRoutingForLobby) return;
            IsRoutingForLobby = false;

            LoadingManager.SceneLoadOverride = null;

            if (CharacterSelectorManager.HasInstance)
                CharacterSelectorManager.Instance.OnCharacterCodeChanged -= ForwardCharacter;
        }

        private static void ForwardCharacter(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            // Persist to the MP-dedicated key so the lobby's "default on join" picks up this
            // choice next session instead of falling back to Josefino.
            NetworkedLobbyManager.SavePreferredCharacterCode(code);
            var lobby = LobbyLocator.GetLobbyManager();
            if (lobby != null) lobby.ServerSelectCharacter(code);
        }

        /// <summary>
        /// Hook for LoadingManager.SceneLoadOverride.
        /// Returns true to swallow the scene load. We:
        ///   - look up the map ID for this scene index
        ///   - vote on the lobby
        ///   - commit the current character (in case the player skipped CHARACTER_SELECTION)
        ///   - transition back to LOBBY_ROOM
        ///   - hide the hardcoded loading canvas
        /// Returns false if no map could be resolved (lets singleplayer-style replay through).
        /// </summary>
        private static bool HandleSceneLoadAttempt(int sceneBuildIndex)
        {
            if (!IsRoutingForLobby) return false;

            int mapId = ResolveMapIdFromBuildIndex(sceneBuildIndex);
            if (mapId < 0)
            {
                // Couldn't map this scene to a catalog entry — likely not a map scene; allow load.
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
                    if (!string.IsNullOrEmpty(code)) lobby.ServerSelectCharacter(code);
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
            // The map button prefab hardcodes SetActive(true) on a loading screen canvas before
            // calling LoadScene. We search the live scene for it and disable it again.
            // Tries common names first.
            var byName = GameObject.Find(LOADING_CANVAS_NAME);
            if (byName != null) { byName.SetActive(false); return; }

            // Fallback: scan inactive roots in active scene for anything looking like a loading screen.
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
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, 0.5f);
        }
    }
}
