#if STEAMWORKS_NET
using Steamworks;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Initializes Steamworks.NET and pumps Steam callbacks every frame.
    /// Place this in the main menu scene; it survives scene loads via DontDestroyOnLoad.
    /// Requires steam_appid.txt (480 = Spacewar) in the project root for testing.
    /// </summary>
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }
        public static bool Initialized { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (!Packsize.Test())
            {
                Debug.LogError("[SteamManager] Packsize mismatch — wrong Steamworks.NET build platform.");
                return;
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[SteamManager] steam_api DLL mismatch.");
                return;
            }

            try
            {
                if (!SteamAPI.Init())
                {
                    Debug.LogError("[SteamManager] SteamAPI.Init() failed. Is Steam running?");
                    return;
                }
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError($"[SteamManager] steam_api DLL not found: {e.Message}");
                return;
            }

            Initialized = true;
            Debug.Log($"[SteamManager] Steam initialized. SteamID: {SteamUser.GetSteamID()} | Name: {SteamFriends.GetPersonaName()}");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;

            if (Initialized)
            {
                SteamAPI.Shutdown();
                Initialized = false;
            }
        }

        private void Update()
        {
            if (Initialized)
                SteamAPI.RunCallbacks();
        }
    }
}
#endif
