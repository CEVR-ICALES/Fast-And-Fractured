using UnityEngine;

namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "ListOfProtectedCharacters", menuName = "CharacterCreator/ListOfProtectedCharacters")]
    public class ListOfProtectedCharacters : ScriptableObject
    {
        public string[] ProtectedCharacters;
    }
}
