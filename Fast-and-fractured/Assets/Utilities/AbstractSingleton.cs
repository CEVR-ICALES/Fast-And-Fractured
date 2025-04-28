using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utilities
{
    /// <summary>
    /// An abstract class that provides base functionalities of a singleton for its derived classes
    /// </summary>
    /// <typeparam name="T">The type of singleton instance</typeparam>
[DefaultExecutionOrder(-222)]    public abstract class AbstractSingleton<T> : MonoBehaviour where T : Component
    {
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
                    s_Instance = FindObjectOfType<T>();
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

        protected virtual void Awake()
        {
            if (s_Instance == null)
            {
                s_Instance = this as T;
            }
            else if (s_Instance != this)
            {
                Destroy(gameObject);
            }
        }
        
    }
}