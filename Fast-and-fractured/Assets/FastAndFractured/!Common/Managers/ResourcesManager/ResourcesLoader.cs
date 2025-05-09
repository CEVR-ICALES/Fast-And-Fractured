using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourcesLoader : MonoBehaviour
{
    // const string RESOURCES_PATH = "FastAndFractured/Resources/";
    const string PLAYER_ICONS_FOLDER = "PlayerIcons";
    const string PLAYER_PORTRAITS_FOLDER = "PlayerPortraitsIcons";
    const string PLAYER_HALF_BODY_FOLDER = "PlayerHalfBodyIcons";
    const string UNIQUE_ABILITIES_ICONS_FOLDER = "UniqueAbilitiesIcons";
    const string PUSH_SHOOT_ICONS_FOLDER = "PushShootIcons";
    const string NORMAL_SHOOT_ICONS_FOLDER = "NormalShootIcons";
    const string SCREEN_EFFECTS_SPRITES_FOLDER = "ScreenEffects";
    const string INPUT_ICONS_FOLDER = "InputIcons";

    public void LoadResources(ref List<Sprite> playerIcons, ref List<Sprite> playerPortraits, ref List<Sprite> playerHalfBody, ref List<Sprite> uniqueAbilitiesIcons, ref List<Sprite> pushShootIcons, ref List<Sprite> normalShootIcons, ref List<Sprite> screenEffectsSprites, ref List<Sprite> keyboardIcons, ref List<Sprite> xboxIcons, ref List<Sprite> playstationIcons)
    {
        // load all Sprite from all the folders inside the given path into the playerIcons list
        playerIcons = new List<Sprite>(Resources.LoadAll<Sprite>(PLAYER_ICONS_FOLDER));
        playerPortraits = new List<Sprite>(Resources.LoadAll<Sprite>(PLAYER_PORTRAITS_FOLDER));
        playerHalfBody = new List<Sprite>(Resources.LoadAll<Sprite>(PLAYER_HALF_BODY_FOLDER));
        uniqueAbilitiesIcons = new List<Sprite>(Resources.LoadAll<Sprite>(UNIQUE_ABILITIES_ICONS_FOLDER));
        pushShootIcons = new List<Sprite>(Resources.LoadAll<Sprite>(PUSH_SHOOT_ICONS_FOLDER));
        normalShootIcons = new List<Sprite>(Resources.LoadAll<Sprite>(NORMAL_SHOOT_ICONS_FOLDER));
        screenEffectsSprites = new List<Sprite>(Resources.LoadAll<Sprite>(SCREEN_EFFECTS_SPRITES_FOLDER));
        keyboardIcons = new List<Sprite>(Resources.LoadAll<Sprite>(INPUT_ICONS_FOLDER + "/Keyboard"));
        xboxIcons = new List<Sprite>(Resources.LoadAll<Sprite>(INPUT_ICONS_FOLDER + "/Xbox"));
        playstationIcons = new List<Sprite>(Resources.LoadAll<Sprite>(INPUT_ICONS_FOLDER + "/Playstation"));
    }
}
