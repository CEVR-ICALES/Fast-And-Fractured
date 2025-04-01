using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using UnityEngine.UI;
using Enums;

[RequireComponent(typeof(ResourcesLoader))]
public class ResourcesManager : AbstractSingleton<ResourcesManager>
{

    public List<Texture2D> PlayerIcons => _playerIcons;
    private List<Texture2D> _playerIcons;
    public List<Texture2D> UniqueAbilitiesIcons => _uniqueAbilitiesIcons;
    private List<Texture2D> _uniqueAbilitiesIcons;
    public List<Texture2D> KeyboardIcons => _keyboardIcons;
    private List<Texture2D> _keyboardIcons;
    public List<Texture2D> XboxIcons => _xboxIcons;
    private List<Texture2D> _xboxIcons;
    public List<Texture2D> PlaystationIcons => _playstationIcons;
    private List<Texture2D> _playstationIcons;

    private ResourcesLoader _resourcesLoader;

    void Start()
    {
        _resourcesLoader = gameObject.GetComponent<ResourcesLoader>();
        _resourcesLoader.LoadResources(ref _playerIcons, ref _uniqueAbilitiesIcons, ref _keyboardIcons, ref _xboxIcons, ref _playstationIcons);
    }

    void InitPlayerIconsDictionary(Dictionary<PlayerIcons, Texture2D> dictionary, List<Texture2D> playerIconsTextures)
    {
        foreach (Texture2D icon in playerIconsTextures)
        {
            if (System.Enum.TryParse(icon.name.ToUpper(), out PlayerIcons iconEnum))
            {
                dictionary.Add(iconEnum, icon);
            }
            else
            {
                Debug.LogError($"Icon {icon.name} not found in PlayerIcons enum.");
            }
        }
    }

    void InitUAIconsDictionary(Dictionary<UniqueAbilitiesIcons, Texture2D> dictionary, List<Texture2D> uaTextures)
    {
        foreach (Texture2D icon in uaTextures)
        {
            // add to the dictionary acording to the texture name (same as enum name with uppercase)
            if (System.Enum.TryParse(icon.name.ToUpper(), out UniqueAbilitiesIcons iconEnum))
            {
                dictionary.Add(iconEnum, icon);
            }
            else
            {
                Debug.LogError($"Icon {icon.name} not found in UniqueAbilitiesIcons enum.");
            }
        }
    }
}
