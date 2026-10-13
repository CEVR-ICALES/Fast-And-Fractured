using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative lobby manager handling:
    /// - Player ready states
    /// - Character selection synchronization
    /// - Game start conditions
    /// 
    /// Place this on a scene NetworkObject (e.g., NetworkManager or dedicated Lobby GO).
    /// </summary>
    public class NetworkedLobbyManager : NetworkBehaviour
    {
        #region EVENTS
        /// <summary>Fired when any player's ready state changes. Args: clientId, isReady</summary>
        public event Action<int, bool> OnPlayerReadyChanged;
        
        /// <summary>Fired when any player's character selection changes. Args: clientId, characterCode</summary>
        public event Action<int, string> OnPlayerCharacterChanged;
        
        /// <summary>Fired when all players are ready and game can start.</summary>
        public event Action OnAllPlayersReady;
        
        /// <summary>Fired when the game countdown starts. Args: countdownSeconds</summary>
        public event Action<int> OnCountdownStarted;
        
        /// <summary>Fired when the game is about to start.</summary>
        public event Action OnGameStarting;
        #endregion

        #region SYNCED_STATE
        /// <summary>
        /// Maps ClientId -> ready state. Synced to all clients.
        /// </summary>
        public readonly SyncDictionary<int, bool> PlayerReadyStates = new SyncDictionary<int, bool>();

        /// <summary>
        /// Maps ClientId -> selected character code (e.g. Pepe_0). Synced to all clients.
        /// </summary>
        public readonly SyncDictionary<int, string> PlayerCharacterSelections = new SyncDictionary<int, string>();

        /// <summary>
        /// Maps ClientId -> display name (Steam persona / platform name). Synced to all clients.
        /// </summary>
        public readonly SyncDictionary<int, string> PlayerNames = new SyncDictionary<int, string>();

        /// <summary>Fired when a player's display name syncs. Args: clientId, name.</summary>
        public event Action<int, string> OnPlayerNameChanged;

        public string GetPlayerName(int clientId) =>
            PlayerNames.TryGetValue(clientId, out var n) && !string.IsNullOrEmpty(n) ? n : $"Player {clientId}";

        /// <summary>
        /// Minimum players required to start the game.
        /// </summary>
        [field: SerializeField]
        public int MinPlayersToStart { get; private set; } = 2;

        /// <summary>
        /// Countdown duration in seconds when all players are ready.
        /// </summary>
        [field: SerializeField]
        public int CountdownDuration { get; private set; } = 5;

        /// <summary>
        /// Current countdown value. -1 means no countdown active.
        /// </summary>
        public readonly SyncVar<int> CurrentCountdown = new SyncVar<int>(-1);

        /// <summary>
        /// Whether the game has started (no more changes allowed).
        /// </summary>
        public readonly SyncVar<bool> GameStarted = new SyncVar<bool>(false);

        // MAP VOTE
        /// <summary>Maps ClientId → voted mapId. -1 means no vote yet.</summary>
        public readonly SyncDictionary<int, int> PlayerMapVotes = new SyncDictionary<int, int>();

        /// <summary>Resolved mapId written by server before game starts. -1 = unresolved.</summary>
        public readonly SyncVar<int> ResolvedMapId = new SyncVar<int>(-1);

        public event Action<int, int> OnPlayerMapVoteChanged;
        public event Action<int>      OnMapResolved;

        // AI SLOTS
        public readonly SyncVar<int> AiSlotCount = new SyncVar<int>(0);

        // ROOM NAME
        /// <summary>Display name for the room, set by the host. Synced to all clients.</summary>
        public readonly SyncVar<string> RoomName = new SyncVar<string>("Room");

        public const int MAX_PLAYERS = 8;

        public event Action<int> OnAiSlotCountChanged;

        // MATCH IN PROGRESS
        /// <summary>
        /// Clients playing the current match (empty between matches). The room outlives the
        /// match: players who join while it runs, and players who leave it early, wait in the
        /// room for the next one.
        /// </summary>
        public readonly SyncList<int> MatchParticipants = new SyncList<int>();

        /// <summary>True when the client is playing the match in progress.</summary>
        public bool IsInMatch(int clientId) => GameStarted.Value && MatchParticipants.Contains(clientId);

        /// <summary>Clients in the room watching the match in progress (they load its map, without a car).</summary>
        public readonly SyncList<int> MatchSpectators = new SyncList<int>();

        public bool IsSpectating(int clientId) => GameStarted.Value && MatchSpectators.Contains(clientId);

        /// <summary>Server: a participant went back to the room before the match ended.</summary>
        public event Action<NetworkConnection> OnParticipantLeftMatch;

        public int HumanPlayerCount => PlayerReadyStates.Count;
        public int TotalSlotCount   => HumanPlayerCount + AiSlotCount.Value;
        public int FreeSlotCount    => Mathf.Max(0, MAX_PLAYERS - TotalSlotCount);
        #endregion

        #region SERVER_STATE
        private float _countdownTimer;
        private bool _countdownActive;
        #endregion

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            PlayerReadyStates.OnChange += HandleReadyStateChange;
            PlayerCharacterSelections.OnChange += HandleCharacterSelectionChange;
            PlayerNames.OnChange += HandlePlayerNameChange;
            PlayerMapVotes.OnChange += HandleMapVoteChange;
            AiSlotCount.OnChange   += HandleAiSlotChange;
            GameStarted.OnChange   += HandleGameStartedServer;
            AdvertiseMatchState(false);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (ServerManager != null)
                ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            PlayerReadyStates.OnChange -= HandleReadyStateChange;
            PlayerCharacterSelections.OnChange -= HandleCharacterSelectionChange;
            PlayerNames.OnChange -= HandlePlayerNameChange;
            PlayerMapVotes.OnChange -= HandleMapVoteChange;
            AiSlotCount.OnChange   -= HandleAiSlotChange;
            GameStarted.OnChange   -= HandleGameStartedServer;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            PlayerReadyStates.OnChange += HandleReadyStateChange;
            PlayerCharacterSelections.OnChange += HandleCharacterSelectionChange;
            PlayerNames.OnChange += HandlePlayerNameChange;
            CurrentCountdown.OnChange += HandleCountdownChange;
            GameStarted.OnChange += HandleGameStartedChange;
            AiSlotCount.OnChange += HandleAiSlotChange;

            // OnRemoteConnectionState doesn't fire for the host's own client connection. Register
            // AND seed the default character + display name in one RPC. We deliberately do NOT
            // read "Selected_Player" — Construct overwrites that with the scene default, so it
            // can't persist the real online choice.
            ServerRegisterPlayerRpc(GetPreferredCharacterCode(), GetLocalDisplayName());
        }

        /// <summary>Local player's platform display name (Steam persona), or null if unavailable.</summary>
        internal static string GetLocalDisplayName()
        {
            var provider = Abstractions.LobbyServiceLocator.IdentityProvider;
            if (provider == null) return null;
            var task = provider.GetLocalIdentityAsync();
            // Steam/Tugboat providers complete synchronously (Task.FromResult).
            return task.IsCompleted && task.Result != null ? task.Result.DisplayName : null;
        }

        /// <summary>Last character the local player picked online, or Josefino if never played.</summary>
        public static string GetPreferredCharacterCode()
        {
            string code = PlayerPrefs.GetString(MP_LAST_CHARACTER_PREF_KEY, DEFAULT_CHARACTER_CODE);
            return string.IsNullOrWhiteSpace(code) ? DEFAULT_CHARACTER_CODE : code;
        }

        /// <summary>Persists the local player's online character pick for next session.</summary>
        public static void SavePreferredCharacterCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            PlayerPrefs.SetString(MP_LAST_CHARACTER_PREF_KEY, code);
        }

        public const string DEFAULT_CHARACTER_CODE   = "Josefino_0";
        public const string MP_LAST_CHARACTER_PREF_KEY = "MP_LastCharacter";

        public override void OnStopClient()
        {
            base.OnStopClient();
            PlayerReadyStates.OnChange -= HandleReadyStateChange;
            PlayerCharacterSelections.OnChange -= HandleCharacterSelectionChange;
            PlayerNames.OnChange -= HandlePlayerNameChange;
            CurrentCountdown.OnChange -= HandleCountdownChange;
            GameStarted.OnChange -= HandleGameStartedChange;
            AiSlotCount.OnChange -= HandleAiSlotChange;
        }

        private void HandlePlayerNameChange(SyncDictionaryOperation op, int clientId, string name, bool asServer)
        {
            if (!asServer) return;
            OnPlayerNameChanged?.Invoke(clientId, name);
        }

        private void Update()
        {
            if (!IsServerInitialized || !_countdownActive) return;

            _countdownTimer -= Time.deltaTime;
            int newCountdown = Mathf.CeilToInt(_countdownTimer);
            
            if (newCountdown != CurrentCountdown.Value && newCountdown >= 0)
            {
                CurrentCountdown.Value = newCountdown;
            }

            if (_countdownTimer <= 0f)
            {
                _countdownActive = false;
                CurrentCountdown.Value = 0;
                StartGameInternal();
            }
        }

        #region SERVER_HANDLERS
        private void HandleRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
            {
                PlayerReadyStates[conn.ClientId] = false;
                PlayerCharacterSelections[conn.ClientId] = "";
                // Clamp AI slots so total never exceeds MAX_PLAYERS after a new player joins
                int maxAi = Mathf.Max(0, MAX_PLAYERS - HumanPlayerCount);
                if (AiSlotCount.Value > maxAi)
                    AiSlotCount.Value = maxAi;
            }
            else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
            {
                PlayerReadyStates.Remove(conn.ClientId);
                PlayerCharacterSelections.Remove(conn.ClientId);
                PlayerNames.Remove(conn.ClientId);
                PlayerMapVotes.Remove(conn.ClientId);
                MatchParticipants.Remove(conn.ClientId);
                MatchSpectators.Remove(conn.ClientId);

                if (_countdownActive && PlayerReadyStates.Count < MinPlayersToStart)
                    CancelCountdown();
            }
        }

        private void HandleReadyStateChange(SyncDictionaryOperation op, int key, bool value, bool asServer)
        {
            if (!asServer) return;
            
            OnPlayerReadyChanged?.Invoke(key, value);
            CheckAllPlayersReady();
        }

        private void HandleCharacterSelectionChange(SyncDictionaryOperation op, int key, string value, bool asServer)
        {
            if (!asServer) return;
            OnPlayerCharacterChanged?.Invoke(key, value);
        }

        private void HandleMapVoteChange(SyncDictionaryOperation op, int clientId, int mapId, bool asServer)
        {
            if (!asServer) return;
            OnPlayerMapVoteChanged?.Invoke(clientId, mapId);
        }

        private void HandleAiSlotChange(int prev, int next, bool asServer)
        {
            OnAiSlotCountChanged?.Invoke(next);
        }

        private void CheckAllPlayersReady()
        {
            if (GameStarted.Value) return;
            
            int playerCount = PlayerReadyStates.Count;
            if (playerCount < MinPlayersToStart)
            {
                if (_countdownActive) CancelCountdown();
                return;
            }

            bool allReady = true;
            foreach (var kvp in PlayerReadyStates)
            {
                if (!kvp.Value)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady && !_countdownActive)
            {
                StartCountdown();
            }
            else if (!allReady && _countdownActive)
            {
                CancelCountdown();
            }
        }

        private void StartCountdown()
        {
            _countdownActive = true;
            _countdownTimer = CountdownDuration;
            CurrentCountdown.Value = CountdownDuration;

            OnAllPlayersReady?.Invoke();
            OnCountdownStarted?.Invoke(CountdownDuration);
            
            RpcNotifyCountdownStarted(CountdownDuration);
        }

        private void CancelCountdown()
        {
            _countdownActive = false;
            CurrentCountdown.Value = -1;

            RpcNotifyCountdownCancelled();
        }

        /// <summary>
        /// Client-side event fired when the server broadcasts the map vote resolution. Args:
        /// votedMapIds (deck to cycle through), resolvedMapId (the winner). LobbyRoom UI listens.
        /// </summary>
        public event System.Action<int[], int> OnMapVoteAnimationRequested;

        private void StartGameInternal()
        {
            // Idempotency guard: ServerForceStart + countdown completion could both call us
            // in the same frame; ResolveMapVote uses TimeManager.LocalTick as seed so a second
            // call would pick a different map and trigger a second LoadGlobalScenes → FishNet
            // kicks the client with "scene load response for handle X, but client was not sent
            // a load for this handle" because the handles don't reconcile.
            if (GameStarted.Value) return;
            GameStarted.Value = true;

            // Stop the countdown immediately so Update() doesn't fall through and re-enter here
            // a few frames later after Force Start already kicked the match off.
            _countdownActive = false;

            // Everyone in the room plays; whoever joins from now on waits for the next match.
            MatchParticipants.Clear();
            foreach (var kv in PlayerReadyStates)
                MatchParticipants.Add(kv.Key);

            // Resolve map vote first so ResolvedMapId is set before anyone reads it.
            ResolveMapVote();

            // Build the "voted maps deck" — the unique set of mapIds players voted for. If empty
            // (everyone abstained) we'll just animate landing on the resolved one.
            var votedIds = new HashSet<int>();
            foreach (var kv in PlayerMapVotes)
            {
                if (kv.Value >= 0) votedIds.Add(kv.Value);
            }
            if (votedIds.Count == 0) votedIds.Add(ResolvedMapId.Value);
            int[] deck = new int[votedIds.Count];
            votedIds.CopyTo(deck);

            // Tell every client (including host) to start the slot-machine animation.
            // The server schedules the actual scene load after the animation duration so the
            // visual reveal completes before the menu scene unloads.
            RpcPlayMapVoteAnimation(deck, ResolvedMapId.Value);
            OnGameStarting?.Invoke();

            // Legacy path: if a NetworkedLevelControllerManager happens to be in this scene
            // (single-scene setups), keep feeding it character selections. With the recommended
            // setup the level controller lives in the GAME scene and reads PlayerCharacterSelections
            // from THIS lobby manager (which is Global, so it survives the scene load).
            var legacyLevelController = FindObjectOfType<NetworkedLevelControllerManager>();
            if (legacyLevelController != null)
            {
                var orderedConnections = GetOrderedActivePlayerConnections();
                for (int i = 0; i < orderedConnections.Count; i++)
                {
                    var conn = orderedConnections[i];
                    if (PlayerCharacterSelections.TryGetValue(conn.ClientId, out string characterCode) &&
                        !string.IsNullOrEmpty(characterCode))
                    {
                        legacyLevelController.AddCharacterSkin(conn, characterCode);
                    }
                    else
                    {
                        string fallbackCode = GetFallbackCharacterCode(i);
                        legacyLevelController.AddCharacterSkin(conn, fallbackCode);
                        Debug.LogWarning($"[NetworkedLobbyManager] Missing character selection for client {conn.ClientId}. Using fallback '{fallbackCode}'.");
                    }
                }
                legacyLevelController.StartGame();
            }
            else
            {
                // Modern path: schedule the FishNet scene load to fire after the animation.
                StartCoroutine(LoadMapSceneAfterAnimation());
            }
        }

        // ── Slot-machine animation + scene load ───────────────────────────────
        private const float MAP_VOTE_ANIMATION_DURATION = 3.2f;

        [ObserversRpc(RunLocally = true)]
        private void RpcPlayMapVoteAnimation(int[] votedMapIds, int resolvedMapId)
        {
            OnMapVoteAnimationRequested?.Invoke(votedMapIds, resolvedMapId);
        }

        private System.Collections.IEnumerator LoadMapSceneAfterAnimation()
        {
            yield return new WaitForSeconds(MAP_VOTE_ANIMATION_DURATION);

            string sceneToLoad = MatchSceneName();
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogError($"[NetworkedLobbyManager] Cannot load map: ResolvedMapId={ResolvedMapId.Value} has no scene in MapCatalog.");
                yield break;
            }

            Debug.Log($"[NetworkedLobbyManager] Loading map scene '{sceneToLoad}' for resolved mapId={ResolvedMapId.Value}.");

            // Only the match's players load the map (a connection scene, not a global one):
            // a global scene would also be pushed onto anyone joining the room mid-match, and
            // nobody could go back to the room while it is loaded. MatchSceneVisibility keeps
            // the map's objects away from the players in the room.
            var players = new List<NetworkConnection>();
            foreach (int clientId in MatchParticipants)
                if (ServerManager.Clients.TryGetValue(clientId, out var conn) && conn.IsActive)
                    players.Add(conn);
            if (players.Count == 0)
            {
                Debug.LogWarning("[NetworkedLobbyManager] Every player left before the map loaded; back to the room.");
                ServerEndMatch();
                yield break;
            }

            var sceneLoadData = new FishNet.Managing.Scened.SceneLoadData(sceneToLoad)
            {
                ReplaceScenes = FishNet.Managing.Scened.ReplaceOption.All
            };
            FishNet.InstanceFinder.SceneManager.LoadConnectionScenes(players.ToArray(), sceneLoadData);
        }

        /// <summary>Scene of the voted map (null before the vote is resolved).</summary>
        private string MatchSceneName()
        {
            var catalog = FastAndFractured.Multiplayer.Lobby.MapCatalogLoader.Get();
            if (catalog == null || ResolvedMapId.Value < 0 || !catalog.TryGetById(ResolvedMapId.Value, out var entry))
                return null;
            return entry.GetSceneNameForMultiplayer();
        }

        // -- Watching a match in progress ----------------------------------------------
        /// <summary>A player waiting in the room asks to watch the match in progress.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerSpectateMatch(NetworkConnection conn = null) => SpectateMatch(conn);

        /// <summary>
        /// Loads the match's map for this one connection. The match itself is untouched: the
        /// server already runs the scene, it only starts sending its objects to one more client
        /// (MatchSceneVisibility) - a little upload bandwidth, nothing simulated twice. The
        /// match's controller hands the spectator the start data once its map has loaded.
        /// </summary>
        [Server]
        public void SpectateMatch(NetworkConnection conn)
        {
            if (conn == null || !GameStarted.Value || conn.IsHost) return;
            if (MatchParticipants.Contains(conn.ClientId) || MatchSpectators.Contains(conn.ClientId)) return;
            string scene = MatchSceneName();
            if (string.IsNullOrEmpty(scene)) return;

            MatchSpectators.Add(conn.ClientId);
            SceneManager.LoadConnectionScenes(conn, new FishNet.Managing.Scened.SceneLoadData(scene)
            {
                ReplaceScenes = FishNet.Managing.Scened.ReplaceOption.All
            });
            Debug.Log($"[NetworkedLobbyManager] Client {conn.ClientId} is now watching the match ('{scene}').");
        }

        // -- End of a match: back to the room ------------------------------------------
        /// <summary>
        /// Server: the match is over (or was abandoned); the room is ready for the next one.
        /// Players, characters, AI slots and the room name stay; ready states and map votes
        /// start over.
        /// </summary>
        [Server]
        public void ServerEndMatch()
        {
            _countdownActive = false;
            CurrentCountdown.Value = -1;
            MatchParticipants.Clear();
            MatchSpectators.Clear();
            PlayerMapVotes.Clear();
            ResolvedMapId.Value = -1;
            var ids = new List<int>(PlayerReadyStates.Keys);
            foreach (int id in ids)
                PlayerReadyStates[id] = false;
            GameStarted.Value = false;
            Debug.Log("[NetworkedLobbyManager] Match over - the room is open for the next one.");
        }

        /// <summary>A player leaves the match in progress and goes back to the room.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerLeaveMatch(NetworkConnection conn = null) => LeaveMatch(conn);

        /// <summary>
        /// Server side of leaving a match early. The host cannot leave its own match (the map
        /// runs on it), so the host leaving ends the match for everyone.
        /// </summary>
        [Server]
        public void LeaveMatch(NetworkConnection conn)
        {
            if (conn == null || !GameStarted.Value) return;
            if (conn.IsHost)
            {
                ServerAbortMatch();
                return;
            }
            bool spectator = MatchSpectators.Remove(conn.ClientId);
            if (!spectator)
            {
                if (!MatchParticipants.Remove(conn.ClientId)) return;
                OnParticipantLeftMatch?.Invoke(conn);
            }
            try { SceneManager.RemoveConnectionsFromNonGlobalScenes(new[] { conn }); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Server: ends the match without a result and brings everyone back to the room.</summary>
        [Server]
        public void ServerAbortMatch()
        {
            if (!GameStarted.Value) return;
            RpcReturnToRoom();
            ServerEndMatch();
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcReturnToRoom() => MultiplayerRoomReturn.ReturnFromAbortedMatch();

        private void HandleGameStartedServer(bool prev, bool next, bool asServer)
        {
            if (asServer) AdvertiseMatchState(next);
        }

        /// <summary>The room list shows rooms in a match as "In a match" (still joinable).</summary>
        private static void AdvertiseMatchState(bool inMatch)
        {
            try
            {
                Abstractions.LobbyServiceLocator.Transport?.SetLobbyMetadata(
                    Abstractions.LobbyListing.KEY_IN_MATCH, inMatch ? "1" : "0");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void ResolveMapVote()
        {
            var catalog = FastAndFractured.Multiplayer.Lobby.MapCatalogLoader.Get();
            if (catalog == null || catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[NetworkedLobbyManager] MapCatalog unavailable — using mapId 0.");
                ResolvedMapId.Value = 0;
                OnMapResolved?.Invoke(0);
                return;
            }

            var validIds = new System.Collections.Generic.List<int>();
            foreach (var entry in catalog.Entries)
                validIds.Add(entry.Id);

            var rng      = new System.Random(unchecked((int)TimeManager.LocalTick));
            int resolved = FastAndFractured.Multiplayer.Lobby.MapVoteResolver.Resolve(PlayerMapVotes, validIds, rng);
            ResolvedMapId.Value = resolved;
            OnMapResolved?.Invoke(resolved);
        }
        #endregion

        #region CLIENT_HANDLERS
        private void HandleCountdownChange(int prev, int next, bool asServer)
        {
            if (asServer) return;
            
            if (next > 0 && prev <= 0)
            {
                OnCountdownStarted?.Invoke(next);
            }
        }

        private void HandleGameStartedChange(bool prev, bool next, bool asServer)
        {
            if (asServer) return;

            if (next && !prev)
            {
                OnGameStarting?.Invoke();
                // Arm the host-disconnect watchdog: any subsequent unexpected client-stop
                // during this match means the host died (or left), so we return to the hub
                // instead of freezing on a dead scene.
                HostDisconnectWatchdog.NotifyMatchStarted();
            }
        }
        #endregion

        #region CLIENT_RPCS
        /// <summary>
        /// Client requests to toggle their ready state.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerSetReady(bool ready, NetworkConnection conn = null)
        {
            if (GameStarted.Value) return;
            if (conn == null) return;
            
            PlayerReadyStates[conn.ClientId] = ready;
        }

        /// <summary>
        /// Client requests to select a character.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerSelectCharacter(string characterName, NetworkConnection conn = null)
        {
            if (conn == null) return;
            if (IsInMatch(conn.ClientId)) return;   // players waiting in the room pick for the next match

            string characterCode = NormalizeCharacterCode(characterName);
            if (string.IsNullOrEmpty(characterCode))
            {
                Debug.LogWarning($"[NetworkedLobbyManager] Ignored empty character selection from player {conn.ClientId}");
                return;
            }

            PlayerCharacterSelections[conn.ClientId] = characterCode;
        }

        /// <summary>
        /// Host/Server can force start the game regardless of ready states.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerForceStart(NetworkConnection conn = null)
        {
            if (GameStarted.Value) return;
            
            // Only allow host (clientId 0 or server-only)
            if (conn != null && conn.ClientId != 0 && !conn.IsHost)
            {
                Debug.LogWarning($"[NetworkedLobbyManager] Non-host player {conn.ClientId} tried to force start");
                return;
            }
            
            if (PlayerReadyStates.Count < 1)
            {
                Debug.LogWarning("[NetworkedLobbyManager] Cannot force start with no players");
                return;
            }

            StartGameInternal();
        }
        #endregion

        /// <summary>Client casts or changes their map vote.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerVoteMap(int mapId, NetworkConnection conn = null)
        {
            if (conn == null || IsInMatch(conn.ClientId)) return;
            PlayerMapVotes[conn.ClientId] = mapId;
        }

        /// <summary>
        /// Every client (host included) calls this on OnStartClient so they appear in
        /// PlayerReadyStates. Carries the player's preferred default character so registration +
        /// default selection happen in ONE RPC — sending a second ServerRpc back-to-back from
        /// OnStartClient was racy and the selection sometimes got dropped (leaving the player on
        /// the GetFallbackCharacterCode default instead of Josefino/their last pick).
        /// Idempotent: safe to call twice.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRegisterPlayerRpc(string preferredCharacter, string displayName, NetworkConnection conn = null)
        {
            // Also while a match runs: the newcomer waits in the room for the next one.
            if (conn == null) return;

            if (!PlayerReadyStates.ContainsKey(conn.ClientId))
                PlayerReadyStates[conn.ClientId] = false;

            // Seed the character only if the player hasn't already picked one (don't clobber a
            // real selection on a reconnect / double-call).
            if (!PlayerCharacterSelections.TryGetValue(conn.ClientId, out var existing) || string.IsNullOrEmpty(existing))
            {
                string code = NormalizeCharacterCode(preferredCharacter);
                PlayerCharacterSelections[conn.ClientId] = string.IsNullOrEmpty(code) ? DEFAULT_CHARACTER_CODE : code;
            }

            PlayerNames[conn.ClientId] = string.IsNullOrWhiteSpace(displayName) ? $"Player {conn.ClientId}" : displayName;

            int maxAi = Mathf.Max(0, MAX_PLAYERS - HumanPlayerCount);
            if (AiSlotCount.Value > maxAi) AiSlotCount.Value = maxAi;
        }

        /// <summary>Host sets the number of AI slots. Clamps to available free slots.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerSetAiSlotCount(int count, NetworkConnection conn = null)
        {
            if (GameStarted.Value) return;
            if (conn != null && conn.ClientId != 0 && !conn.IsHost)
            {
                Debug.LogWarning($"[NetworkedLobbyManager] Non-host client {conn?.ClientId} tried to set AI slots.");
                return;
            }

            int maxAi = Mathf.Max(0, MAX_PLAYERS - HumanPlayerCount);
            AiSlotCount.Value = Mathf.Clamp(count, 0, maxAi);
        }

        /// <summary>Host sets the display name of the room. Other clients see it via SyncVar.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerSetRoomName(string name, NetworkConnection conn = null)
        {
            if (GameStarted.Value) return;
            if (conn != null && conn.ClientId != 0 && !conn.IsHost)
            {
                Debug.LogWarning($"[NetworkedLobbyManager] Non-host client {conn?.ClientId} tried to set room name.");
                return;
            }

            ApplyRoomName(name);
        }

        /// <summary>
        /// Host path. The host is the server, and a [ServerRpc] called from the server never runs
        /// its logic (see memory: ServerRpc-from-host no-op) - the room name the host typed was
        /// silently dropped and the lobby room showed "Room" (playtest feedback 2026-09-30).
        /// </summary>
        [Server]
        public void ApplyRoomName(string name)
        {
            if (GameStarted.Value) return;
            string sanitized = string.IsNullOrWhiteSpace(name) ? "Room" : name.Trim();
            if (sanitized.Length > 64) sanitized = sanitized.Substring(0, 64);
            RoomName.Value = sanitized;
        }

        #region OBSERVERS_RPCS
        [ObserversRpc]
        private void RpcNotifyCountdownStarted(int seconds)
        {
            if (IsServerInitialized) return;
            OnCountdownStarted?.Invoke(seconds);
        }

        [ObserversRpc]
        private void RpcNotifyCountdownCancelled()
        {
            if (IsServerInitialized) return;
        }
        #endregion

        #region PUBLIC_HELPERS
        /// <summary>
        /// Check if the local player is ready.
        /// </summary>
        public bool IsLocalPlayerReady()
        {
            if (!IsClientInitialized) return false;
            int localId = LocalConnection.ClientId;
            return PlayerReadyStates.ContainsKey(localId) && PlayerReadyStates[localId];
        }

        /// <summary>
        /// Get the character selected by a specific player.
        /// </summary>
        public string GetPlayerCharacter(int clientId)
        {
            return PlayerCharacterSelections.ContainsKey(clientId) ? PlayerCharacterSelections[clientId] : "";
        }

        /// <summary>
        /// Get count of ready players.
        /// </summary>
        public int GetReadyPlayerCount()
        {
            int count = 0;
            foreach (var kvp in PlayerReadyStates)
            {
                if (kvp.Value) count++;
            }
            return count;
        }

        /// <summary>
        /// Get total player count in lobby.
        /// </summary>
        public int GetTotalPlayerCount()
        {
            return PlayerReadyStates.Count;
        }

        private List<NetworkConnection> GetOrderedActivePlayerConnections()
        {
            var ordered = new List<NetworkConnection>();
            var seenClientIds = new HashSet<int>();

            foreach (var conn in ServerManager.Clients.Values)
            {
                if (conn == null || !conn.IsActive)
                    continue;

                if (seenClientIds.Add(conn.ClientId))
                    ordered.Add(conn);
            }

            if (IsHostInitialized && LocalConnection != null && LocalConnection.IsValid && seenClientIds.Add(LocalConnection.ClientId))
            {
                ordered.Add(LocalConnection);
            }

            ordered.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
            return ordered;
        }

        public static string GetFallbackCharacterCode(int orderedPlayerIndex)
        {
            string[] fallbackCodes = { "Pepe_0", "Josefino_0", "MariaAntonia_0", "Carme_0" };
            if (orderedPlayerIndex < 0)
                orderedPlayerIndex = 0;

            return fallbackCodes[orderedPlayerIndex % fallbackCodes.Length];
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
        #endregion
    }
}
