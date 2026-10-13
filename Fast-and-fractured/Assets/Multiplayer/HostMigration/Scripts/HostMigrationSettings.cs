using UnityEngine;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// The on/off switch of host migration. Off by default: with it off, nothing in this folder
    /// runs and a lost host ends the match exactly as before (HostDisconnectWatchdog returns to
    /// the hub). Toggle it on the Resources/HostMigrationConfig asset (Tools ▸ Fast And Fractured
    /// ▸ Host Migration) - no code changes, no rollback needed if it misbehaves.
    ///
    /// The host's value decides: it travels in every match snapshot, so a client only migrates
    /// when the host it lost had migration enabled.
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
