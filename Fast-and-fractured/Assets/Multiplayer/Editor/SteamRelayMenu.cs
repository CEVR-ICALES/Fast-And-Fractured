using UnityEditor;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Editor
{ 
    public static class SteamRelayMenu
    {
        private const string MENU_PATH = "Tools/Fast And Fractured/Multiplayer/Steam Relay";
        private const string ASSET_PATH = "Assets/Multiplayer/Core/Resources/MultiplayerTransportConfig.asset";

        [MenuItem(MENU_PATH)]
        private static void Toggle()
        {
            var config = GetOrCreateConfig();
            config.UseSteamRelay = !config.UseSteamRelay;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            MultiplayerTransport.Reload();
            Debug.Log($"[MultiplayerTransport] Steam relay {(config.UseSteamRelay ? "ON" : "OFF")} ({ASSET_PATH}).");
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, MultiplayerTransport.SteamRelayConfigured);
            return true;
        }

        private static MultiplayerTransportConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MultiplayerTransportConfig>(ASSET_PATH);
            if (config != null) return config;
            if (!AssetDatabase.IsValidFolder("Assets/Multiplayer/Core/Resources"))
                AssetDatabase.CreateFolder("Assets/Multiplayer/Core", "Resources");
            config = ScriptableObject.CreateInstance<MultiplayerTransportConfig>();
            AssetDatabase.CreateAsset(config, ASSET_PATH);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
