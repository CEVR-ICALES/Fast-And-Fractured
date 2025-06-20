using FastAndFractured.Multiplayer;
using FishNet.Object;
using System;
using TMPro;
using UnityEngine;

public class DebugCharacterSelector : NetworkBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        dropdown.onValueChanged.AddListener(SelectCharacter);
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    public override void OnStartClient()
    {
        base.OnStartClient();

    }
    [ServerRpc]
    private void SelectCharacter(int characterSelectionUnparsed )
    {
        FindFirstObjectByType<NetworkedLevelControllerManager>().AddCharacterSkin(Owner,dropdown.options[characterSelectionUnparsed].text + "_0");
         
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
