namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Every event the game can send. The key is the event name in GA4 and in Firestore:
    /// lowercase, words joined by '_', at most 40 characters (GA4's limit). Adding one here and
    /// calling Telemetry.Track with it is all a new statistic needs.
    /// </summary>
    public static class TelemetryKeys
    {
        // Session
        public const string SESSION_START = "session_start";   // version, hardware, language
        public const string SESSION_END = "session_end";       // duration_s

        // Menu
        public const string MENU_SCREEN = "menu_screen";       // screen

        // Matches (singleplayer and multiplayer)
        public const string MATCH_START = "match_start";       // mode, map, character, skin, characters
        public const string MATCH_END = "match_end";           // + won, duration_s, kills, damage, placement, fps
        public const string MATCH_ABANDONED = "match_abandoned"; // left before the end: mode, map, character, duration_s

        // Tutorial
        public const string TUTORIAL_START = "tutorial_start";
        public const string TUTORIAL_STEP = "tutorial_step";   // step, total, duration_s
        public const string TUTORIAL_COMPLETE = "tutorial_complete"; // duration_s
        public const string TUTORIAL_EXIT = "tutorial_exit";   // steps_done, total, completed, duration_s

        // Progression
        public const string LEVEL_UP = "level_up";             // level, total_xp
    }
}
