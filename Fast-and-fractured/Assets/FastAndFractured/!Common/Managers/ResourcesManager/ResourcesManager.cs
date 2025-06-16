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
    public List<Sprite> PlayerPortraits => _playerPortraits;
    private List<Sprite> _playerPortraits;
    public List<Sprite> PlayerHalfBody => _playerHalfBody;
    private List<Sprite> _playerHalfBody;
    public List<Sprite> UniqueAbilitiesIcons => _uniqueAbilitiesIcons;
    private List<Sprite> _uniqueAbilitiesIcons;
    public List<Sprite> PushShootIcons => _pushShootIcons;
    private List<Sprite> _pushShootIcons;
    public List<Sprite> NormalShootIcons => _normalShootIcons;
    private List<Sprite> _normalShootIcons;
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
    private Dictionary<PlayerPortraits, Sprite> _playerPortraitsDictionary = new Dictionary<PlayerPortraits, Sprite>();
    private Dictionary<PlayerHalfBody, Sprite> _playerHalfBodyDictionary = new Dictionary<PlayerHalfBody, Sprite>();
    private Dictionary<UniqueAbilitiesIcons, Sprite> _uaIconsDictionary = new Dictionary<UniqueAbilitiesIcons, Sprite>();
    private Dictionary<PushShootIcons, Sprite> _pushShootIconsDictionary = new Dictionary<PushShootIcons, Sprite>();
    private Dictionary<NormalShootIcons, Sprite> _normalShootIconsDictionary = new Dictionary<NormalShootIcons, Sprite>();
    private Dictionary<ScreenEffects, Sprite> _screenEffectsDictionary = new Dictionary<ScreenEffects, Sprite>();

    protected override void Construct()
    {
        base.Construct();
        _resourcesLoader = gameObject.GetComponent<ResourcesLoader>();
        _resourcesLoader.LoadResources(ref _playerIcons, ref _playerPortraits, ref _playerHalfBody, ref _uniqueAbilitiesIcons, ref _pushShootIcons, ref _normalShootIcons, ref _screenEffectsSprites, ref _keyboardIcons, ref _xboxIcons, ref _playstationIcons);

        InitDictionary(typeof(PlayerIcons), _playerIcons, _playerIconsDictionary);
        InitDictionary(typeof(PlayerPortraits), _playerPortraits, _playerPortraitsDictionary);
        InitDictionary(typeof(PlayerHalfBody), _playerHalfBody, _playerHalfBodyDictionary);
        InitDictionary(typeof(UniqueAbilitiesIcons), _uniqueAbilitiesIcons, _uaIconsDictionary);
        InitDictionary(typeof(PushShootIcons), _pushShootIcons, _pushShootIconsDictionary);
        InitDictionary(typeof(NormalShootIcons), _normalShootIcons, _normalShootIconsDictionary);
        InitDictionary(typeof(ScreenEffects), _screenEffectsSprites, _screenEffectsDictionary);
    }


    private void InitDictionary(System.Type enumType, List<Sprite> sprites, IDictionary dictionary)
    {
        foreach (Sprite sprite in sprites)
        {
            if (System.Enum.IsDefined(enumType, sprite.name.ToUpper()))
            {
                var enumKey = System.Enum.Parse(enumType, sprite.name.ToUpper());
                dictionary.Add(enumKey, sprite);
            }
            else
            {
                Debug.LogWarning($"Sprite {sprite.name} not found in {enumType.Name} enum.");
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
    public Sprite GetResourcesSprite(PushShootIcons iconKey)
    {
        if (_pushShootIconsDictionary.TryGetValue(iconKey, out Sprite icon))
        {
            return icon;
        }

        Debug.LogWarning($"Push shoot icon for key {iconKey} not found.");
        return null;
    }

    // Overload
    public Sprite GetResourcesSprite(NormalShootIcons iconKey)
    {
        if (_normalShootIconsDictionary.TryGetValue(iconKey, out Sprite icon))
        {
            return icon;
        }

        Debug.LogWarning($"Normal shoot icon for key {iconKey} not found.");
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
        foreach (Sprite sprite in _playerIconsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _playerPortraitsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _playerHalfBodyDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _screenEffectsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _uaIconsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _pushShootIconsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        foreach (Sprite sprite in _normalShootIconsDictionary.Values)
        {
            if (sprite.name.ToUpper() == spriteKey.ToUpper()) return sprite;
        }

        Debug.LogWarning($"Sprite with name {spriteKey} not found.");
        return null;
    }
}
