using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Enums;
using FastAndFractured;

[RequireComponent(typeof(ResourcesLoader))]
public class ResourcesManager : AbstractSingleton<ResourcesManager>
{

    public List<Sprite> PlayerIcons => _playerIcons;
    private List<Sprite> _playerIcons;
    public List<Sprite> UniqueAbilitiesIcons => _uniqueAbilitiesIcons;
    private List<Sprite> _uniqueAbilitiesIcons;
    public List<Sprite> ScreenEffectsSprites => _screenEffectsSprites;
    private List<Sprite> _screenEffectsSprites;
    public List<Sprite> KeyboardIcons => _keyboardIcons;
    private List<Sprite> _keyboardIcons;
    public List<Sprite> XboxIcons => _xboxIcons;
    private List<Sprite> _xboxIcons;
    public List<Sprite> PlaystationIcons => _playstationIcons;
    private List<Sprite> _playstationIcons;

    private ResourcesLoader _resourcesLoader;

    private Dictionary<PlayerIcons, Sprite> _playerIconsDictionary = new Dictionary<PlayerIcons, Sprite>();
    private Dictionary<UniqueAbilitiesIcons, Sprite> _uaIconsDictionary = new Dictionary<UniqueAbilitiesIcons, Sprite>();
    private Dictionary<ScreenEffects, Sprite> _screenEffectsDictionary = new Dictionary<ScreenEffects, Sprite>();


    void Start()
    {
        _resourcesLoader = gameObject.GetComponent<ResourcesLoader>();
        _resourcesLoader.LoadResources(ref _playerIcons, ref _uniqueAbilitiesIcons, ref _screenEffectsSprites, ref _keyboardIcons, ref _xboxIcons, ref _playstationIcons);

        InitPlayerIconsDictionary(_playerIcons);
        InitUAIconsDictionary(_uniqueAbilitiesIcons);
        InitScreenEffectsDictionary(_screenEffectsSprites);
        IngameEventsManager.Instance.SetCharactersTopElements();
    }

    void InitPlayerIconsDictionary(List<Sprite> playerIconsSprites)
    {
        foreach (Sprite icon in playerIconsSprites)
        {
            if (System.Enum.TryParse(icon.name.ToUpper(), out PlayerIcons iconEnum))
            {
                _playerIconsDictionary.Add(iconEnum, icon);
            }
            else
            {
                Debug.LogWarning($"Icon {icon.name} not found in PlayerIcons enum.");
            }
        }
    }

    void InitUAIconsDictionary(List<Sprite> uaSprites)
    {
        foreach (Sprite icon in uaSprites)
        {
            if (System.Enum.TryParse(icon.name.ToUpper(), out UniqueAbilitiesIcons iconEnum))
            {
                _uaIconsDictionary.Add(iconEnum, icon);
            }
            else
            {
                Debug.LogWarning($"Icon {icon.name} not found in UniqueAbilitiesIcons enum.");
            }
        }
    }

    void InitScreenEffectsDictionary(List<Sprite> screenEffectsSprites)
    {
        foreach (Sprite sprite in screenEffectsSprites)
        {
            if (System.Enum.TryParse(sprite.name.ToUpper(), out ScreenEffects spriteEnum))
            {
                _screenEffectsDictionary.Add(spriteEnum, sprite);
            }
            else
            {
                Debug.LogWarning($"Sprite {sprite.name} not found in ScreenEffects enum.");
            }
        }
    }

    /// <summary>
    /// Retrieves a Sprite for a given resources icon enum key.
    /// </summary>
    public Sprite GetResourcesSprite(PlayerIcons iconKey)
    {
        if (_playerIconsDictionary.TryGetValue(iconKey, out Sprite icon))
        {
            return icon;
        }

        Debug.LogWarning($"Player icon for key {iconKey} not found.");
        return null;
    }

    // Overload
    public Sprite GetResourcesSprite(UniqueAbilitiesIcons iconKey)
    {
        if (_uaIconsDictionary.TryGetValue(iconKey, out Sprite icon))
        {
            return icon;
        }

        Debug.LogWarning($"Unique ability icon for key {iconKey} not found.");
        return null;
    }

    // Overload
    public Sprite GetResourcesSprite(ScreenEffects spriteKey)
    {
        if (_screenEffectsDictionary.TryGetValue(spriteKey, out Sprite sprite))
        {
            return sprite;
        }

        Debug.LogWarning($"Screen effect sprite for key {spriteKey} not found.");
        return null;
    }

    // Overload
    public Sprite GetResourcesSprite(string spriteKey) // provisional
    {
        foreach(Sprite sprite in _playerIconsDictionary.Values)
        {
            if(sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _screenEffectsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }


        foreach (Sprite sprite in _uaIconsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        Debug.LogWarning($"Sprite with name {spriteKey} not found.");
        return null;
    }
}
