using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
[CreateAssetMenu(fileName = "ListOfCharactersData", menuName = "CharacterCreator/ListOfCharactersData")]
public class ListOfCharactersData : ScriptableObject
{
    public List<CharacterData> listOfCharactersData;
}
}
