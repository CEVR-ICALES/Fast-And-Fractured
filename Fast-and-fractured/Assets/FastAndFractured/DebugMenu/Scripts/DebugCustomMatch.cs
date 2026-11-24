using System;
using System.IO;
using UnityEngine;

namespace FastAndFractured
{
    public static class DebugCustomMatch
    {
        public const int RANDOM_SPAWN = -1;

        public static void Request(int rivals, int spawnPoint)
        {
            LevelController.NextMatchCharacters = Mathf.Max(0, rivals) + 1;
            LevelController.NextMatchPlayerSpawn = spawnPoint >= 0 ? spawnPoint : (int?)null;
        }

        public static bool IsPlayableMap(string scenePath)
        {
            string name = Path.GetFileNameWithoutExtension(scenePath);
            if (string.IsNullOrEmpty(name) || name.StartsWith("Networked", StringComparison.Ordinal)) return false;
            return name.EndsWith("Map", StringComparison.Ordinal) || name.Contains("Training");
        }
    }
}
