using FastAndFractured;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMenuCharacterData", menuName = "MainMenu/MenuCharacterData")]
public class CharacterMenuData : ScriptableObject
{
    public CharacterData CharacterStats => characterStats;
    public string CharacterName => characterName;
    public string CharacterDescription => characterDescription;
    public GameObject[] Models => models;



    [SerializeField] private CharacterData characterStats;
    [SerializeField] private string characterName;
    [SerializeField] private string characterDescription;
    [SerializeField] private GameObject[] models; 
}
