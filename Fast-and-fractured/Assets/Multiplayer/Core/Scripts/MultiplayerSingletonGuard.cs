using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Runs before every singleton: registers the scene's LevelController and InteractableHandler as instances,
    /// so an early .Instance call does not create an auxiliary one, and holds their initialization for the server.
    /// </summary>
    [DefaultExecutionOrder(-999)]
    public class MultiplayerSingletonGuard : MonoBehaviour
    {
        [Header("Scene Singletons to Guard")]
        [Tooltip("The LevelController in the scene (auto-found if null)")]
        [SerializeField] private LevelController _levelController;
        
        [Tooltip("The InteractableHandler in the scene (auto-found if null)")]
        [SerializeField] private InteractableHandler _interactableHandler;

        private void Awake()
        {
            if (_levelController == null)
                _levelController = FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
            
            if (_interactableHandler == null)
                _interactableHandler = FindFirstObjectByType<InteractableHandler>(FindObjectsInactive.Include);

            if (_levelController != null)
            {
                // s_Instance directly, without triggering Construct.
                var instanceField = typeof(AbstractSingleton<LevelController>)
                    .GetField("s_Instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (instanceField != null)
                {
                    instanceField.SetValue(null, _levelController);
                }
                
                // No auto-init (it starts the game loop) before the server's game data; construction stays allowed.
                var autoInitStartField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoInitializeOnStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var autoInitEnableField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoInitializeOnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (autoInitStartField != null) autoInitStartField.SetValue(_levelController, false);
                if (autoInitEnableField != null) autoInitEnableField.SetValue(_levelController, false);
            }
            else
            {
                Debug.LogWarning("[MultiplayerSingletonGuard] LevelController not found in scene!");
            }

            // InteractableHandler waits for the server to start the game.
            if (_interactableHandler != null)
            {
                var autoConstructField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoConstructOnAwake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var autoInitStartField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoInitializeOnStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var autoInitEnableField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoInitializeOnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (autoConstructField != null) autoConstructField.SetValue(_interactableHandler, false);
                if (autoInitStartField != null) autoInitStartField.SetValue(_interactableHandler, false);
                if (autoInitEnableField != null) autoInitEnableField.SetValue(_interactableHandler, false);

                var ihInstanceField = typeof(AbstractSingleton<InteractableHandler>)
                    .GetField("s_Instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (ihInstanceField != null)
                {
                    ihInstanceField.SetValue(null, _interactableHandler);
                }
            }
        }
    }
}
