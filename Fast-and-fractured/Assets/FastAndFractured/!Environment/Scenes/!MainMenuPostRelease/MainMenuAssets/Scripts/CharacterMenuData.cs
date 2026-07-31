using FastAndFractured;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMenuCharacterData", menuName = "MainMenu/MenuCharacterData")]
public class CharacterMenuData : ScriptableObject
{
    public CharacterData CharacterStats { get => characterStats; set => characterStats = value; }
    public string CharacterName { get => characterName; set => characterName = value; }
    public string CharacterDescription { get => characterDescription; set => characterDescription = value; }
    public GameObject[] Models { get => models; set => models = value; }



    [SerializeField] private CharacterData characterStats;
    [SerializeField] private string characterName;
    [SerializeField] private string characterDescription;
    [SerializeField] private GameObject[] models; 
}
