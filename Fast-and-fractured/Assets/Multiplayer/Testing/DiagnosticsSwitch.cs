using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    public static class DiagnosticsSwitch
    {
        public const string COMMAND_LINE_FLAG = "-mpdiag";

        // The recorders sample every frame and write logs: release builds only run them when asked.
        public static bool Enabled =>
            IsEnabled(Application.isEditor, Debug.isDebugBuild, System.Environment.GetCommandLineArgs());

        public static bool IsEnabled(bool isEditor, bool isDebugBuild, string[] commandLine) =>
            isEditor || isDebugBuild || (commandLine != null && System.Array.IndexOf(commandLine, COMMAND_LINE_FLAG) >= 0);
    }
}
