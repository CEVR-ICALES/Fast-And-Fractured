using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourcesLoader : MonoBehaviour
{
    // const string RESOURCES_PATH = "FastAndFractured/Resources/";
    const string PLAYER_ICONS_FOLDER = "PlayerIcons";
    const string UNIQUE_ABILITIES_ICONS_FOLDER = "UniqueAbilitiesIcons";
    const string INPUT_ICONS_FOLDER = "InputIcons";

    public void LoadResources(ref List<Texture2D> playerIcons, ref List<Texture2D> uniqueAbilitiesIcons, ref List<Texture2D> keyboardIcons, ref List<Texture2D> xboxIcons, ref List<Texture2D> playstationIcons)
    {
        // load all Texture2D from all the folders inside the given path into the playerIcons list
        playerIcons = new List<Texture2D>(Resources.LoadAll<Texture2D>(PLAYER_ICONS_FOLDER));
        uniqueAbilitiesIcons = new List<Texture2D>(Resources.LoadAll<Texture2D>(UNIQUE_ABILITIES_ICONS_FOLDER));
        keyboardIcons = new List<Texture2D>(Resources.LoadAll<Texture2D>(INPUT_ICONS_FOLDER + "/Keyboard"));
        xboxIcons = new List<Texture2D>(Resources.LoadAll<Texture2D>(INPUT_ICONS_FOLDER + "/Xbox"));
        playstationIcons = new List<Texture2D>(Resources.LoadAll<Texture2D>(INPUT_ICONS_FOLDER + "/Playstation"));
    }
}
