using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Turns off the debug HUD of FishNet's demo NetworkManager in the menu when a scene loads.</summary>
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
