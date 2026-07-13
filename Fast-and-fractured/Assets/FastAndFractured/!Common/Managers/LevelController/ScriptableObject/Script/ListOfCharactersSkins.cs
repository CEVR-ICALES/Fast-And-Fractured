using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "ListOfCharactersSkins", menuName = "CharacterCreator/ListOfCharactersSkins")]
    public class ListOfCharactersSkins : ScriptableObject
    {
        public List<string> listOfCharacters;
        public List<int> listOfCharactersSkinCount;
    }
}
