using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public static class LevelUtilities
    {
        public static T GetRandomValueFromList<T>(List<T> list, T defaultValue)
        {
            if (list == null || list.Count == 0)
            {
                return defaultValue;
            }
            return list[Random.Range(0, list.Count)];
        }

        public static void ShuffleList<T>(IList<T> list)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static void ParseCharacterNameCode(string nameCode, out string name, out int skinNum)
        {
            name = " ";
            skinNum = -1;
            if (string.IsNullOrEmpty(nameCode)) return;

            string[] dividedNameCode = nameCode.Split(LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE);
            if (dividedNameCode.Length == LevelConstants.LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME)
            {
                name = dividedNameCode[0];
                if (!int.TryParse(dividedNameCode[1], out skinNum))
                {
                    skinNum = -1;  
                }
            }
        }

        public static string ParseCharacterNameFromCode(string nameCode)
        {
            if (string.IsNullOrEmpty(nameCode)) return " ";

            string[] dividedNameCode = nameCode.Split(LevelConstants.DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE);
            if (dividedNameCode.Length >= 1)  
            {
                return dividedNameCode[0];
            }
            return " ";
        }
        
        public static bool CheckIfList1ElementsAreInList2<T>(List<T> list1, List<T> list2)
        {
            if (list1 == null || list2 == null) return false;
            if (list1.Count == 0) return true; 
            if (list2.Count == 0 && list1.Count > 0) return false;
            
            foreach (T item in list1)
            {
                if (!list2.Contains(item))
                {
                    return false;
                }
            }
            return true;
        }
    }
}