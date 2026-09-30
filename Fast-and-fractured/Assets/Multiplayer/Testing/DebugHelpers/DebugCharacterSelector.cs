using FastAndFractured.Multiplayer;
using FishNet.Object;
using TMPro;
using UnityEngine;

public class DebugCharacterSelector : NetworkBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    
    private NetworkedLobbyManager _lobbyManager;
    
    private void Start()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }
    }

    private void OnDestroy()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        TryResolveLobbyManager();
    }
    
    private void OnDropdownValueChanged(int index)
    {
        if (!IsOwner) return;
        SelectCharacterFromAuto(index);
    }
    
    // Public method for AutoCharacterSelector to call
    public bool SelectCharacterFromAuto(int dropdownIndex)
    {
        if (!IsOwner) return false;

        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null || dropdownIndex < 0 || dropdownIndex >= dropdown.options.Count)
            return false;

        string characterName = dropdown.options[dropdownIndex].text;
        return SubmitCharacterSelection(characterName);
    }

    public bool SelectCharacterByNameFromAuto(string characterName)
    {
        if (!IsOwner) return false;
        return SubmitCharacterSelection(characterName);
    }

    private bool SubmitCharacterSelection(string rawCharacterName)
    {
        string characterCode = NormalizeCharacterCode(rawCharacterName);
        if (string.IsNullOrEmpty(characterCode))
        {
            Debug.LogWarning("[DebugCharacterSelector] Tried to submit an empty character.");
            return false;
        }

        TryResolveLobbyManager();

        if (_lobbyManager != null)
        {
            _lobbyManager.ServerSelectCharacter(characterCode);
            return true;
        }

        // Legacy fallback if lobby manager is not present yet.
        SelectCharacterByName(characterCode);
        return true;
    }
    
    /// <summary>
    /// Sends the character name directly to the server.
    /// This avoids index mismatch issues between client/server dropdowns.
    /// </summary>
    [ServerRpc]
    private void SelectCharacterByName(string characterNameOrCode)
    {
        string characterNameCode = NormalizeCharacterCode(characterNameOrCode);
        if (string.IsNullOrEmpty(characterNameCode))
            return;
        
        Debug.Log($"<color=yellow>[DebugCharacterSelector] Server received selection from client {Owner.ClientId}: {characterNameCode}</color>");

        var lobbyManager = FindFirstObjectByType<NetworkedLobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.PlayerCharacterSelections[Owner.ClientId] = characterNameCode;
            return;
        }
        
        var levelManager = FindFirstObjectByType<NetworkedLevelControllerManager>();
        if (levelManager != null)
        {
            levelManager.AddCharacterSkin(Owner, characterNameCode);
        }
        
        // Notify AutoGameStarter if it exists
        var autoStarter = FindFirstObjectByType<AutoGameStarter>();
        if (autoStarter != null)
        {
            autoStarter.OnPlayerReady((int)Owner.ClientId);
        }
    }

    private void TryResolveLobbyManager()
    {
        if (_lobbyManager == null)
            _lobbyManager = FindFirstObjectByType<NetworkedLobbyManager>();
    }

    private static string NormalizeCharacterCode(string characterNameOrCode)
    {
        if (string.IsNullOrWhiteSpace(characterNameOrCode))
            return string.Empty;

        string trimmed = characterNameOrCode.Trim();
        int separatorIndex = trimmed.LastIndexOf('_');
        if (separatorIndex > 0 &&
            separatorIndex < trimmed.Length - 1 &&
            int.TryParse(trimmed.Substring(separatorIndex + 1), out _))
        {
            return trimmed;
        }

        return $"{trimmed}_0";
    }
}
