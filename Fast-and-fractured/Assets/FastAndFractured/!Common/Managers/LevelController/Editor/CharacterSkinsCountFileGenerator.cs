using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace FastAndFractured {

    public class CharacterSkinsCountFileGenerator
    {
        private const string RESOURCES_FOLDER_PATH = "Assets/FastAndFractured/Resources";
        private const string LIST_OF_CHARACTER_SKINS = "/ListOfCharacterSkins.asset";

        [MenuItem("Tools/GenerateCharacterSkinCountFile")]
        public static void GenerateCharacterSkinsCountFile()
        {
            string characterFolderPath = RESOURCES_FOLDER_PATH + "/" + LevelConstants.SKINS_LOADER_PATH;
            if (!Directory.Exists(characterFolderPath))
            {
                Debug.LogError("Folder " + characterFolderPath + " doesn't exist");
                return;
            }
            string[] characters = Directory.GetDirectories(characterFolderPath, "*", SearchOption.TopDirectoryOnly);

            string[] charactersName = new string[characters.Length];
            int[] skinCount = new int[characters.Length];

            for (int i = 0; i < characters.Length; i++) 
            {
                charactersName[i] = Path.GetFileName(characters[i]);
                string[] subfolders = Directory.GetDirectories(characterFolderPath + "/" + charactersName[i], "*", SearchOption.TopDirectoryOnly);
                skinCount[i] = subfolders.Length;
            }
            ListOfCharactersSkins asset = ScriptableObject.CreateInstance<ListOfCharactersSkins>();
            asset.listOfCharacters = charactersName.ToList();
            asset.listOfCharactersSkinCount = skinCount.ToList();

            AssetDatabase.CreateAsset(asset, characterFolderPath + LIST_OF_CHARACTER_SKINS);
            AssetDatabase.SaveAssets();
        }
    }
}
