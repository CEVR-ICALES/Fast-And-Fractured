using Assets.SimpleLocalization.Scripts;
using FishNet;
using Enums;
using FastAndFractured;
using FastAndFractured.Multiplayer;
using FastAndFractured.Multiplayer.Abstractions;
using FastAndFractured.Multiplayer.Lobby;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Player list, ready state and countdown of the LOBBY_ROOM screen; forwards the local character pick.</summary>
    public class LobbyReadyUI : MonoBehaviour
    {
        [Header("Ready Button")]
        [SerializeField] private Button    _readyButton;
        [SerializeField] private TMP_Text  _readyButtonText;
        [SerializeField] private Image     _readyButtonImage;
        // Orange reads as a button to press, green as a confirmed state.
        [SerializeField] private Color     _notReadyColor = new Color(0.90f, 0.50f, 0.10f, 1f); // orange (act)
        [SerializeField] private Color     _readyColor    = new Color(0.15f, 0.65f, 0.25f, 1f); // green  (set)

        [Header("Player List")]
        [SerializeField] private Transform  _playerListContainer;
        [SerializeField] private GameObject _playerEntryPrefab;

        [Header("Countdown")]
        [SerializeField] private TMP_Text   _countdownText;
        [SerializeField] private GameObject _countdownPanel;

        [Header("Host Controls")]
        [SerializeField] private Button _forceStartButton;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;
        [SerializeField] private float  _fadeDuration = 0.5f;

        [Header("Room")]
        [SerializeField] private TMP_Text _roomNameLabel;

        [Header("Map Vote Animation")]
        [SerializeField] private FastAndFractured.Multiplayer.Lobby.MapVoteAnimationUI _mapVoteAnimation;

        private NetworkedLobbyManager _lobbyManager;
        private bool _subscribed;
        private bool _isReady;
        private int _lateSubscribeRetries;
        private const int MAX_LATE_SUBSCRIBE_RETRIES = 20;
        private const float LATE_SUBSCRIBE_INTERVAL = 0.5f;
        private const float SCREEN_TRANSITION_SECONDS = 0.5f;
        private const float BANNER_FONT_SIZE = 28f;
        private const float WATCH_FONT_SIZE = 24f;
        private const float RESET_WINS_GAP = 8f;
        private const float RESET_WINS_HEIGHT_RATIO = 0.55f;
        private static readonly Vector2 DEFAULT_SLOT_SIZE = new Vector2(400f, 120f);
        private static readonly Vector2 SLOT_SPACING = new Vector2(12f, 12f);
        private static readonly Color BANNER_COLOR = new Color(0.96f, 0.69f, 0.25f, 0.92f);
        private static readonly Color BANNER_TEXT_COLOR = new Color(0.12f, 0.08f, 0.04f, 1f);
        private static readonly Color WATCH_BUTTON_COLOR = new Color(0.12f, 0.08f, 0.04f, 0.92f);
        private static readonly Color WATCH_TEXT_COLOR = new Color(0.96f, 0.69f, 0.25f, 1f);
        private readonly Dictionary<int, GameObject> _playerEntries = new Dictionary<int, GameObject>();

        private void Awake()
        {
            ArrangePlayerListAsGrid();
        }

        /// <summary>Lays the player slots out as a wrapping grid (eight players overflowed a vertical stack).</summary>
        private void ArrangePlayerListAsGrid()
        {
            if (_playerListContainer == null) return;
            var vertical = _playerListContainer.GetComponent<VerticalLayoutGroup>();
            if (vertical != null) DestroyImmediate(vertical);

            var grid = _playerListContainer.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = _playerListContainer.gameObject.AddComponent<GridLayoutGroup>();
            var entryRect = _playerEntryPrefab != null ? _playerEntryPrefab.GetComponent<RectTransform>() : null;
            grid.cellSize = entryRect != null && entryRect.sizeDelta.x > 0f && entryRect.sizeDelta.y > 0f
                ? entryRect.sizeDelta : DEFAULT_SLOT_SIZE;
            grid.spacing = SLOT_SPACING;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
        }

        private void OnEnable()
        {
            _lateSubscribeRetries = 0;
            _lobbyManager = LobbyLocator.GetLobbyManager();
            if (_lobbyManager == null)
            {
                Invoke(nameof(TryLateSubscribe), LATE_SUBSCRIBE_INTERVAL);
                return;
            }
            Subscribe();
        }

        private void TryLateSubscribe()
        {
            _lobbyManager = LobbyLocator.GetLobbyManager();
            if (_lobbyManager != null)
            {
                Subscribe();
                return;
            }
            if (++_lateSubscribeRetries < MAX_LATE_SUBSCRIBE_RETRIES)
                Invoke(nameof(TryLateSubscribe), LATE_SUBSCRIBE_INTERVAL);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(TryLateSubscribe));
            Unsubscribe();
        }

        private void Subscribe()
        {
            _subscribed = true;
            _lobbyManager.OnPlayerReadyChanged     += HandlePlayerReadyChanged;
            _lobbyManager.OnPlayerCharacterChanged += HandlePlayerCharacterChanged;
            _lobbyManager.OnPlayerNameChanged      += HandlePlayerNameChanged;
            _lobbyManager.OnPlayerLevelChanged     += HandlePlayerLevelChanged;
            MultiplayerSettings.Changed            += RefreshPlayerList;
            _lobbyManager.OnPlayerMapVoteChanged   += HandlePlayerMapVoteChanged;
            _lobbyManager.OnCountdownStarted       += HandleCountdownStarted;
            _lobbyManager.OnGameStarting           += HandleGameStarting;
            _lobbyManager.PlayerReadyStates.OnChange        += HandleReadyStatesSync;
            _lobbyManager.PlayerCharacterSelections.OnChange += HandleCharacterSelectionsSync;
            _lobbyManager.PlayerNames.OnChange               += HandlePlayerNamesSync;
            _lobbyManager.PlayerMapVotes.OnChange            += HandleMapVotesSync;
            _lobbyManager.CurrentCountdown.OnChange         += HandleCountdownSync;
            _lobbyManager.RoomName.OnChange                 += HandleRoomNameSync;
            _lobbyManager.AiSlotCount.OnChange              += HandleAiSlotCountSync;
            _lobbyManager.OnMapVoteAnimationRequested       += HandleMapVoteAnimation;
            _lobbyManager.GameStarted.OnChange              += HandleGameStartedSync;
            _lobbyManager.MatchParticipants.OnChange        += HandleParticipantsSync;
            _lobbyManager.PlayerWins.OnChange               += HandleWinsSync;
            _lobbyManager.ReservedSlots.OnChange            += HandleReservedSync;
            EnsureReservedSlotsPanel();
            UpdateRoomName(_lobbyManager.RoomName.Value);

            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveAllListeners();
                _readyButton.onClick.AddListener(OnReadyClicked);
                UpdateReadyButtonVisual();
            }

            if (_forceStartButton != null)
            {
                _forceStartButton.onClick.RemoveAllListeners();
                _forceStartButton.onClick.AddListener(OnForceStartClicked);
                bool isHost = InstanceFinder.IsServerStarted;
                _forceStartButton.gameObject.SetActive(isHost);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(OnBackClicked);
            }

            if (_countdownPanel != null)
                _countdownPanel.SetActive(false);

            RefreshPlayerList();
            RefreshMatchState();
            RefreshResetWinsButton();
            HideMapPickWhenIdle();
        }

        private void Unsubscribe()
        {
            _subscribed = false;
            if (_lobbyManager == null) return;
            _lobbyManager.OnPlayerReadyChanged     -= HandlePlayerReadyChanged;
            _lobbyManager.OnPlayerCharacterChanged -= HandlePlayerCharacterChanged;
            _lobbyManager.OnPlayerNameChanged      -= HandlePlayerNameChanged;
            _lobbyManager.OnPlayerLevelChanged     -= HandlePlayerLevelChanged;
            MultiplayerSettings.Changed            -= RefreshPlayerList;
            _lobbyManager.OnPlayerMapVoteChanged   -= HandlePlayerMapVoteChanged;
            _lobbyManager.OnCountdownStarted       -= HandleCountdownStarted;
            _lobbyManager.OnGameStarting           -= HandleGameStarting;
            _lobbyManager.PlayerReadyStates.OnChange        -= HandleReadyStatesSync;
            _lobbyManager.PlayerCharacterSelections.OnChange -= HandleCharacterSelectionsSync;
            _lobbyManager.PlayerNames.OnChange               -= HandlePlayerNamesSync;
            _lobbyManager.PlayerMapVotes.OnChange            -= HandleMapVotesSync;
            _lobbyManager.CurrentCountdown.OnChange         -= HandleCountdownSync;
            _lobbyManager.RoomName.OnChange                 -= HandleRoomNameSync;
            _lobbyManager.AiSlotCount.OnChange              -= HandleAiSlotCountSync;
            _lobbyManager.OnMapVoteAnimationRequested       -= HandleMapVoteAnimation;
            _lobbyManager.GameStarted.OnChange              -= HandleGameStartedSync;
            _lobbyManager.MatchParticipants.OnChange        -= HandleParticipantsSync;
            _lobbyManager.PlayerWins.OnChange               -= HandleWinsSync;
            _lobbyManager.ReservedSlots.OnChange            -= HandleReservedSync;
        }

        private void HandleReservedSync(int prev, int next, bool asServer)
        {
            if (asServer && InstanceFinder.IsClientStarted) return;   // the host hears it again as client
            RefreshPlayerList();
        }

        private ReservedSlotsUI _reservedPanel;

        private void EnsureReservedSlotsPanel()
        {
            if (_reservedPanel != null) return;
            var screen = GetComponentInParent<MenuScreen>(true);
            var aiPanel = screen != null ? screen.GetComponentInChildren<AiSlotsUI>(true) : GetComponentInChildren<AiSlotsUI>(true);
            if (aiPanel != null) _reservedPanel = ReservedSlotsUI.CreateBelow(aiPanel);
        }

        private void HandleWinsSync(FishNet.Object.Synchronizing.SyncDictionaryOperation op, int key, int value, bool asServer)
        {
            if (asServer && InstanceFinder.IsClientStarted) return;   // the host hears it again as client
            RefreshPlayerList();
            RefreshResetWinsButton();
        }

        private Button _resetWinsButton;

        private void EnsureResetWinsButton()
        {
            if (_resetWinsButton != null || _forceStartButton == null) return;
            var go = Instantiate(_forceStartButton.gameObject, _forceStartButton.transform.parent);
            go.name = "ResetWinsButton";
            go.transform.SetSiblingIndex(_forceStartButton.transform.GetSiblingIndex());
            var rect = (RectTransform)go.transform;
            var source = (RectTransform)_forceStartButton.transform;
            if (source.parent.GetComponent<LayoutGroup>() == null)
            {
                float height = source.rect.height * RESET_WINS_HEIGHT_RATIO;
                rect.sizeDelta = new Vector2(source.sizeDelta.x, source.sizeDelta.y - (source.rect.height - height));
                float offset = source.rect.height * (1f - source.pivot.y) + height * source.pivot.y + RESET_WINS_GAP;
                rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, offset);
            }
            foreach (var localized in go.GetComponentsInChildren<Assets.SimpleLocalization.Scripts.LocalizedText>(true)) DestroyImmediate(localized);
            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = LocalizationManager.Localize("MP.Room.ResetWins");
            _resetWinsButton = go.GetComponent<Button>();
            _resetWinsButton.onClick.RemoveAllListeners();
            _resetWinsButton.onClick.AddListener(OnResetWinsClicked);
        }

        private void RefreshResetWinsButton()
        {
            bool isHost = InstanceFinder.IsServerStarted;
            if (isHost) EnsureResetWinsButton();
            if (_resetWinsButton == null) return;
            _resetWinsButton.gameObject.SetActive(isHost);
            _resetWinsButton.interactable = _lobbyManager != null && !_lobbyManager.GameStarted.Value && _lobbyManager.AnyWins;
        }

        private void OnResetWinsClicked()
        {
            if (_lobbyManager == null || !InstanceFinder.IsServerStarted) return;
            _lobbyManager.ResetWins();
        }

        private RoomJoiningOverlay _joiningOverlay;

        // A client sees the room before the host has registered it: without this the screen looks frozen.
        private void RefreshJoiningOverlay()
        {
            if (_playerListContainer == null) return;
            int localId = InstanceFinder.ClientManager?.Connection?.ClientId ?? -1;
            bool joining = IsJoining(InstanceFinder.IsServerStarted, InstanceFinder.IsClientStarted,
                _lobbyManager != null && localId >= 0 && _lobbyManager.PlayerReadyStates.ContainsKey(localId));
            if (_joiningOverlay == null)
            {
                if (!joining) return;
                var area = _playerListContainer.parent as RectTransform ?? (RectTransform)_playerListContainer;
                _joiningOverlay = RoomJoiningOverlay.Create(area, _roomNameLabel);
            }
            _joiningOverlay.Show(joining);
        }

        internal static bool IsJoining(bool isHost, bool isClient, bool registered) => !isHost && isClient && !registered;

        /// <summary>A host migration spawns a new room object: bind to it as soon as it appears.</summary>
        private void Update()
        {
            RefreshJoiningOverlay();
            if (!_subscribed || _lobbyManager != null) return;
            _subscribed = false;
            foreach (var entry in _playerEntries.Values)
                if (entry != null) Destroy(entry);
            _playerEntries.Clear();
            _isReady = false;
            _lateSubscribeRetries = 0;
            TryLateSubscribe();
        }

        private GameObject _matchBanner;

        private void HandleGameStartedSync(bool prev, bool next, bool asServer)
        {
            if (asServer) return;   // the host gets the client-side call as well
            if (prev && !next)
            {
                // The match is over and the room starts over: nobody is ready any more.
                _isReady = false;
                UpdateReadyButtonVisual();
                if (_countdownPanel != null) _countdownPanel.SetActive(false);
                HideMapPickWhenIdle();
            }
            RefreshMatchState();
            RefreshPlayerList();
        }

        private void HandleParticipantsSync(FishNet.Object.Synchronizing.SyncListOperation op, int index, int prev, int next, bool asServer)
        {
            if (asServer) return;
            RefreshMatchState();
            RefreshPlayerList();
        }

        /// <summary>While the room plays a match, players waiting in it see a banner and the ready/start buttons rest.</summary>
        private void RefreshMatchState()
        {
            if (_lobbyManager == null) return;
            bool running = _lobbyManager.GameStarted.Value;
            int localId = InstanceFinder.ClientManager?.Connection?.ClientId ?? -1;
            bool waiting = running && !_lobbyManager.IsInMatch(localId);

            if (_readyButton != null) _readyButton.interactable = !running;
            if (_forceStartButton != null) _forceStartButton.interactable = !running;
            RefreshResetWinsButton();
            if (waiting && _readyButtonText != null) _readyButtonText.text = LocalizationManager.Localize("MP.Room.InProgress");
            else if (!running) UpdateReadyButtonVisual();

            if (waiting && _matchBanner == null) _matchBanner = BuildMatchBanner();
            if (_matchBanner != null) _matchBanner.SetActive(waiting);
        }

        private GameObject BuildMatchBanner()
        {
            var go = new GameObject("MatchInProgressBanner", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -128f);
            rect.sizeDelta = new Vector2(1100f, 56f);
            var background = go.AddComponent<Image>();
            background.color = BANNER_COLOR;
            background.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-200f, 0f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (_roomNameLabel != null) text.font = _roomNameLabel.font;
            text.fontSize = BANNER_FONT_SIZE;
            text.color = BANNER_TEXT_COLOR;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = LocalizationManager.Localize("MP.Room.InProgressNext");

            // Watching loads the map for this client only.
            var watchGo = new GameObject("WatchButton", typeof(RectTransform));
            var watchRect = (RectTransform)watchGo.transform;
            watchRect.SetParent(rect, false);
            watchRect.anchorMin = watchRect.anchorMax = watchRect.pivot = new Vector2(1f, 0.5f);
            watchRect.anchoredPosition = new Vector2(-8f, 0f);
            watchRect.sizeDelta = new Vector2(176f, 44f);
            var watchImage = watchGo.AddComponent<Image>();
            watchImage.color = WATCH_BUTTON_COLOR;
            var watch = watchGo.AddComponent<Button>();
            watch.targetGraphic = watchImage;
            watch.onClick.AddListener(OnWatchClicked);
            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(watchRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (_roomNameLabel != null) label.font = _roomNameLabel.font;
            label.fontSize = WATCH_FONT_SIZE;
            label.color = WATCH_TEXT_COLOR;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = LocalizationManager.Localize("MP.Room.Watch");
            return go;
        }

        private void OnWatchClicked()
        {
            if (_lobbyManager == null || !_lobbyManager.GameStarted.Value) return;
            _lobbyManager.ServerSpectateMatch();
        }

        private void HandleAiSlotCountSync(int prev, int next, bool asServer) => RefreshPlayerList();

        private FastAndFractured.Multiplayer.Lobby.MapVoteAnimationUI MapPickAnimation =>
            _mapVoteAnimation != null
                ? _mapVoteAnimation
                : GetComponentInChildren<FastAndFractured.Multiplayer.Lobby.MapVoteAnimationUI>(true);

        /// <summary>Every client: the server resolved the map vote and loads the map once this animation ends.</summary>
        private void HandleMapVoteAnimation(int[] candidates, int[] votes, int[] hops, bool random)
        {
            var anim = MapPickAnimation;
            if (anim == null)
            {
                Debug.LogWarning("[LobbyReadyUI] OnMapVoteAnimationRequested but no MapVoteAnimationUI found in scene.");
                return;
            }
            anim.Play(candidates, random ? null : votes, hops, random);
        }

        /// <summary>The pick stays on screen until the curtain covers it; back in the room, it goes.</summary>
        private void HideMapPickWhenIdle()
        {
            if (_lobbyManager == null || _lobbyManager.GameStarted.Value) return;
            MapPickAnimation?.Hide();
        }

        private void HandleRoomNameSync(string prev, string next, bool asServer) => UpdateRoomName(next);

        private void UpdateRoomName(string name)
        {
            if (_roomNameLabel != null)
                _roomNameLabel.text = string.IsNullOrEmpty(name) ? LocalizationManager.Localize("MP.Room.Room") : name;
        }

        #region UI_CALLBACKS
        private void OnReadyClicked()
        {
            if (_lobbyManager == null || _lobbyManager.GameStarted.Value) return;
            _isReady = !_isReady;
            _lobbyManager.ServerSetReady(_isReady);
            UpdateReadyButtonVisual();
        }

        private void OnForceStartClicked() =>
            _lobbyManager?.ServerForceStart();

        private async void OnBackClicked()
        {
            // An intentional stop: HostDisconnectWatchdog must not report the host as lost.
            HostDisconnectWatchdog.NotifyIntentionalLeave();

            try { await LobbyServiceLocator.Transport.LeaveLobbyAsync(); }
            catch { /* best-effort */ }

            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, _fadeDuration);
        }
        #endregion

        #region LOBBY_CALLBACKS
        private void HandlePlayerReadyChanged(int clientId, bool isReady)    => RefreshPlayerList();
        private void HandlePlayerCharacterChanged(int clientId, string code) => RefreshPlayerList();
        private void HandlePlayerNameChanged(int clientId, string name)      => RefreshPlayerList();
        private void HandlePlayerLevelChanged(int clientId, int level)       => RefreshPlayerList();
        private void HandlePlayerMapVoteChanged(int clientId, int mapId)     => RefreshPlayerList();

        private void HandleCountdownStarted(int seconds)
        {
            if (_countdownPanel != null) _countdownPanel.SetActive(true);
            UpdateCountdownText(seconds);
        }

        private void HandleGameStarting()
        {
            if (_readyButton     != null) _readyButton.interactable     = false;
            if (_forceStartButton != null) _forceStartButton.interactable = false;
            if (_countdownText   != null) _countdownText.text            = LocalizationManager.Localize("MP.Room.Starting");
        }

        private void HandleReadyStatesSync(FishNet.Object.Synchronizing.SyncDictionaryOperation op, int key, bool value, bool asServer)
            => RefreshPlayerList();

        private void HandleCharacterSelectionsSync(FishNet.Object.Synchronizing.SyncDictionaryOperation op, int key, string value, bool asServer)
            => RefreshPlayerList();

        private void HandlePlayerNamesSync(FishNet.Object.Synchronizing.SyncDictionaryOperation op, int key, string value, bool asServer)
            => RefreshPlayerList();

        private void HandleMapVotesSync(FishNet.Object.Synchronizing.SyncDictionaryOperation op, int key, int value, bool asServer)
            => RefreshPlayerList();

        private void HandleCountdownSync(int prev, int next, bool asServer)
        {
            if (next >= 0)
            {
                if (_countdownPanel != null && !_countdownPanel.activeSelf)
                    _countdownPanel.SetActive(true);
                UpdateCountdownText(next);
            }
            else
            {
                if (_countdownPanel != null)
                    _countdownPanel.SetActive(false);
            }
        }
        #endregion

        #region UI_UPDATES
        private void UpdateReadyButtonVisual()
        {
            if (_readyButtonText  != null) _readyButtonText.text   = _isReady ? LocalizationManager.Localize("MP.Room.ReadyButton") : LocalizationManager.Localize("MP.Room.NotReadyButton");
            if (_readyButtonImage != null) _readyButtonImage.color = _isReady ? _readyColor : _notReadyColor;
        }

        private void UpdateCountdownText(int seconds)
        {
            if (_countdownText == null) return;
            _countdownText.text = seconds > 0 ? LocalizationManager.Localize("MP.Room.StartingIn", seconds) : LocalizationManager.Localize("MP.Room.Go");
        }


        private const int AI_SLOT_ID_BASE = -1000;
        private const int RESERVED_SLOT_ID_BASE = -2000;

        // Kick and reserve act on the server directly: only the host has those controls.
        private static bool CanKick => InstanceFinder.IsServerStarted;

        private void RefreshPlayerList()
        {
            if (_lobbyManager == null || _playerListContainer == null || _playerEntryPrefab == null) return;

            int  localClientId = InstanceFinder.ClientManager?.Connection?.ClientId ?? -1;
            var  catalog       = MapCatalogLoader.Get();
            var  currentPlayers = new HashSet<int>();

            foreach (var kvp in _lobbyManager.PlayerReadyStates)
            {
                int    clientId = kvp.Key;
                bool   isReady  = kvp.Value;
                string code     = _lobbyManager.GetPlayerCharacter(clientId);
                currentPlayers.Add(clientId);

                if (!_playerEntries.TryGetValue(clientId, out var entry) || entry == null)
                {
                    entry = Instantiate(_playerEntryPrefab, _playerListContainer);
                    _playerEntries[clientId] = entry;
                }

                Sprite voteSprite = null;
                if (catalog != null &&
                    _lobbyManager.PlayerMapVotes.TryGetValue(clientId, out int mapId) &&
                    mapId >= 0 &&
                    catalog.TryGetById(mapId, out var entryDef))
                {
                    voteSprite = entryDef.Thumbnail;
                }

                bool isLocal = clientId == localClientId;

                // The reused singleplayer character/HUD UI reads "Selected_Player": keep it on the lobby pick.
                if (isLocal && !string.IsNullOrEmpty(code))
                    PlayerPrefs.SetString("Selected_Player", code);

                string displayName = _lobbyManager.GetPlayerName(clientId);

                var slot = entry.GetComponent<PlayerSlotUI>();
                if (slot != null)
                {
                    slot.SetData(clientId, code, isReady, voteSprite, isLocal, displayName,
                        MultiplayerSettings.ShowPlayerLevels ? _lobbyManager.GetPlayerLevel(clientId) : 0,
                        _lobbyManager.GetPlayerWins(clientId));
                    slot.SetInMatch(_lobbyManager.IsInMatch(clientId));
                    int kickedId = clientId;
                    slot.SetKick(CanKick && !isLocal && !_lobbyManager.IsInMatch(clientId)
                        ? () => _lobbyManager.KickPlayer(kickedId)
                        : (System.Action)null);
                    slot.SetMakeHost(!isLocal && HostMigration.RoomMigration.CanHandOverTo(clientId)
                        ? () => HostMigration.RoomMigration.HandOverTo(kickedId)
                        : (System.Action)null);
                    var btn = slot.GetSelectButton();
                    if (btn != null && isLocal)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(OnSelectCharacterClicked);
                    }
                }
                else
                {
                    // Legacy text-only entry prefab.
                    var label = entry.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                    {
                        string charDisplay = string.IsNullOrEmpty(code) ? "?" : code;
                        label.text = $"{displayName}: {charDisplay} {(isReady ? "[READY]" : "[...]")}";
                    }
                    var img = entry.GetComponent<Image>();
                    if (img != null) img.color = isReady ? _readyColor : _notReadyColor;
                }
            }

            // AI slots: negative ids, names from a pool indexed by bot number so every peer shows the same.
            int aiCount = Mathf.Min(_lobbyManager.AiSlotCount.Value, MatchNames.BotNames.Length);
            for (int i = 0; i < aiCount; i++)
            {
                int slotId = AI_SLOT_ID_BASE - i;
                currentPlayers.Add(slotId);

                if (!_playerEntries.TryGetValue(slotId, out var aiEntry) || aiEntry == null)
                {
                    aiEntry = Instantiate(_playerEntryPrefab, _playerListContainer);
                    _playerEntries[slotId] = aiEntry;
                }

                string aiName = MatchNames.BotName(i);
                var aiSlot = aiEntry.GetComponent<PlayerSlotUI>();
                if (aiSlot != null)
                {
                    aiSlot.SetData(slotId, aiName, isReady: true, mapVoteThumbnail: null, isLocal: false);
                    // Bots have no identity of their own: removing any one drops the AI count by one.
                    aiSlot.SetKick(CanKick && !_lobbyManager.GameStarted.Value
                        ? () => _lobbyManager.ServerSetAiSlotCount(_lobbyManager.AiSlotCount.Value - 1)
                        : (System.Action)null);
                }
                else
                {
                    var label = aiEntry.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = LocalizationManager.Localize("MP.Room.AiReady", aiName);
                }
            }

            for (int i = 0; i < _lobbyManager.ReservedSlots.Value; i++)
            {
                int slotId = RESERVED_SLOT_ID_BASE - i;
                currentPlayers.Add(slotId);
                if (!_playerEntries.TryGetValue(slotId, out var reservedEntry) || reservedEntry == null)
                {
                    reservedEntry = Instantiate(_playerEntryPrefab, _playerListContainer);
                    _playerEntries[slotId] = reservedEntry;
                }
                reservedEntry.transform.SetAsLastSibling();
                var reservedSlot = reservedEntry.GetComponent<PlayerSlotUI>();
                if (reservedSlot == null) continue;
                reservedSlot.SetData(slotId, null, isReady: false, mapVoteThumbnail: null, isLocal: false,
                    displayName: LocalizationManager.Localize("MP.Room.ReservedSeat"));
                reservedSlot.SetReserved();
                reservedSlot.SetKick(CanKick ? () => _lobbyManager.SetReservedSlots(_lobbyManager.ReservedSlots.Value - 1) : (System.Action)null);
            }

            var toRemove = new List<int>();
            foreach (var kvp in _playerEntries)
            {
                if (!currentPlayers.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                    if (kvp.Value != null) Destroy(kvp.Value);
                }
            }
            foreach (int id in toRemove)
                _playerEntries.Remove(id);
        }

        /// <summary>
        /// The local player's SELECT CHARACTER &amp; MAP button: routes through the character and map screens back to
        /// LOBBY_ROOM. The character camera is activated by hand, as CharacterScreen may have no screenCamera.
        /// </summary>
        private void OnSelectCharacterClicked()
        {
            MultiplayerMenuRouter.BeginLobbyFlow();

            ForceActivateCameraByName("CharSlectionCamera", "CharSelectionCamera", "CharacterSelectionCamera");

            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.CHARACTER_SELECTION, SCREEN_TRANSITION_SECONDS);
        }

        /// <summary>Activates the first scene GameObject with one of the names (inactive ones included).</summary>
        private static void ForceActivateCameraByName(params string[] candidates)
        {
            foreach (string name in candidates)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t == null) continue;
                    if (t.hideFlags != HideFlags.None) continue;
                    if (!t.gameObject.scene.IsValid()) continue;   // skip prefab assets
                    if (t.name != name) continue;
                    t.gameObject.SetActive(true);
                    Debug.Log($"[LobbyReadyUI] Force-activated camera GameObject '{t.name}'.");
                    return;
                }
            }
            Debug.LogWarning($"[LobbyReadyUI] None of these camera GameObjects found in scene: {string.Join(", ", candidates)}");
        }
        #endregion
    }
}
