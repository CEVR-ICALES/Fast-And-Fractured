using FastAndFractured.Multiplayer.Abstractions;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>The name every peer shows for a match character: the player's for a human, the bot's for an AI.</summary>
    public static class MatchNames
    {
        /// <summary>Stable bot name pool. Index = AI slot / AI order in the match.</summary>
        public static readonly string[] BotNames =
        {
            "Bot Sandra", "Bot Mike",   "Bot Carla",  "Bot Diego",
            "Bot Lucia",  "Bot Tomas",  "Bot Elena",  "Bot Marcos",
            "Bot Paula",  "Bot Hector", "Bot Nuria",  "Bot Javier",
        };

        public static string BotName(int aiIndex) =>
            BotNames[((aiIndex % BotNames.Length) + BotNames.Length) % BotNames.Length];

        /// <summary>Name of the character this object belongs to (a car, its root or any part of it).</summary>
        public static string Of(GameObject anyPartOfCharacter)
        {
            if (anyPartOfCharacter == null) return null;

            if (TryGetOwner(anyPartOfCharacter.transform, out int clientId))
            {
                var lobby = LobbyLocator.GetLobbyManager();
                return lobby != null ? lobby.GetPlayerName(clientId) : $"Player {clientId}";
            }

            int aiIndex = AiIndexOf(anyPartOfCharacter.transform);
            return aiIndex >= 0 ? BotName(aiIndex) : CleanObjectName(anyPartOfCharacter.name);
        }

        public static bool TryGetOwner(Transform part, out int clientId)
        {
            clientId = -1;
            foreach (var nob in part.GetComponentsInParent<NetworkObject>(true))
                if (nob.Owner != null && nob.Owner.IsValid) { clientId = nob.Owner.ClientId; return true; }
            foreach (var nob in part.GetComponentsInChildren<NetworkObject>(true))
                if (nob.Owner != null && nob.Owner.IsValid) { clientId = nob.Owner.ClientId; return true; }
            return false;
        }

        /// <summary>Position of this AI among the match's AIs (spawn order, the same on every peer), or -1.</summary>
        public static int AiIndexOf(Transform part)
        {
            var level = LevelController.ExistingInstance;
            if (level == null || level.InGameCharacters == null) return -1;
            int aiIndex = 0;
            foreach (var character in level.InGameCharacters)
            {
                if (character == null) continue;
                if (TryGetOwner(character.transform, out _)) continue;
                var c = character.transform;
                if (part == c || part.IsChildOf(c) || c.IsChildOf(part)) return aiIndex;
                aiIndex++;
            }
            return -1;
        }

        private static string CleanObjectName(string name) =>
            name.Replace("(Clone)", string.Empty).Replace("Networked", string.Empty).Trim();
    }
}
