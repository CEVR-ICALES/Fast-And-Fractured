using System;
using System.Globalization;
using System.IO;

namespace FastAndFractured
{
    public sealed class DebugBenchmarkOptions
    {
        public const string COMMAND_LINE_FLAG = "-debugbench";
        public const string MAPS_FLAG = "-benchmaps";
        public const string SECONDS_FLAG = "-benchseconds";
        public const string MOOD_FLAG = "-benchmood";
        public const string RIVALS_FLAG = "-benchrivals";
        public const float DEFAULT_SECONDS = 20f;
        public const int DEFAULT_MOOD = 1;
        public const int DEFAULT_RIVALS = 7;

        private const string TRAINING = "Training";

        public bool Requested { get; private set; }
        public string[] Maps { get; private set; } = Array.Empty<string>();
        public float Seconds { get; private set; } = DEFAULT_SECONDS;
        public int Mood { get; private set; } = DEFAULT_MOOD;
        public int Rivals { get; private set; } = DEFAULT_RIVALS;

        public static DebugBenchmarkOptions From(string[] commandLine)
        {
            var options = new DebugBenchmarkOptions();
            if (commandLine == null) return options;
            options.Requested = Array.IndexOf(commandLine, COMMAND_LINE_FLAG) >= 0;
            string maps = ValueOf(commandLine, MAPS_FLAG);
            if (!string.IsNullOrEmpty(maps)) options.Maps = maps.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (float.TryParse(ValueOf(commandLine, SECONDS_FLAG), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && seconds > 0f)
                options.Seconds = seconds;
            if (int.TryParse(ValueOf(commandLine, MOOD_FLAG), out int mood) && mood >= 0) options.Mood = mood;
            if (int.TryParse(ValueOf(commandLine, RIVALS_FLAG), out int rivals) && rivals >= 0) options.Rivals = rivals;
            return options;
        }

        public bool Wants(string scenePath)
        {
            if (!DebugCustomMatch.IsPlayableMap(scenePath)) return false;
            string name = Path.GetFileNameWithoutExtension(scenePath);
            if (Maps.Length == 0) return name.IndexOf(TRAINING, StringComparison.OrdinalIgnoreCase) < 0;
            foreach (string map in Maps)
                if (name.IndexOf(map.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string ValueOf(string[] commandLine, string flag)
        {
            int index = Array.IndexOf(commandLine, flag);
            return index >= 0 && index + 1 < commandLine.Length ? commandLine[index + 1] : null;
        }
    }
}
