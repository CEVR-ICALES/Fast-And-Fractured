using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utilities
{
    /// <summary>
    /// An abstract class that provides base functionalities of a singleton for its derived classes
    /// </summary>
    /// <typeparam name="T">The type of singleton instance</typeparam>
    [DefaultExecutionOrder(-222)]
    public abstract class AbstractSingleton<T> : AbstractAutoInitializableMonoBehaviour, IOverwritableSingleton where T : Component
    {
        [SerializeField] SingletonDuplicateResolution singletonDuplicateResolution = SingletonDuplicateResolution.DISABLE_COMPONENT;
        [SerializeField] bool alsoDisableGameobjectOnDuplicateResolution = false;
        static T s_Instance;
        /// <summary>
        /// Check if instance exists without creating it
        /// </summary>
        public static bool HasInstance
        {
            get
            {
                if (s_Instance != null) return true;
                var objects = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                return objects != null && objects.Length > 0;
            }
        }
        /// <summary>
        /// Returns an existing instance without creating a new one.
        /// </summary>
        public static T ExistingInstance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                var objects = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (objects != null && objects.Length > 0)
                {
                    s_Instance = objects[0];
                }
                return s_Instance;
            }
        }
        /// <summary>
        /// static Singleton instance
        /// </summary>
        public static T Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    // Use FindObjectsByType to exclude inactive components
                    var allInstances = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    if (allInstances != null && allInstances.Length > 0)
                    {
                        s_Instance = allInstances[0];
                    }
                    
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
            else if (s_Instance != this && s_Instance.gameObject != this.gameObject)
            {
                HandleDuplicate();

            }
        }
        private void HandleDuplicate()
        {
            Debug.LogWarning($"<color=red>Duplicate singleton '{typeof(T).Name}' on '{gameObject.name}'. " +
                             $"Resolving via {singletonDuplicateResolution}.</color>", this);

            switch (singletonDuplicateResolution)
            {
                case SingletonDuplicateResolution.DESTROY_GAMEOBJECT:
                    Destroy(gameObject);
                    break;
                case SingletonDuplicateResolution.DISABLE_COMPONENT:
                    this.enabled = false;
                    break;
            }
            Deconstruct();
            if (alsoDisableGameobjectOnDuplicateResolution)
            {
                gameObject.SetActive(false);
            }
        }

        public void ClaimSingletonOwnership()
        {
            if (s_Instance == this)
            {
                return;
            }
            bool wasInitialized = false;
            bool wasConstructed = false;
            if (s_Instance != null)
            {
                Debug.Log($"<color=orange>'{gameObject.name}' is claiming ownership of singleton '{typeof(T).Name}'. " +
                          $"Destroying previous instance on '{s_Instance.gameObject.name}'.</color>");

                var oldSingleton = s_Instance as AbstractSingleton<T>;
                if (oldSingleton != null)
                {
                    wasConstructed = oldSingleton._isConstructed;
                    wasInitialized = oldSingleton._isInitialized;
                    oldSingleton.HandleDuplicate();
                }
                else
                {
                    Destroy(s_Instance.gameObject);
                }
            }
            s_Instance = this as T;
            enabled = true;
            if (wasConstructed)
            {
                Construct();
            }

            if (wasInitialized)
            {
                Initialize();
            }
        }

        public void ForceConstruct()
        {
            PerformConstruct();
        }

        public void ForceInitialize()
        {
            PerformInitialize();
        }
        protected override void Deconstruct()
        {
            base.Deconstruct();
        }
    }
}

enum SingletonDuplicateResolution
{
    DESTROY_GAMEOBJECT,
    DISABLE_COMPONENT
}
// Technically only needed for the multiplayer (at least for now)
public interface IOverwritableSingleton
{
    /// <summary>
    /// Forces this instance to become the primary singleton, destroying any existing one.
    /// </summary>
    void ClaimSingletonOwnership();

    void ForceConstruct();

    void ForceInitialize();
}
