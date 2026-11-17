using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Progression
{
    /// <summary>
    /// The equipped accessories on the player's car in singleplayer too (the multiplayer wrapper
    /// does it online): when a level's characters start, without touching the singleplayer code.
    /// </summary>
    public static class SinglePlayerAccessories
    {
        private static LevelController _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _hooked = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!LevelController.HasInstance) return;
            var level = LevelController.Instance;
            if (level == _hooked || level.charactersCustomStart == null) return;
            _hooked = level;
            level.charactersCustomStart.AddListener(Apply);
        }

        private static void Apply()
        {
            if (InstanceFinder.IsClientStarted || InstanceFinder.IsServerStarted) return;   // online: the wrapper
            if (!LevelController.HasInstance) return;
            var player = LevelController.Instance.LocalPlayer;
            string code = PlayerPrefs.GetString("Selected_Player", string.Empty);
            if (player == null || string.IsNullOrEmpty(code)) return;
            try { AccessoryApplier.ApplyLocalLoadout(player, code.Split('_')[0]); }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }
}
