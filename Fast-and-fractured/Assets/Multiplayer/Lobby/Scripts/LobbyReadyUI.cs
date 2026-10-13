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
    /// <summary>
    /// Drives the player-list + ready/countdown section of the LOBBY_ROOM screen.
    /// Character selection is handled by the existing CharacterSelectorManager;
    /// this component subscribes to OnCharacterCodeChanged and forwards to the lobby manager.
    /// </summary>
    public class LobbyReadyUI : MonoBehaviour
    {
        [Header("Ready Button")]
        [SerializeField] private Button    _readyButton;
        [SerializeField] private TMP_Text  _readyButtonText;
        [SerializeField] private Image     _readyButtonImage;
        // Default colors are tuned for legibility: orange = call-to-action ("act now"),
        // saturated green = confirmed state. Plain red/green felt ambiguous because both look
        // like status labels rather than a button you can press.
        [SerializeField] private Color     _notReadyColor = new Color(0.90f, 0.50f, 0.10f, 1f); // orange (act)
        [SerializeField] private Color     _readyColor    = new Color(0.15f, 0.65f, 0.25f, 1f); // green  (set)
        [SerializeField] private string    _notReadyLabel = "PRESS TO READY UP";
        [SerializeField] private string    _readyLabel    = "READY  ·  CLICK TO CANCEL";

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
        private const int MAX_LATE_SUBSCRIBE_RETRIES = 20; // 10 seconds @ 0.5s interval
        private readonly Dictionary<int, GameObject> _playerEntries = new Dictionary<int, GameObject>();

        private void Awake()
        {
            ArrangePlayerListAsGrid();
        }

        /// <summary>
        /// The player list was a vertical stack: with 8 players (humans + AI) the entries ran off
        /// the panel (playtest feedback 2026-09-30). Lay the slots out as a grid that wraps by the
        /// panel's width (4 per row at 1920x1080).
        /// </summary>
        private void ArrangePlayerListAsGrid()
        {
            if (_playerListContainer == null) return;
            var vertical = _playerListContainer.GetComponent<VerticalLayoutGroup>();
            if (vertical != null) DestroyImmediate(vertical);

            var grid = _playerListContainer.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = _playerListContainer.gameObject.AddComponent<GridLayoutGroup>();
            var entryRect = _playerEntryPrefab != null ? _playerEntryPrefab.GetComponent<RectTransform>() : null;
            grid.cellSize = entryRect != null && entryRect.sizeDelta.x > 0f && entryRect.sizeDelta.y > 0f
                ? entryRect.sizeDelta : new Vector2(400f, 120f);
            grid.spacing = new Vector2(12f, 12f);
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
                Invoke(nameof(TryLateSubscribe), 0.5f);
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
                Invoke(nameof(TryLateSubscribe), 0.5f);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(TryLateSubscribe));
            Unsubscribe();

            if (CharacterSelectorManager.HasInstance)
                CharacterSelectorManager.Instance.OnCharacterCodeChanged -= OnLocalCharacterChanged;
        }

        private void Subscribe()
        {
            _subscribed = true;
            _lobbyManager.OnPlayerReadyChanged     += HandlePlayerReadyChanged;
            _lobbyManager.OnPlayerCharacterChanged += HandlePlayerCharacterChanged;
            _lobbyManager.OnPlayerNameChanged      += HandlePlayerNameChanged;
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
            UpdateRoomName(_lobbyManager.RoomName.Value);

            // Forward local character selection to the server
            if (CharacterSelectorManager.HasInstance)
            {
                CharacterSelectorManager.Instance.OnCharacterCodeChanged += OnLocalCharacterChanged;
                // Send current selection immediately
                string current = CharacterSelectorManager.Instance.CurrentCharacterCode;
                if (!string.IsNullOrEmpty(current))
                    _lobbyManager.ServerSelectCharacter(current);
            }

            // Initial UI setup
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
        }

        private void Unsubscribe()
        {
            _subscribed = false;
            if (_lobbyManager == null) return;
            _lobbyManager.OnPlayerReadyChanged     -= HandlePlayerReadyChanged;
            _lobbyManager.OnPlayerCharacterChanged -= HandlePlayerCharacterChanged;
            _lobbyManager.OnPlayerNameChanged      -= HandlePlayerNameChanged;
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
        }

        /// <summary>
        /// The room is a new object after a host migration (the new host spawns its own):
        /// bind to it as soon as it appears.
        /// </summary>
        private void Update()
        {
            if (!_subscribed || _lobbyManager != null) return;
            _subscribed = false;
            foreach (var entry in _playerEntries.Values)
                if (entry != null) Destroy(entry);
            _playerEntries.Clear();
            _isReady = false;
            _lateSubscribeRetries = 0;
            TryLateSubscribe();
        }

        // ── Match in progress ────────────────────────────────────────────────────────
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

        /// <summary>
        /// While the room plays a match, players in the room (joined mid-match, or left it) wait
        /// for the next one: a banner says so and the ready / start buttons rest.
        /// </summary>
        private void RefreshMatchState()
        {
            if (_lobbyManager == null) return;
            bool running = _lobbyManager.GameStarted.Value;
            int localId = InstanceFinder.ClientManager?.Connection?.ClientId ?? -1;
            bool waiting = running && !_lobbyManager.IsInMatch(localId);

            if (_readyButton != null) _readyButton.interactable = !running;
            if (_forceStartButton != null) _forceStartButton.interactable = !running;
            if (waiting && _readyButtonText != null) _readyButtonText.text = "MATCH IN PROGRESS";
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
            background.color = new Color(0.96f, 0.69f, 0.25f, 0.92f);
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
            text.fontSize = 28f;
            text.color = new Color(0.12f, 0.08f, 0.04f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = "MATCH IN PROGRESS  ·  you will play the next one";

            // Watch it meanwhile: loads the map for this client only, the match is unaffected.
            var watchGo = new GameObject("WatchButton", typeof(RectTransform));
            var watchRect = (RectTransform)watchGo.transform;
            watchRect.SetParent(rect, false);
            watchRect.anchorMin = watchRect.anchorMax = watchRect.pivot = new Vector2(1f, 0.5f);
            watchRect.anchoredPosition = new Vector2(-8f, 0f);
            watchRect.sizeDelta = new Vector2(176f, 44f);
            var watchImage = watchGo.AddComponent<Image>();
            watchImage.color = new Color(0.12f, 0.08f, 0.04f, 0.92f);
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
            label.fontSize = 24f;
            label.color = new Color(0.96f, 0.69f, 0.25f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = "WATCH";
            return go;
        }

        private void OnWatchClicked()
        {
            if (_lobbyManager == null || !_lobbyManager.GameStarted.Value) return;
            _lobbyManager.ServerSpectateMatch();
        }

        private void HandleAiSlotCountSync(int prev, int next, bool asServer) => RefreshPlayerList();

        /// <summary>
        /// Fired on every client (including host via RunLocally) when the server has resolved
        /// the map vote and is about to load the map scene. We trigger the slot-machine animation
        /// here; the server-side coroutine in NetworkedLobbyManager waits a matching duration
        /// before kicking off the actual FishNet scene load.
        /// </summary>
        private void HandleMapVoteAnimation(int[] votedMapIds, int resolvedMapId)
        {
            // Resolve via SerializeField first, fall back to a search inside this screen.
            var anim = _mapVoteAnimation != null
                ? _mapVoteAnimation
                : GetComponentInChildren<FastAndFractured.Multiplayer.Lobby.MapVoteAnimationUI>(true);
            if (anim == null)
            {
                Debug.LogWarning("[LobbyReadyUI] OnMapVoteAnimationRequested but no MapVoteAnimationUI found in scene.");
                return;
            }
            anim.Play(votedMapIds, resolvedMapId);
        }

        private void HandleRoomNameSync(string prev, string next, bool asServer) => UpdateRoomName(next);

        private void UpdateRoomName(string name)
        {
            if (_roomNameLabel != null)
                _roomNameLabel.text = string.IsNullOrEmpty(name) ? "Room" : name;
        }

        private void OnLocalCharacterChanged(string code) =>
            _lobbyManager?.ServerSelectCharacter(code);

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
            // Flag the upcoming client-stop as intentional so HostDisconnectWatchdog
            // doesn't fire its "Host disconnected" path on a user-triggered leave.
            HostDisconnectWatchdog.NotifyIntentionalLeave();

            // Tear down our networking so we don't leak a half-connected client.
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
            if (_countdownText   != null) _countdownText.text            = "STARTING!";
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
            if (_readyButtonText  != null) _readyButtonText.text   = _isReady ? _readyLabel : _notReadyLabel;
            if (_readyButtonImage != null) _readyButtonImage.color = _isReady ? _readyColor : _notReadyColor;
        }

        private void UpdateCountdownText(int seconds)
        {
            if (_countdownText == null) return;
            _countdownText.text = seconds > 0 ? $"Starting in {seconds}..." : "GO!";
        }


        // AI slots use synthetic negative client IDs so they don't collide with real clients.
        private const int AI_SLOT_ID_BASE = -1000;

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

                // Resolve voted map's thumbnail (null if not voted yet or vote invalid).
                Sprite voteSprite = null;
                if (catalog != null &&
                    _lobbyManager.PlayerMapVotes.TryGetValue(clientId, out int mapId) &&
                    mapId >= 0 &&
                    catalog.TryGetById(mapId, out var entryDef))
                {
                    voteSprite = entryDef.Thumbnail;
                }

                bool isLocal = clientId == localClientId;

                // Keep the singleplayer "Selected_Player" key in sync with the local player's
                // lobby character (default Josefino or their pick). The reused singleplayer
                // character/HUD UI reads this key; with PlayerPrefs cleared it would otherwise be
                // empty and the UI would stall half-loaded when you never open Character Selection.
                if (isLocal && !string.IsNullOrEmpty(code))
                    PlayerPrefs.SetString("Selected_Player", code);

                string displayName = _lobbyManager.GetPlayerName(clientId);

                var slot = entry.GetComponent<PlayerSlotUI>();
                if (slot != null)
                {
                    slot.SetData(clientId, code, isReady, voteSprite, isLocal, displayName);
                    slot.SetInMatch(_lobbyManager.IsInMatch(clientId));
                    var btn = slot.GetSelectButton();
                    if (btn != null && isLocal)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(OnSelectCharacterClicked);
                    }
                }
                else
                {
                    // Backward compat: legacy text-only entry prefab (no PlayerSlotUI).
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

            // ── AI slots ──────────────────────────────────────────────────────────
            // Render synthetic slots for each AI configured. Slot IDs are negative so they
            // never collide with real clientIds. AI names come from a stable pool indexed by
            // bot number so every peer sees the same name for the same slot.
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
                    // AI is always "ready", has no map vote, and never shows the local SELECT btn.
                    aiSlot.SetData(slotId, aiName, isReady: true, mapVoteThumbnail: null, isLocal: false);
                }
                else
                {
                    var label = aiEntry.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = $"{aiName} [AI READY]";
                }
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
        /// Click handler for the local player's SELECT CHARACTER &amp; MAP button on their slot.
        /// Kicks off MultiplayerMenuRouter and routes the user through the existing character
        /// + map selection screens, which then return them to LOBBY_ROOM with a vote registered.
        /// Explicitly activates the character selection camera as belt-and-braces: the
        /// CharacterScreen MenuScreen may have a null screenCamera in singleplayer, which would
        /// leave Cinemachine on InitialCamera after MenuScreen.ActivateCamera does nothing.
        /// </summary>
        private void OnSelectCharacterClicked()
        {
            MultiplayerMenuRouter.BeginLobbyFlow();

            // Aggressively activate the char selection camera before screen transition fires its
            // own (possibly empty) ActivateCamera. We try all plausible names that the user might
            // have in their scene.
            ForceActivateCameraByName("CharSlectionCamera", "CharSelectionCamera", "CharacterSelectionCamera");

            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.CHARACTER_SELECTION, 0.5f);
        }

        /// <summary>
        /// Searches the scene for the first GameObject matching any of the supplied names and
        /// SetActive(true)s it. Used to compensate for MenuScreens with no screenCamera wired.
        /// </summary>
        private static void ForceActivateCameraByName(params string[] candidates)
        {
            foreach (string name in candidates)
            {
                // Use Resources.FindObjectsOfTypeAll to find inactive GameObjects too.
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
