using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Server-authoritative room: ready states, character picks, map vote, AI slots and match start.</summary>
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
        /// <summary>ClientId -> ready state.</summary>
        public readonly SyncDictionary<int, bool> PlayerReadyStates = new SyncDictionary<int, bool>();

        /// <summary>ClientId -> selected character code (e.g. Pepe_0).</summary>
        public readonly SyncDictionary<int, string> PlayerCharacterSelections = new SyncDictionary<int, string>();

        /// <summary>ClientId -> display name (Steam persona or platform name).</summary>
        public readonly SyncDictionary<int, string> PlayerNames = new SyncDictionary<int, string>();

        /// <summary>Fired when a player's display name syncs. Args: clientId, name.</summary>
        public event Action<int, string> OnPlayerNameChanged;

        /// <summary>Fired on every peer when a player's progression level arrives or changes. Args: clientId, level</summary>
        public event Action<int, int> OnPlayerLevelChanged;

        /// <summary>Each player's progression level, sent by the player itself (shown in the room).</summary>
        public readonly SyncDictionary<int, int> PlayerLevels = new SyncDictionary<int, int>();

        /// <summary>The player's level, or 0 when it has not arrived yet.</summary>
        public int GetPlayerLevel(int clientId) => PlayerLevels.TryGetValue(clientId, out int level) ? level : 0;

        public string GetPlayerName(int clientId) =>
            PlayerNames.TryGetValue(clientId, out var n) && !string.IsNullOrEmpty(n) ? n : $"Player {clientId}";

        [field: SerializeField]
        public int MinPlayersToStart { get; private set; } = 2;

        [field: SerializeField]
        public int CountdownDuration { get; private set; } = 5;

        /// <summary>Current countdown value; -1 when none is running.</summary>
        public readonly SyncVar<int> CurrentCountdown = new SyncVar<int>(-1);

        public readonly SyncVar<bool> GameStarted = new SyncVar<bool>(false);

        /// <summary>Maps ClientId → voted mapId. -1 means no vote yet.</summary>
        public readonly SyncDictionary<int, int> PlayerMapVotes = new SyncDictionary<int, int>();

        /// <summary>Resolved mapId written by server before game starts. -1 = unresolved.</summary>
        public readonly SyncVar<int> ResolvedMapId = new SyncVar<int>(-1);

        public event Action<int, int> OnPlayerMapVoteChanged;
        public event Action<int>      OnMapResolved;

        public readonly SyncVar<int> AiSlotCount = new SyncVar<int>(0);

        /// <summary>Display name for the room, set by the host. Synced to all clients.</summary>
        public readonly SyncVar<string> RoomName = new SyncVar<string>("Room");

        public const int MAX_PLAYERS = 8;

        public event Action<int> OnAiSlotCountChanged;

        /// <summary>Clients playing the current match (empty between matches); everyone else waits in the room.</summary>
        public readonly SyncList<int> MatchParticipants = new SyncList<int>();

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
            PlayerLevels.OnChange += HandlePlayerLevelChange;
            Progression.PlayerProgression.OnChanged += SendLocalLevel;
            CurrentCountdown.OnChange += HandleCountdownChange;
            GameStarted.OnChange += HandleGameStartedChange;
            AiSlotCount.OnChange += HandleAiSlotChange;

            // The host's own connection fires no OnRemoteConnectionState: register it here. Not from Selected_Player,
            // which Construct overwrites.
            ServerRegisterPlayerRpc(GetPreferredCharacterCode(), GetLocalDisplayName(), Progression.PlayerProgression.Level);
        }

        /// <summary>The local level after it changes (a match's XP), so the room shows the new one.</summary>
        private void SendLocalLevel()
        {
            if (!IsClientInitialized) return;
            int level = Progression.PlayerProgression.Level;
            if (LocalConnection != null && GetPlayerLevel(LocalConnection.ClientId) == level) return;
            ServerSetLevelRpc(level);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerSetLevelRpc(int level, NetworkConnection conn = null)
        {
            if (conn != null) PlayerLevels[conn.ClientId] = ClampLevel(level);
        }

        /// <summary>A level sent by a client, kept within the progression's range.</summary>
        public static int ClampLevel(int level) => Mathf.Clamp(level, 1, Progression.ProgressionMath.MAX_LEVEL);

        private void HandlePlayerLevelChange(SyncDictionaryOperation op, int clientId, int level, bool asServer)
        {
            if (asServer && IsClientInitialized) return;   // the host hears it once, as client
            OnPlayerLevelChanged?.Invoke(clientId, level);
        }

        /// <summary>Local player's platform display name (Steam persona), or null if unavailable.</summary>
        internal static string GetLocalDisplayName()
        {
            var provider = Abstractions.LobbyServiceLocator.IdentityProvider;
            if (provider == null) return null;
            var task = provider.GetLocalIdentityAsync();
            return task.IsCompleted && task.Result != null ? task.Result.DisplayName : null;
        }

        /// <summary>Last character the local player picked online, or Josefino if never played.</summary>
        public static string GetPreferredCharacterCode()
        {
            string code = PlayerPrefs.GetString(MP_LAST_CHARACTER_PREF_KEY, DEFAULT_CHARACTER_CODE);
            return string.IsNullOrWhiteSpace(code) ? DEFAULT_CHARACTER_CODE : code;
        }

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
            PlayerLevels.OnChange -= HandlePlayerLevelChange;
            Progression.PlayerProgression.OnChanged -= SendLocalLevel;
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
                int maxAi = Mathf.Max(0, MAX_PLAYERS - HumanPlayerCount);
                if (AiSlotCount.Value > maxAi)
                    AiSlotCount.Value = maxAi;
            }
            else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
            {
                PlayerReadyStates.Remove(conn.ClientId);
                PlayerCharacterSelections.Remove(conn.ClientId);
                PlayerNames.Remove(conn.ClientId);
                PlayerLevels.Remove(conn.ClientId);
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

        /// <summary>Client: the server's map pick (candidates, votes, hops ending on the chosen map, random or not).</summary>
        public event System.Action<int[], int[], int[], bool> OnMapVoteAnimationRequested;

        private void StartGameInternal()
        {
            // Force start and the countdown can both land here; a second pick would start a second scene load.
            if (GameStarted.Value) return;
            GameStarted.Value = true;

            _countdownActive = false;

            // Everyone in the room plays; whoever joins from now on waits for the next match.
            MatchParticipants.Clear();
            foreach (var kv in PlayerReadyStates)
                MatchParticipants.Add(kv.Key);

            ResolveMapVote();

            // Every peer animates the same pick; the server loads the map once it is over.
            var pick = BuildMapPick(PlayerMapVotes, CatalogMapIds(), ResolvedMapId.Value,
                                    new System.Random(unchecked((int)TimeManager.LocalTick * MAP_PICK_SEED_MULTIPLIER + MAP_PICK_SEED_OFFSET)));
            _mapPickDuration = FastAndFractured.Multiplayer.Lobby.MapVoteAnimationUI.DurationFor(pick.Hops.Length);
            RpcPlayMapVoteAnimation(pick.Candidates, pick.Votes, pick.Hops, pick.Random);
            OnGameStarting?.Invoke();

            // Single-scene setups: feed a NetworkedLevelControllerManager in this scene, if any.
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
                StartCoroutine(LoadMapSceneAfterAnimation());
            }
        }

        private const int MIN_MAP_PICK_HOPS = 12;
        private const int RANDOM_MAP_PICK_HOPS = 14;
        private float _mapPickDuration = 3f;
        private const int MAP_PICK_SEED_MULTIPLIER = 31;
        private const int MAP_PICK_SEED_OFFSET = 7;
        private const int MAX_ROOM_NAME_LENGTH = 64;

        /// <summary>What the map pick animation shows; see MapVoteAnimationUI.</summary>
        public struct MapPick
        {
            public int[] Candidates;   // map ids on the strip
            public int[] Votes;        // votes per candidate; empty for a random pick
            public int[] Hops;         // indices into Candidates; the last one is the winner
            public bool Random;
        }

        /// <summary>Votes cast: a wheel over the voted maps stopping on the winner. Nobody voted: random non-repeating hops over every map.</summary>
        public static MapPick BuildMapPick(IEnumerable<KeyValuePair<int, int>> votes, IList<int> allMapIds,
                                           int winner, System.Random rng)
        {
            var tally = new SortedDictionary<int, int>();
            if (votes != null)
                foreach (var vote in votes)
                    if (allMapIds.Contains(vote.Value))
                        tally[vote.Value] = tally.TryGetValue(vote.Value, out int n) ? n + 1 : 1;

            var pick = new MapPick();
            if (tally.Count > 0 && tally.ContainsKey(winner))
            {
                pick.Candidates = new int[tally.Count];
                pick.Votes = new int[tally.Count];
                int i = 0;
                foreach (var kv in tally) { pick.Candidates[i] = kv.Key; pick.Votes[i] = kv.Value; i++; }
                int count = pick.Candidates.Length;
                int target = System.Array.IndexOf(pick.Candidates, winner);
                int hops = count == 1 ? 1 : target + 1;
                while (count > 1 && hops < MIN_MAP_PICK_HOPS) hops += count;
                pick.Hops = new int[hops];
                for (int k = 0; k < hops; k++) pick.Hops[k] = k % count;
                return pick;
            }

            var all = new List<int>(allMapIds);
            if (!all.Contains(winner)) all.Add(winner);
            all.Sort();
            pick.Candidates = all.ToArray();
            pick.Votes = new int[0];
            pick.Random = true;
            int winnerIndex = all.IndexOf(winner);
            pick.Hops = new int[all.Count == 1 ? 1 : RANDOM_MAP_PICK_HOPS];
            int previous = -1;
            for (int k = 0; k < pick.Hops.Length - 1; k++)
            {
                int next;
                do next = rng.Next(all.Count);
                while (next == previous || (k == pick.Hops.Length - 2 && next == winnerIndex));
                pick.Hops[k] = previous = next;
            }
            pick.Hops[pick.Hops.Length - 1] = winnerIndex;
            return pick;
        }

        private static List<int> CatalogMapIds()
        {
            var ids = new List<int>();
            var catalog = FastAndFractured.Multiplayer.Lobby.MapCatalogLoader.Get();
            if (catalog != null)
                foreach (var entry in catalog.Entries) ids.Add(entry.Id);
            return ids;
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcPlayMapVoteAnimation(int[] candidates, int[] votes, int[] hops, bool random)
        {
            OnMapVoteAnimationRequested?.Invoke(candidates, votes, hops, random);
        }

        private System.Collections.IEnumerator LoadMapSceneAfterAnimation()
        {
            yield return new WaitForSeconds(_mapPickDuration);

            string sceneToLoad = MatchSceneName();
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogError($"[NetworkedLobbyManager] Cannot load map: ResolvedMapId={ResolvedMapId.Value} has no scene in MapCatalog.");
                yield break;
            }

            Debug.Log($"[NetworkedLobbyManager] Loading map scene '{sceneToLoad}' for resolved mapId={ResolvedMapId.Value}.");

            // A connection scene, not a global one: it would be pushed onto anyone joining the room mid-match.
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

        /// <summary>A player waiting in the room asks to watch the match in progress.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerSpectateMatch(NetworkConnection conn = null) => SpectateMatch(conn);

        /// <summary>Loads the match's map for this connection only; the server just starts sending its objects there.</summary>
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

        /// <summary>Server: the match is over; players, characters, AI slots and room name stay, ready states and votes reset.</summary>
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

        /// <summary>Server side of leaving early; the host leaving ends the match (the map runs on it).</summary>
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
                // From now on an unexpected client stop means the host is gone.
                HostDisconnectWatchdog.NotifyMatchStarted();
            }
        }
        #endregion

        #region CLIENT_RPCS
        [ServerRpc(RequireOwnership = false)]
        public void ServerSetReady(bool ready, NetworkConnection conn = null)
        {
            if (GameStarted.Value) return;
            if (conn == null) return;
            
            PlayerReadyStates[conn.ClientId] = ready;
        }

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

        [ServerRpc(RequireOwnership = false)]
        public void ServerForceStart(NetworkConnection conn = null)
        {
            if (GameStarted.Value) return;
            
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

        [ServerRpc(RequireOwnership = false)]
        public void ServerVoteMap(int mapId, NetworkConnection conn = null)
        {
            if (conn == null || IsInMatch(conn.ClientId)) return;
            PlayerMapVotes[conn.ClientId] = mapId;
        }

        /// <summary>Every client registers on OnStartClient, with its default character in the same RPC (a second one could be dropped). Idempotent.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRegisterPlayerRpc(string preferredCharacter, string displayName, int level, NetworkConnection conn = null)
        {
            // Also while a match runs: the newcomer waits in the room for the next one.
            if (conn == null) return;

            if (!PlayerReadyStates.ContainsKey(conn.ClientId))
                PlayerReadyStates[conn.ClientId] = false;

            // Keep a real pick on a reconnect.
            if (!PlayerCharacterSelections.TryGetValue(conn.ClientId, out var existing) || string.IsNullOrEmpty(existing))
            {
                string code = NormalizeCharacterCode(preferredCharacter);
                PlayerCharacterSelections[conn.ClientId] = string.IsNullOrEmpty(code) ? DEFAULT_CHARACTER_CODE : code;
            }

            PlayerNames[conn.ClientId] = string.IsNullOrWhiteSpace(displayName) ? $"Player {conn.ClientId}" : displayName;
            PlayerLevels[conn.ClientId] = ClampLevel(level);

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

        /// <summary>Host path: a ServerRpc called from the host never runs its logic.</summary>
        [Server]
        public void ApplyRoomName(string name)
        {
            if (GameStarted.Value) return;
            string sanitized = string.IsNullOrWhiteSpace(name) ? "Room" : name.Trim();
            if (sanitized.Length > MAX_ROOM_NAME_LENGTH) sanitized = sanitized.Substring(0, MAX_ROOM_NAME_LENGTH);
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
        public bool IsLocalPlayerReady()
        {
            if (!IsClientInitialized) return false;
            int localId = LocalConnection.ClientId;
            return PlayerReadyStates.ContainsKey(localId) && PlayerReadyStates[localId];
        }

        public string GetPlayerCharacter(int clientId)
        {
            return PlayerCharacterSelections.ContainsKey(clientId) ? PlayerCharacterSelections[clientId] : "";
        }

        public int GetReadyPlayerCount()
        {
            int count = 0;
            foreach (var kvp in PlayerReadyStates)
            {
                if (kvp.Value) count++;
            }
            return count;
        }

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
