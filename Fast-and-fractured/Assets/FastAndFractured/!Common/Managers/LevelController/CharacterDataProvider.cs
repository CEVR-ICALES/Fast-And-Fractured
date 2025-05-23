using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class CharacterDataProvider
    {
        private readonly List<CharacterData> _charactersData;

        public CharacterDataProvider(List<CharacterData> charactersData)
        {
            _charactersData = charactersData ?? new List<CharacterData>();
        }

        public GameObject GetCharacterPrefab(string nameCode)
        {
            LevelUtilities.ParseCharacterNameCode(nameCode, out string name, out int skinNum);

            foreach (var character in _charactersData)
            {
                if (character.CharacterName == name)
                {
                    if (skinNum == LevelConstants.DEFAULT_SKIN_ID)
                    {
                        return character.CarDefaultPrefab;
                    }
                    if (skinNum > 0 && (skinNum - 1) < character.CarWithSkinsPrefabs.Count)
                    {
                        return character.CarWithSkinsPrefabs[skinNum - 1];
                    }
                    Debug.LogWarning($"Skin ID {skinNum} for character {name} not found. Check CharacterData.");
                    return character.CarDefaultPrefab; // Fallback to default
                }
            }
            Debug.LogWarning($"Character with name {name} (from code {nameCode}) not found in CharacterData.");
            return null;
        }

        public List<string> CreateAllPossibleCharacterNameCodes(Dictionary<string, int> characterSelectedLimitTracker)
        {
            var allNameCodes = new List<string>();
            characterSelectedLimitTracker.Clear();

            foreach (var character in _charactersData)
            {
                allNameCodes.Add(character.CharacterName + LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + LevelConstants.DEFAULT_SKIN_ID.ToString());
                characterSelectedLimitTracker.Add(character.CharacterName, 0);
                for (int i = 0; i < character.CarWithSkinsPrefabs.Count; i++)
                {
                    allNameCodes.Add(character.CharacterName + LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + (i + 1).ToString());
                }

                int totalSkinsForCharacter = 1 + character.CarWithSkinsPrefabs.Count;
                if (totalSkinsForCharacter < LevelConstants.DEFAULT_LIMIT_OF_SAME_CHARACTER_SPAWNED)
                {
                    int difference = LevelConstants.DEFAULT_LIMIT_OF_SAME_CHARACTER_SPAWNED - totalSkinsForCharacter;
                    for (int i = 0; i < difference; i++)
                    {
                        allNameCodes.Add(character.CharacterName + LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + LevelConstants.DEFAULT_SKIN_ID.ToString());
                    }
                }
            }
            return allNameCodes;
        }
    }
}