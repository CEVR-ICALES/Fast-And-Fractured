using System.Collections.Generic;
using UnityEngine;
using UnityEngine.WSA;

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
                    //if (skinNum == LevelConstants.DEFAULT_SKIN_ID)
                    //{
                        return character.CarDefaultPrefab;
                    //}
                    //if (skinNum > 0 && (skinNum - 1) < character.CarWithSkinsPrefabs.Count)
                    //{
                    //    return character.CarWithSkinsPrefabs[skinNum - 1];
                    //}
                    //Debug.LogWarning($"Skin ID {skinNum} for character {name} not found. Check CharacterData.");
                    //return character.CarDefaultPrefab; // Fallback to default
                }
            }
            Debug.LogWarning($"Character with name {name} (from code {nameCode}) not found in CharacterData.");
            return null;
        }

        public void SetCharacterSkin(string nameCode, GameObject instantiatedCar)
        {
            LevelUtilities.ParseCharacterNameCode(nameCode, out string name, out int skinNum);

            string skinPath = LevelConstants.SKINS_LOADER_PATH + "/" + name + "/" + "_" + skinNum;
            Transform visuals = instantiatedCar.transform.Find(LevelConstants.VISUAL_CHARACTER_PARTS);
            string characterPath = LevelConstants.CHARACTER_MATERIALS + "/" + name + LevelConstants.CHARACTER_MATERIALS + "/" + LevelConstants.CHARACTER_PATH + "/" + name;
           //Character Skin
           Transform character = visuals.Find(characterPath);
            if(character!=null)
           SetSkinPart(character, skinPath + "/" + LevelConstants.CHARACTER_MATERIALS);

            //Chassis Skin
            string chassisPath = LevelConstants.CHASSIS_PATH + "/" + name + LevelConstants.CHASSIS_PATH + "/" + LevelConstants.VISUAL_CHARACTER_PARTS;
           Transform chassis = visuals.transform.Find(chassisPath)!.GetChild(0);
            if(chassis!=null)
           SetSkinPart(chassis, skinPath + "/" + LevelConstants.CHASSIS_MATERIALS);

           //Wheels Skin

        }

        private Material[] LoadSkinMaterials(string path)
        {
            return Resources.LoadAll<Material>(path);
        }

        private void SetSkinPart(Transform instantiatedCarPart, string skinPartPath)
        {

            Material[] skinPart = LoadSkinMaterials(skinPartPath); 
            if (skinPart.Length != 0)
            {
                Renderer renderPart = instantiatedCarPart.GetComponent<Renderer>();
                Material[] defaultSkinMaterials = renderPart.materials;
                for(int materialIterator = 0; materialIterator < defaultSkinMaterials.Length; materialIterator++)
                {
                    if (skinPart.Length > materialIterator)
                    {
                        defaultSkinMaterials[materialIterator] = skinPart[materialIterator];
                    }
                    else
                    {
                        defaultSkinMaterials[materialIterator] = null;
                    }
                }
                renderPart.materials = defaultSkinMaterials;
            }
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