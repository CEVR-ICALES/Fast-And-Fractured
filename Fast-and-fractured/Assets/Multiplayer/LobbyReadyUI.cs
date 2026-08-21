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
        private bool _isReady;
        private int _lateSubscribeRetries;
        private const int MAX_LATE_SUBSCRIBE_RETRIES = 20; // 10 seconds @ 0.5s interval
        private readonly Dictionary<int, GameObject> _playerEntries = new Dictionary<int, GameObject>();

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
        }

        private void Unsubscribe()
        {
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
            if (_lobbyManager == null) return;
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

        // Stable bot name pool. Index = AI slot index. AiSlotCount=3 → first 3 names are used.
        // All clients use the same pool so everyone sees the same bot names.
        private static readonly string[] BOT_NAMES =
        {
            "Bot Sandra", "Bot Mike",   "Bot Carla",  "Bot Diego",
            "Bot Lucia",  "Bot Tomas",  "Bot Elena",  "Bot Marcos",
            "Bot Paula",  "Bot Hector", "Bot Nuria",  "Bot Javier",
        };

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
            int aiCount = Mathf.Min(_lobbyManager.AiSlotCount.Value, BOT_NAMES.Length);
            for (int i = 0; i < aiCount; i++)
            {
                int slotId = AI_SLOT_ID_BASE - i;
                currentPlayers.Add(slotId);

                if (!_playerEntries.TryGetValue(slotId, out var aiEntry) || aiEntry == null)
                {
                    aiEntry = Instantiate(_playerEntryPrefab, _playerListContainer);
                    _playerEntries[slotId] = aiEntry;
                }

                string aiName = BOT_NAMES[i];
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
