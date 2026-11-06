using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{
    public static class MultiplayerAvailabilityMenu
    {
        private const string MENU_PATH = "Tools/Fast And Fractured/Multiplayer/Multiplayer Enabled";
        private const string ASSET_PATH = "Assets/Multiplayer/Core/Resources/MultiplayerAvailabilityConfig.asset";

        [MenuItem(MENU_PATH)]
        private static void Toggle()
        {
            var config = AssetDatabase.LoadAssetAtPath<MultiplayerAvailabilityConfig>(ASSET_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MultiplayerAvailabilityConfig>();
                AssetDatabase.CreateAsset(config, ASSET_PATH);
            }
            config.MultiplayerEnabled = !config.MultiplayerEnabled;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            MultiplayerAvailability.Reload();
            Debug.Log($"[MultiplayerAvailability] Multiplayer {(config.MultiplayerEnabled ? "ENABLED" : "DISABLED")} ({ASSET_PATH}).");
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, MultiplayerAvailability.Enabled);
            return true;
        }
    }
}
