using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Host migration switch, off by default (Resources/HostMigrationConfig). The host's value travels in every
    /// snapshot, so a client only migrates when the lost host had it on.
    /// </summary>
    public static class HostMigrationSettings
    {
        public const string CONFIG_RESOURCES_PATH = "HostMigrationConfig";

        private static HostMigrationConfig _config;
        private static bool _loaded;

        public static bool Enabled
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _config = Resources.Load<HostMigrationConfig>(CONFIG_RESOURCES_PATH);
                }
                return _config != null && _config.Enabled;
            }
        }

        /// <summary>Room migration (on unless the config turns it off): the room survives its host leaving.</summary>
        public static bool RoomMigrationEnabled
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _config = Resources.Load<HostMigrationConfig>(CONFIG_RESOURCES_PATH);
                }
                return _config == null || _config.RoomMigration;
            }
        }

        /// <summary>Editor / tests: forget the cached asset so a toggle is seen immediately.</summary>
        public static void Reload() => _loaded = false;
    }
}
