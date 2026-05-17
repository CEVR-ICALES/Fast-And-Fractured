using UnityEngine;

namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "ListOfProtectedCharacters", menuName = "ListOfProtectedCharacters")]
    public class ListOfProtectedCharacters : ScriptableObject
    {
        public string[] ProtectedCharacters;
    }
}
