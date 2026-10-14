// SCENE SETUP:
//   - Place anywhere inside the LOBBY_ROOM MenuScreen.
//   - Wire _mapCardContainer (Transform, horizontal/grid layout)
//   - Wire _mapCardPrefab    (MapVoteCard prefab — Image for thumbnail + TMP_Text for name + Button)
//   - Wire _voteCountLabel   (TMP_Text, e.g. "3 votes cast")
//   - Assign _catalog        (MapCatalog ScriptableObject from Resources/MapCatalog)
using Assets.SimpleLocalization.Scripts;
using System.Collections.Generic;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class MapVoteUI : MonoBehaviour
    {
        [SerializeField] private Transform  _mapCardContainer;
        [SerializeField] private GameObject _mapCardPrefab;
        [SerializeField] private TMP_Text   _voteCountLabel;

        private NetworkedLobbyManager _lobby;
        private readonly List<(int mapId, Button button, Image highlight)> _cards = new();
        private int _localVote = -1;

        private void OnEnable()
        {
            _lobby = LobbyLocator.GetLobbyManager();
            if (_lobby == null) return;

            BuildCards();
            _lobby.PlayerMapVotes.OnChange += OnVotesChanged;
            RefreshVoteDisplay();
        }

        private void OnDisable()
        {
            if (_lobby != null)
                _lobby.PlayerMapVotes.OnChange -= OnVotesChanged;
        }

        private void BuildCards()
        {
            foreach (Transform child in _mapCardContainer)
                Destroy(child.gameObject);
            _cards.Clear();

            var catalog = MapCatalogLoader.Get();
            if (catalog == null) return;

            foreach (var entry in catalog.Entries)
            {
                var card    = Instantiate(_mapCardPrefab, _mapCardContainer);
                var img     = card.GetComponentInChildren<Image>();
                var label   = card.GetComponentInChildren<TMP_Text>();
                var btn     = card.GetComponentInChildren<Button>();
                var outline = card.GetComponent<Outline>() ?? card.GetComponentInChildren<Outline>();

                if (img   != null && entry.Thumbnail != null) img.sprite = entry.Thumbnail;
                if (label != null) label.text = entry.DisplayNameKey;

                int mapId = entry.Id;
                btn?.onClick.AddListener(() => CastVote(mapId));

                _cards.Add((mapId, btn, img));
            }
        }

        private void CastVote(int mapId)
        {
            if (_lobby == null) return;
            _localVote = mapId;
            _lobby.ServerVoteMap(mapId);
            RefreshVoteDisplay();
        }

        private void OnVotesChanged(SyncDictionaryOperation op, int clientId, int mapId, bool asServer)
            => RefreshVoteDisplay();

        private void RefreshVoteDisplay()
        {
            if (_lobby == null) return;

            var tally = new Dictionary<int, int>();
            foreach (var kvp in _lobby.PlayerMapVotes)
            {
                if (!tally.ContainsKey(kvp.Value)) tally[kvp.Value] = 0;
                tally[kvp.Value]++;
            }

            if (_voteCountLabel != null)
                _voteCountLabel.text = LocalizationManager.Localize("MP.Room.VotesCast", _lobby.PlayerMapVotes.Count);

            foreach (var (mapId, btn, img) in _cards)
            {
                if (btn == null) continue;
                bool isMyVote = _localVote == mapId;
                var colors = btn.colors;
                colors.normalColor = isMyVote ? new Color(0.2f, 0.8f, 0.2f, 1f) : Color.white;
                btn.colors = colors;

                // Show vote count via button label if present
                var countLabel = btn.GetComponentInChildren<TMP_Text>();
                if (countLabel != null)
                {
                    int count = tally.TryGetValue(mapId, out int c) ? c : 0;
                    countLabel.text = count > 0 ? count.ToString() : string.Empty;
                }
            }
        }
    }
}
