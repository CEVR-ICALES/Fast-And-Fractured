using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured{
[CreateAssetMenu(fileName = "MainMenuSelectionScreenCharacters", menuName = "MainMenu/MainMenuSelectionScreenCharacters")]
public class MainMenuSelectionScreenCharacters : ScriptableObject
{
   public List<CharacterMenuData> allMainMenuCharactersData;
}
}
