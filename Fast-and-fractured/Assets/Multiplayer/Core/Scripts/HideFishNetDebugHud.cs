using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// The menu's NetworkManager is FishNet's demo prefab, which carries its debug HUD
    /// ("Start Server / Start Client" over the menu). The game connects through the lobby, so
    /// the HUD is switched off when a scene loads - the scene and the prefab stay untouched.
    /// </summary>
    public static class HideFishNetDebugHud
    {
        private const string HUD_COMPONENT = "NetworkHudCanvases";   // FishNet.Example, in the demos assembly

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Hide();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Hide();

        private static void Hide()
        {
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (behaviour != null && behaviour.GetType().Name == HUD_COMPONENT)
                    behaviour.gameObject.SetActive(false);
        }
    }
}
