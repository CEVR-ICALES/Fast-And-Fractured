using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Single exit gate for multiplayer map -> MainMenu transitions. It prepares networked
    /// abilities, gives Unity one frame to execute their deferred Destroy/OnDestroy callbacks,
    /// and only then unloads the map and its scene services.
    /// </summary>
    internal static class MultiplayerSceneTeardown
    {
        private static bool _mainMenuLoadPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _mainMenuLoadPending = false;
        }

        public static void LoadMainMenu()
        {
            if (_mainMenuLoadPending) return;
            _mainMenuLoadPending = true;

            NetworkedUniqueAbility.PrepareAllForSceneTeardown();

            var runnerObject = new GameObject("[MultiplayerSceneTeardown]");
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<SceneTeardownRunner>().Begin();
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

                SceneManager.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX);

                // Keep the duplicate-load guard armed through the actual scene transition.
                yield return null;
                _mainMenuLoadPending = false;
                Destroy(gameObject);
            }
        }
    }
}
