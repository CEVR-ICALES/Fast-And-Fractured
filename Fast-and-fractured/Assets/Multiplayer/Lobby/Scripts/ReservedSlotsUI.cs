using System.Linq;
using Assets.SimpleLocalization.Scripts;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class ReservedSlotsUI : MonoBehaviour
    {
        private const float GAP = 6f;

        private TMP_Text _label;
        private Button _decButton;
        private Button _incButton;
        private NetworkedLobbyManager _lobby;

        public static ReservedSlotsUI CreateBelow(AiSlotsUI aiPanel)
        {
            var copy = Instantiate(aiPanel.gameObject, aiPanel.transform.parent);
            copy.name = "ReservedSlots";
            DestroyImmediate(copy.GetComponent<AiSlotsUI>());
            foreach (var localized in copy.GetComponentsInChildren<LocalizedText>(true)) DestroyImmediate(localized);

            var source = (RectTransform)aiPanel.transform;
            var rect = (RectTransform)copy.transform;
            rect.anchoredPosition = source.anchoredPosition - new Vector2(0f, source.rect.height + GAP);
            copy.transform.SetSiblingIndex(aiPanel.transform.GetSiblingIndex() + 1);

            var ui = copy.AddComponent<ReservedSlotsUI>();
            var buttons = copy.GetComponentsInChildren<Button>(true).OrderBy(b => b.transform.position.x).ToArray();
            if (buttons.Length >= 2)
            {
                ui._decButton = buttons[0];
                ui._incButton = buttons[buttons.Length - 1];
            }
            ui._label = copy.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.GetComponentInParent<Button>() == null);
            copy.SetActive(true);
            return ui;
        }

        private void OnEnable()
        {
            _lobby = LobbyLocator.GetLobbyManager();
            if (_lobby == null) return;
            _lobby.ReservedSlots.OnChange += OnCountChanged;
            _lobby.AiSlotCount.OnChange += OnCountChanged;
            _lobby.PlayerReadyStates.OnChange += OnPlayersChanged;
            if (_decButton != null) { _decButton.onClick.RemoveAllListeners(); _decButton.onClick.AddListener(() => Change(-1)); }
            if (_incButton != null) { _incButton.onClick.RemoveAllListeners(); _incButton.onClick.AddListener(() => Change(+1)); }
            Refresh();
        }

        private void OnDisable()
        {
            if (_lobby == null) return;
            _lobby.ReservedSlots.OnChange -= OnCountChanged;
            _lobby.AiSlotCount.OnChange -= OnCountChanged;
            _lobby.PlayerReadyStates.OnChange -= OnPlayersChanged;
        }

        private void Update()
        {
            if (_lobby != null || LobbyLocator.GetLobbyManager() == null) return;
            OnDisable();
            OnEnable();
        }

        private void OnCountChanged(int prev, int next, bool asServer) => Refresh();

        private void OnPlayersChanged(FishNet.Object.Synchronizing.SyncDictionaryOperation op, int key, bool value, bool asServer) => Refresh();

        private void Change(int delta)
        {
            if (_lobby == null || !InstanceFinder.IsServerStarted) return;
            _lobby.SetReservedSlots(_lobby.ReservedSlots.Value + delta);
        }

        private void Refresh()
        {
            if (_lobby == null) return;
            bool isHost = InstanceFinder.IsServerStarted;
            if (_label != null) _label.text = LocalizationManager.Localize("MP.Room.ReservedCount", _lobby.ReservedSlots.Value);
            if (_decButton != null)
            {
                _decButton.gameObject.SetActive(isHost);
                _decButton.interactable = _lobby.ReservedSlots.Value > 0;
            }
            if (_incButton != null)
            {
                _incButton.gameObject.SetActive(isHost);
                _incButton.interactable = _lobby.FreeSlotCount > 0;
            }
        }
    }
}
