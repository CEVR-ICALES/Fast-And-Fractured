namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Where the statistics go: Firebase project "fast-and-fractured-stats" (cevricales@gmail.com).
    /// These values ship inside the game and are public by design: Firestore's security rules
    /// only let a player add their own data, never read anything.
    /// </summary>
    internal static class TelemetryConfig
    {
        public const string FIREBASE_PROJECT_ID = "fast-and-fractured-stats";
        public const string FIREBASE_API_KEY = "AIzaSyAZ83e06QlZlUtbnfwN_eboNngf7X4iWhY";

        public const string GA_MEASUREMENT_ID = "G-R01WQK9SDY";
        /// <summary>GA4 ▸ Admin ▸ Data streams ▸ Measurement Protocol API secrets. Empty = GA4 off.</summary>
        public const string GA_API_SECRET = "-lQQPWgzRNOpWTylT35Wcw";

        /// <summary>Mixed into the Steam ID before hashing, so the stored id is not the Steam ID itself.</summary>
        public const string STEAM_ID_SALT = "fast-and-fractured/telemetry/v1";

        public const int MAX_EVENTS_PER_BATCH = 200;   // Firestore rule
        public const int MAX_QUEUED_EVENTS = 2000;     // beyond that the oldest are dropped
        public const float FLUSH_INTERVAL = 90f;       // seconds between automatic sends
    }
}
