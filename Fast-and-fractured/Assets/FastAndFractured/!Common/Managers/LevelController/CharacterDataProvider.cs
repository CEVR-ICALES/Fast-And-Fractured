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
                        return character.CarDefaultPrefab;
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

            //Character Skin
            //Hierarchy for the character model '/Visuals/Character/{name}Character/Visuals/CharacterModel/{name}' 

            string characterPath = LevelConstants.CHARACTER_MATERIALS_FOLDER + "/" + name + LevelConstants.CHARACTER_MATERIALS_FOLDER + "/" + LevelConstants.CHARACTER_PREFAB_PATH + "/" + name;
            Transform character = visuals.Find(characterPath);
            
            if(!SetSkinPart(character, skinPath + "/" + LevelConstants.CHARACTER_MATERIALS_FOLDER))
            {
                Debug.LogError($"Character model to change the skin not found. Make sure the hierarchy to get the model is " + characterPath);
            }

            //Chassis Skin
            //Hierarchy for the chassis model '/Visuals/Chassis/{name}Chassis/Visuals/{name}Vehicle'

            string chassisPath = LevelConstants.CHASSIS_PREFAB_PATH + "/" + name + LevelConstants.CHASSIS_PREFAB_PATH + "/" + LevelConstants.VISUAL_CHARACTER_PARTS;
            Transform chassis = visuals.transform.Find(chassisPath);

            bool logError = false;

            if (chassis != null)
            {
                Transform chassisModel = chassis.GetChild(0);
               logError = !SetSkinPart(chassisModel, skinPath + "/" + LevelConstants.CHASSIS_MATERIALS_FOLDER);
            }
            else
                logError = true;
            if (logError)
                Debug.LogError($"Vehicle model to change the skin not found.Make sure the hierarchy to get the model is " + chassisPath);


            //Wheels Skin
            //Hierarchy for the wheels /Visuals/WheelsVisuals/[Front/Back][Left/Right]Wheel/WheelVisuals/Visuals/[anyName]

            Transform frontRightWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.FRONT_RIGHT_WHEEL_PATH);
            Transform backRightWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.BACK_RiGHT_WHEEL_PATH);
            Transform frontLeftWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.FRONT_LEFT_WHEEL_PATH);
            Transform backLeftWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.BACK_LEFT_WHEEL_PATH);

             logError = false;

            if (frontRightWheel!=null&&backRightWheel!=null&&frontLeftWheel!=null&&backLeftWheel!=null) 
            {
                Transform[] wheels = new Transform[]
                {
                frontRightWheel.GetChild(0),
                backRightWheel.GetChild(0),
                frontLeftWheel.GetChild(0),
                backLeftWheel.GetChild(0),
                };
               logError = !SetSkinPart(wheels, skinPath + "/" + LevelConstants.WHEElS_MATERIALS_FOLDER);
            }
            else
                logError = true;
            if (logError)
                Debug.LogError($"Wheels models to change the skin not found.Make sure the hierarchy to get the models is /Visuals/WheelsVisuals/[Front / Back][Left / Right]Wheel/WheelVisuals/Visuals/[anyName]");
        }

        private Material[] LoadSkinMaterials(string path)
        {
            return Resources.LoadAll<Material>(path);
        }

        private bool SetSkinPart(Transform instantiatedCarPart, string skinPartPath)
        {
            Material[] skinPart = LoadSkinMaterials(skinPartPath); 
            if (skinPart.Length != 0)
            {
                if (instantiatedCarPart == null)
                {
                    return false;
                }
                    Renderer renderPart = instantiatedCarPart.GetComponent<Renderer>();
                    Material[] defaultSkinMaterials = renderPart.materials;
                    for (int materialIterator = 0; materialIterator < defaultSkinMaterials.Length; materialIterator++)
                    {
                        if (skinPart.Length > materialIterator)
                        {
                            defaultSkinMaterials[materialIterator] = skinPart[materialIterator];
                        }
                        else
                        {
                            defaultSkinMaterials[materialIterator] = skinPart[materialIterator - 1];
                        }
                    }
                    renderPart.materials = defaultSkinMaterials;
            }
            return true;
        }

        private bool SetSkinPart(Transform[] instantiatedCarParts, string skinPartPath)
        {

            Material[] skinPart = LoadSkinMaterials(skinPartPath);
            if (skinPart.Length != 0)
            {
                foreach (Transform instantiatedCarPart in instantiatedCarParts)
                {
                    if (instantiatedCarPart == null)
                    {
                        return false;
                    }
                    Renderer renderPart = instantiatedCarPart.GetComponent<Renderer>();
                        Material[] defaultSkinMaterials = renderPart.materials;
                        for (int materialIterator = 0; materialIterator < defaultSkinMaterials.Length; materialIterator++)
                        {
                            if (skinPart.Length > materialIterator)
                            {
                                defaultSkinMaterials[materialIterator] = skinPart[materialIterator];
                            }
                            else
                            {
                                defaultSkinMaterials[materialIterator] = skinPart[materialIterator - 1];
                            }
                        }
                        renderPart.materials = defaultSkinMaterials;
                }
            }
            return true;
        }

        public List<string> CreateAllPossibleCharacterNameCodes(Dictionary<string, int> characterSelectedLimitTracker)
        {
            var allNameCodes = new List<string>();
            characterSelectedLimitTracker.Clear();

            ListOfCharactersSkins characterSkinsList = Resources.Load<ListOfCharactersSkins>(LevelConstants.LIST_OF_CHARACTERS_SKINS_PATH);

            foreach (var character in _charactersData)
            {
                allNameCodes.Add(character.CharacterName + LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + LevelConstants.DEFAULT_SKIN_ID.ToString());
                characterSelectedLimitTracker.Add(character.CharacterName, 0);
                int characterIndex = characterSkinsList.listOfCharacters.IndexOf(character.CharacterName);
                int characterSkinCount = characterSkinsList.listOfCharactersSkinCount[characterIndex];
                for (int i = 0; i < characterSkinCount; i++)
                {
                    allNameCodes.Add(character.CharacterName + LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + (i + 1).ToString());
                }

                int totalSkinsForCharacter = 1 + characterSkinCount;
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