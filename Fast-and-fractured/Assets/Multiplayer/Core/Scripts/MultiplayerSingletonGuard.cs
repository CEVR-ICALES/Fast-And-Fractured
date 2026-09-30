using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Runs VERY early to prevent singleton auxiliar creation cascade in multiplayer.
    /// 
    /// Problem: InteractableHandler.OnEnable() calls LevelController.Instance
    /// before it's initialized, creating a broken auxiliar. This cascades into dozens
    /// of NullReferenceExceptions.
    /// 
    /// Solution: This script runs before any singletons and ensures the scene's
    /// LevelController is registered as the singleton instance before
    /// anything else tries to access it. It also disables auto-initialization
    /// on singletons that should only be initialized by the server.
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

            // Register LevelController as the singleton instance before anything calls
            // .Instance, which would otherwise create an auxiliar one.
            if (_levelController != null)
            {
                // Use reflection to set s_Instance directly without triggering Construct.
                var instanceField = typeof(AbstractSingleton<LevelController>)
                    .GetField("s_Instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (instanceField != null)
                {
                    instanceField.SetValue(null, _levelController);
                }
                
                // Suppress auto-init so LevelController doesn't call Initialize() 
                // (which starts the game loop) before the server sends game data.
                // Construction is still allowed (other singletons need LevelController.Instance).
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

            // Disable InteractableHandler's OnEnable from triggering too early
            // It will be re-enabled when the server starts the game
            if (_interactableHandler != null)
            {
                // Disable auto-initialization flags so it doesn't try to Initialize on Start/OnEnable
                var autoConstructField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoConstructOnAwake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var autoInitStartField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoInitializeOnStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var autoInitEnableField = typeof(AbstractAutoInitializableMonoBehaviour)
                    .GetField("_autoInitializeOnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (autoConstructField != null) autoConstructField.SetValue(_interactableHandler, false);
                if (autoInitStartField != null) autoInitStartField.SetValue(_interactableHandler, false);
                if (autoInitEnableField != null) autoInitEnableField.SetValue(_interactableHandler, false);

                // Register InteractableHandler as singleton instance too
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
