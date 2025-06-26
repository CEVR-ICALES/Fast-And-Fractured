using FastAndFractured.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utilities
{
    /// <summary>
    /// An abstract class that provides base functionalities of a singleton for its derived classes
    /// </summary>
    /// <typeparam name="T">The type of singleton instance</typeparam>
    [DefaultExecutionOrder(-222)]
    public abstract class AbstractSingleton<T> : AbstractAutoInitializableMonoBehaviour where T : Component
    {
        [SerializeField] SingletonType singletonType = SingletonType.TRUE;
        static T s_Instance;
        /// <summary>
        /// static Singleton instance
        /// </summary>
        public static T Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<T>();
                    return s_Instance;
                    if (s_Instance == null)
                    {
                        GameObject obj = new GameObject();
                        Debug.LogError("Created Auxiliar singleton" + typeof(T).Name);
                        obj.name = typeof(T).Name;
                        s_Instance = obj.AddComponent<T>();
                    }
                }

                return s_Instance;
            }
        }
        protected override void Construct()
        {
            InitializeSingleton();
        }
        public void InitializeSingleton()
        {
            if (s_Instance == null)
            {
                s_Instance = this as T;
            }
            else if (s_Instance != this)
            {
                if (singletonType == SingletonType.TRUE)
                {
                    Destroy(gameObject);
                }
                else
                {
                    enabled = false;
                }
            }
        }

    }
}
enum SingletonType
{
    TRUE,
    SOFT
}