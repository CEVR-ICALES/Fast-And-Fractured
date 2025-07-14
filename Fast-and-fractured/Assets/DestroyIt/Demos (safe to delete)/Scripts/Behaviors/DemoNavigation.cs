using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DestroyIt
{
    public class DemoNavigation : MonoBehaviour
    {
        private readonly LoadSceneParameters lsp = new LoadSceneParameters { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };

        public void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void LoadMainScenariosDemoScene()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

#if UNITY_EDITOR
            EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DestroyIt/Demos (safe to delete)/Main Scenarios Scene.unity", lsp);
#else
    SceneManager.LoadSceneAsync("Assets/DestroyIt/Demos (safe to delete)/Main Scenarios Scene.unity", lsp);
#endif
        }

        public void LoadSUVShowcaseDemoScene()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

#if UNITY_EDITOR
            EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DestroyIt/Demos (safe to delete)/SUV Showcase Scene/SUV Showcase Scene.unity", lsp);
#else
    SceneManager.LoadSceneAsync("Assets/DestroyIt/Demos (safe to delete)/SUV Showcase Scene/SUV Showcase Scene.unity", lsp);
#endif
        }
    }
}