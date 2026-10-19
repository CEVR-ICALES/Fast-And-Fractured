using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration.Editor
{
    /// <summary>
    /// Tools ▸ Fast And Fractured ▸ Host Migration ▸ Enable / Disable. Flips the flag on
    /// Resources/HostMigrationConfig (created if missing); builds pick it up from that asset.
    /// </summary>
    public static class HostMigrationMenu
    {
        private const string ASSET_PATH = "Assets/Multiplayer/HostMigration/Resources/HostMigrationConfig.asset";

        [MenuItem("Tools/Fast And Fractured/Host Migration/Enable")]
        public static void Enable() => SetEnabled(true);

        [MenuItem("Tools/Fast And Fractured/Host Migration/Disable")]
        public static void Disable() => SetEnabled(false);

        [MenuItem("Tools/Fast And Fractured/Host Migration/Enable", true)]
        private static bool CanEnable() => !HostMigrationSettings.Enabled;

        [MenuItem("Tools/Fast And Fractured/Host Migration/Disable", true)]
        private static bool CanDisable() => HostMigrationSettings.Enabled;

        public static HostMigrationConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<HostMigrationConfig>(ASSET_PATH);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<HostMigrationConfig>();
            AssetDatabase.CreateAsset(config, ASSET_PATH);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void SetEnabled(bool enabled)
        {
            var config = GetOrCreateConfig();
            config.Enabled = enabled;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            HostMigrationSettings.Reload();
            Debug.Log($"[HostMigration] Host migration {(enabled ? "ENABLED" : "disabled")} ({ASSET_PATH}).");
        }
    }
}
