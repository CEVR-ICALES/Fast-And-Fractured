using System;
using UnityEngine;

namespace FastAndFractured
{
    public static class DebugMenuSwitch
    {
        public static readonly bool ENABLED_IN_BUILDS = true;
        public const string COMMAND_LINE_FLAG = "-debugmenu";

        public static bool Available =>
            IsAvailable(Application.isEditor, Debug.isDebugBuild, ENABLED_IN_BUILDS, Environment.GetCommandLineArgs());

        public static bool IsAvailable(bool isEditor, bool isDebugBuild, bool enabledInBuilds, string[] commandLine) =>
            isEditor || isDebugBuild || enabledInBuilds || (commandLine != null && Array.IndexOf(commandLine, COMMAND_LINE_FLAG) >= 0);
    }
}
