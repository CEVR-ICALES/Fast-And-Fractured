
using Assets.SimpleLocalization.Scripts;
using FastAndFractured;
using FastAndFractured.Multiplayer;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Enums;

namespace FastAndFractured.Multiplayer
{
    public class NetworkedLevelControllerManager : NetworkBehaviour
    {
        #region MATCH_COUNTDOWN
        // The server publishes the tick the "3" appears on: every peer's countdown follows it and MatchStartGate
        // holds every car until "Start".
        private readonly FishNet.Object.Synchronizing.SyncVar<uint> _countdownStartTick = new FishNet.Object.Synchronizing.SyncVar<uint>();

        // Time for the slowest client to finish its level init after the cars spawn.
        private const float COUNTDOWN_LEAD_SECONDS = 1.5f;
        // GameReadyScript: "3" at StartEventCountdown, "Start" 3 s later - when the gate opens.
        private const float COUNTDOWN_TO_CONTROLS_SECONDS = 3f;

        // A resumed (migrated) match skips the 3-2-1; cars are released this long after the overlay drops.
        private readonly FishNet.Object.Synchronizing.SyncVar<bool> _resumedAfterMigration = new FishNet.Object.Synchronizing.SyncVar<bool>();
        private const float MIGRATION_LEAD_SECONDS = 0.75f;
        private const float MIGRATION_RESUME_SECONDS = 1f;
        private const float LOCAL_CAR_WAIT_SECONDS = 5f;
        private const float START_DATA_WAIT_SECONDS = 30f;
        private const float CAST_SPAWN_WAIT_SECONDS = 5f;
        private const float DEATH_VFX_LIFETIME_SECONDS = 5f;
        private const ushort FALLBACK_PORT = 7770;

        private bool _countdownSuppressed, _countdownSignalled, _countdownShown, _countdownRpcSent, _introStarted, _matchClockStarted;
        private void LateUpdate()
        {
            if (!_countdownSignalled)
            {
                // Cancel every root's pending 3-2-1 until the server says so: which one is ours is not settled yet.
                foreach (var script in FindObjectsByType<GameReadyScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    script.CancelInvoke("StartEventCountdown");
            }

            if (_countdownStartTick.Value == 0 || base.TimeManager == null) return;
            uint start = _countdownStartTick.Value;
            bool resumed = _resumedAfterMigration.Value;
            uint controlsTick = start + base.TimeManager.TimeToTicks(resumed ? MIGRATION_RESUME_SECONDS : COUNTDOWN_TO_CONTROLS_SECONDS);
            MatchStartGate.LockUntil(controlsTick);
            StartMatchClockAt(controlsTick);

            // The on-screen countdown follows the server's RPC: a client's own server-tick estimate lags after loading.
            if (base.IsServerInitialized && !_countdownRpcSent && base.TimeManager.Tick >= start)
            {
                _countdownRpcSent = true;
                RpcStartCountdown();
            }

            if (_countdownShown) return;
            if (resumed && _countdownSignalled && FindLocalCountdown() == null)
            {
                // No car of our own after the migration (spectating): just drop the overlay.
                _countdownShown = true;
                HostMigration.HostMigrationOverlay.Hide();
                return;
            }
            GameReadyScript countdown = FindLocalCountdown();
            if (countdown == null) return;
            if (!_countdownSuppressed)
            {
                _countdownSuppressed = true;
                countdown.CancelInvoke("StartEventCountdown");
            }
            if (!_introStarted && !resumed)
            {
                _introStarted = true;
                var car = countdown.transform.root.GetComponentInChildren<CarMovementController>();
                if (!_countdownSignalled) PreMatchSequence.Run(gameObject.scene, car != null ? car.transform : null, true, null);
            }
            if (!_countdownSignalled) return;
            _countdownShown = true;
            if (PreMatchSequence.Current != null) PreMatchSequence.Current.EndForCountdown();
            if (resumed)
            {
                HostMigration.HostMigrationOverlay.Hide();
                MpCanary.Mark($"Match resumed on screen (server tick {start})");
                return;
            }
            countdown.Invoke("StartEventCountdown", 0f);
            MpCanary.Mark($"Countdown shown (server tick {start})");
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcStartCountdown() => _countdownSignalled = true;

        // Map timers (rising lava) count from the server's "Start" tick, so every peer agrees on them.
        private void StartMatchClockAt(uint controlsTick)
        {
            if (_matchClockStarted || base.TimeManager.Tick < controlsTick || !LevelController.HasInstance) return;
            _matchClockStarted = true;
            var levelController = LevelController.Instance;
            levelController.NetworkMatchSeconds = () => base.TimeManager.Tick > controlsTick
                ? (float)base.TimeManager.TicksToTime(base.TimeManager.Tick - controlsTick) : 0f;
            levelController.StartMatchClock();
        }

        #region HOST_MIGRATION
        // Everything here is inert while HostMigrationSettings.Enabled is off (see HostMigration/).
        private int _replaySeed, _replayTotalCharacters;
        private string[] _replayHumanTokens = new string[0], _replayHumanCodes = new string[0];
        private readonly Dictionary<GameObject, int> _originalIndexByCar = new Dictionary<GameObject, int>();

        private static bool IsResumingMigratedMatch =>
            HostMigration.HostMigrationSettings.Enabled && HostMigration.HostMigrationResume.Pending.HasValue;

        /// <summary>Server: what a new host needs to replay this match's start.</summary>
        [Server]
        private void RecordMigrationReplay(List<NetworkConnection> orderedConns, int seed, int totalCharacters, int humanCount)
        {
            _replaySeed = seed;
            _replayTotalCharacters = totalCharacters;
            if (IsResumingMigratedMatch)
            {
                // Keep the ORIGINAL start, so a second migration replays the same cast again.
                var resume = HostMigration.HostMigrationResume.Pending.Value;
                _replayHumanTokens = resume.HumanTokens;
                _replayHumanCodes = resume.HumanCodes;
            }
            else
            {
                var codes = _levelControllerWrapper.GetCharacterNameCodes();
                _replayHumanTokens = new string[humanCount];
                _replayHumanCodes = new string[humanCount];
                for (int i = 0; i < humanCount; i++)
                {
                    var conn = i < orderedConns.Count ? orderedConns[i] : null;
                    string token = HostMigration.HostMigrationNetwork.TokenOf(conn);
                    _replayHumanTokens[i] = token ?? ("client-" + (conn != null ? conn.ClientId : -1));
                    _replayHumanCodes[i] = i < codes.Count ? codes[i] : string.Empty;
                }
            }

            _originalIndexByCar.Clear();
            var cars = _levelControllerWrapper.GetSpawnedCharacters();
            for (int i = 0; i < cars.Count; i++)
                if (cars[i] != null) _originalIndexByCar[cars[i]] = i;
        }

        /// <summary>Server, migrated match: keeps the reconnected humans and surviving AIs, removes the rest, and returns the connections that get a car.</summary>
        [Server]
        private List<NetworkConnection> ApplyMigrationRoster(List<NetworkConnection> orderedConns)
        {
            var resume = HostMigration.HostMigrationResume.Pending.Value;
            var tokens = new List<string>(orderedConns.Count);
            foreach (var conn in orderedConns) tokens.Add(HostMigration.HostMigrationNetwork.TokenOf(conn) ?? string.Empty);
            var plan = HostMigration.HostMigrationRoster.Build(resume, tokens);

            var cars = _levelControllerWrapper.GetSpawnedCharacters();
            var codes = _levelControllerWrapper.GetCharacterNameCodes();
            var originalCars = new List<GameObject>(cars);
            var originalCodes = new List<string>(codes);
            var keptConns = new List<NetworkConnection>();
            var keptCars = new List<GameObject>();
            var keptCodes = new List<string>();

            for (int i = 0; i < orderedConns.Count; i++)
            {
                int index = plan.HumanCharacterIndex[i];
                if (index < 0 || index >= originalCars.Count || originalCars[index] == null) continue;
                keptConns.Add(orderedConns[i]);
                keptCars.Add(originalCars[index]);
                keptCodes.Add(index < originalCodes.Count ? originalCodes[index] : string.Empty);
            }
            foreach (int index in plan.AiCharacterIndices)
            {
                if (index >= originalCars.Count || originalCars[index] == null) continue;
                keptCars.Add(originalCars[index]);
                keptCodes.Add(index < originalCodes.Count ? originalCodes[index] : string.Empty);
            }

            foreach (var car in originalCars)
            {
                if (car == null || keptCars.Contains(car)) continue;
                _originalIndexByCar.Remove(car);
                var root = car.transform.parent != null ? car.transform.parent.gameObject : car;
                Destroy(root);
            }
            cars.Clear(); cars.AddRange(keptCars);
            codes.Clear(); codes.AddRange(keptCodes);

            // Move the root, so the root-relative pose the spawn code captures stays the authored one.
            foreach (var car in keptCars)
            {
                if (!_originalIndexByCar.TryGetValue(car, out int index)) continue;
                if (!TryGetSnapshotCharacter(resume, index, out var saved)) continue;
                var root = car.transform.parent;
                if (root != null) root.position += saved.Position - car.transform.position;
                car.transform.SetPositionAndRotation(saved.Position, saved.Rotation);
            }

            MpCanary.Mark($"HostMigration: roster {keptConns.Count} human(s) + {plan.AiCharacterIndices.Count} AI kept, " +
                          $"{originalCars.Count - keptCars.Count} removed");
            return keptConns;
        }

        /// <summary>
        /// Server, migrated match: once this host's own car is started on its client side, make
        /// it the local player (see NetworkedLevelControllerWrapper.RebindLocalPlayerAfterMigration).
        /// </summary>
        private System.Collections.IEnumerator RebindLocalPlayerWhenOwned()
        {
            float waited = 0f;
            GameObject localCar = null;
            while (IsServerInitialized && waited < LOCAL_CAR_WAIT_SECONDS && (localCar = _levelControllerWrapper.FindLocalPlayerCar()) == null)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (localCar == null)
            {
                MpCanary.Mark("HostMigration: this host has no car of its own - HUD not rebound");
                yield break;
            }
            try { _levelControllerWrapper.RebindLocalPlayerAfterMigration(localCar); }
            catch (Exception e)
            {
                MpCanary.Mark($"HostMigration: rebinding the local player failed: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
            }
        }

        private System.Collections.IEnumerator ApplyMigratedStateWhenCarsAreReady()
        {
            while (IsServerInitialized && _countdownStartTick.Value != 0 && base.TimeManager.Tick < _countdownStartTick.Value)
                yield return null;
            if (IsServerInitialized) ApplyMigratedState();
        }

        /// <summary>Server, migrated match: endurance, motion, storm and map, each applied on its own.</summary>
        [Server]
        private void ApplyMigratedState()
        {
            if (!HostMigration.HostMigrationResume.Pending.HasValue) return;
            var resume = HostMigration.HostMigrationResume.Pending.Value;
            HostMigration.HostMigrationResume.Pending = null;
            HostMigration.HostMigrationNetwork.ClearSnapshot();

            int failures = 0;
            void Restore(string part, Action apply)
            {
                try { apply(); }
                catch (Exception e)
                {
                    failures++;
                    MpCanary.Mark($"HostMigration: restoring {part} failed: {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e);
                }
            }

            foreach (var kv in _originalIndexByCar)
            {
                if (kv.Key == null || !TryGetSnapshotCharacter(resume, kv.Value, out var saved)) continue;
                var car = kv.Key;
                Restore($"endurance of {car.name}", () =>
                {
                    var stats = car.GetComponentInChildren<NetworkedStatsController>();
                    var coreStats = car.GetComponentInChildren<StatsController>();
                    if (stats != null && coreStats != null && SingleplayerReflectionCache.StatsController_CurrentEndurance != null)
                    {
                        SingleplayerReflectionCache.StatsController_CurrentEndurance.SetValue(coreStats, saved.Endurance);
                        stats.ForceEnduranceSync();
                    }
                });
                Restore($"motion of {car.name}", () =>
                {
                    var rb = car.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.linearVelocity = saved.Velocity;
                        rb.angularVelocity = saved.AngularVelocity;
                    }
                });
            }
            Restore("storm", () => NetworkedSandstormController.ServerInstance?.ServerRestoreFromMigration(resume.StormStarted,
                resume.SecondsUntilStorm, resume.StormGrowth, resume.StormSpawnPoint, resume.StormMirrorPoint));
            Restore("broken props", () => NetworkedWorldBreakables.Instance?.ServerRestore(resume.BrokenWorldProps));
            Restore("platforms", () => NetworkedBreakablePlatform.ServerRestoreStages(resume.PlatformStages));
            Restore("golf balls", () => NetworkedGolfBall.ServerRestorePositions(resume.GolfBallPositions));
            Restore("pickups", () => FindFirstObjectByType<NetworkedInteractableHandler>()?.ServerRestorePool(resume.InteractablePool));
            MpCanary.Mark(failures == 0 ? "HostMigration: match resumed" : $"HostMigration: match resumed ({failures} part(s) failed)");
        }

        private static bool TryGetSnapshotCharacter(HostMigration.MatchSnapshotBroadcast snapshot, int index, out HostMigration.SnapshotCharacter character)
        {
            if (snapshot.Characters != null)
                foreach (var c in snapshot.Characters)
                    if (c.OriginalIndex == index) { character = c; return true; }
            character = default;
            return false;
        }

        /// <summary>Server: the current state of the match, for HostMigrationSnapshotSender.</summary>
        [Server]
        internal bool TryBuildMigrationSnapshot(out HostMigration.MatchSnapshotBroadcast snapshot)
        {
            snapshot = default;
            if (!IsServerInitialized || _matchEnded || _replayHumanTokens.Length == 0) return false;
            LevelController level = LevelController.ExistingInstance;
            if (level == null) return false;

            var successorTokens = new List<string>();
            var successorAddresses = new List<string>();
            foreach (var conn in GetOrderedActivePlayerConnections())
            {
                if (conn == null || conn.IsLocalClient) continue;
                string token = HostMigration.HostMigrationNetwork.TokenOf(conn);
                if (string.IsNullOrEmpty(token)) continue;
                successorTokens.Add(token);
                successorAddresses.Add(conn.GetAddress());
            }

            var characters = new List<HostMigration.SnapshotCharacter>(_originalIndexByCar.Count);
            foreach (var kv in _originalIndexByCar)
            {
                var car = kv.Key;
                var nob = car != null ? car.GetComponent<NetworkObject>() : null;
                bool alive = nob != null && _deathTracker.IsAlive(nob.ObjectId);
                var saved = new HostMigration.SnapshotCharacter { OriginalIndex = kv.Value, Alive = alive };
                if (alive)
                {
                    var rb = car.GetComponent<Rigidbody>();
                    var coreStats = car.GetComponentInChildren<StatsController>();
                    saved.Position = rb != null ? rb.position : car.transform.position;
                    saved.Rotation = rb != null ? rb.rotation : car.transform.rotation;
                    saved.Velocity = rb != null ? rb.linearVelocity : Vector3.zero;
                    saved.AngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;
                    saved.Endurance = coreStats != null ? coreStats.Endurance : 0f;
                }
                characters.Add(saved);
            }

            snapshot = new HostMigration.MatchSnapshotBroadcast
            {
                MigrationEnabled = true,
                SceneName = level.gameObject.scene.name,
                Seed = _replaySeed,
                TotalCharacters = _replayTotalCharacters,
                HumanTokens = _replayHumanTokens,
                HumanCodes = _replayHumanCodes,
                SuccessorTokens = successorTokens.ToArray(),
                SuccessorAddresses = successorAddresses.ToArray(),
                Port = MultiplayerTransport.Port != 0 ? MultiplayerTransport.Port : FALLBACK_PORT,
                Characters = characters.ToArray(),
            };
            NetworkedSandstormController.ServerInstance?.ServerDescribeForMigration(
                out snapshot.StormStarted, out snapshot.SecondsUntilStorm, out snapshot.StormGrowth,
                out snapshot.StormSpawnPoint, out snapshot.StormMirrorPoint);
            snapshot.BrokenWorldProps = NetworkedWorldBreakables.Instance != null
                ? NetworkedWorldBreakables.Instance.ServerBrokenIndices() : new int[0];
            snapshot.PlatformStages = NetworkedBreakablePlatform.DescribeStages();
            snapshot.VolumeIndex = _serverVolumeIndex;
            snapshot.GolfBallPositions = NetworkedGolfBall.DescribePositions();
            var pickups = FindFirstObjectByType<NetworkedInteractableHandler>();
            snapshot.InteractablePool = pickups != null ? pickups.ServerDescribePool() : new int[0];
            return true;
        }
        #endregion

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            MatchStartGate.Clear();
        }

        private static GameReadyScript FindLocalCountdown()
        {
            foreach (var countdown in FindObjectsByType<GameReadyScript>(FindObjectsSortMode.None))
            {
                if (!countdown.isActiveAndEnabled) continue;
                var owner = countdown.GetComponentInParent<NetworkObject>();
                if (owner == null || owner.IsOwner) return countdown;
            }
            return null;
        }
        #endregion

        private NetworkedLevelControllerWrapper _levelControllerWrapper;
        private bool _gameStartTriggered;

        private void Awake()
        {
            _levelControllerWrapper = GetComponent<NetworkedLevelControllerWrapper>();
            if (_levelControllerWrapper == null)
                _levelControllerWrapper = gameObject.AddComponent<NetworkedLevelControllerWrapper>();
            if (GetComponent<NetworkedWorldBreakables>() == null)
                gameObject.AddComponent<NetworkedWorldBreakables>();
            if (GetComponent<NetworkedLooseProps>() == null)
                gameObject.AddComponent<NetworkedLooseProps>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _gameStartTriggered = false;

            var lobbyManager = LobbyLocator.GetLobbyManager();
            Debug.Log($"[NetworkedLevelControllerManager] OnStartServer. lobby={(lobbyManager != null ? "found" : "NULL")}, " +
                      $"GameStarted={lobbyManager?.GameStarted.Value}, " +
                      $"PlayerCharacterSelections={lobbyManager?.PlayerCharacterSelections.Count}, " +
                      $"AiSlotCount={lobbyManager?.AiSlotCount.Value}");

            if (IsResumingMigratedMatch)
            {
                if (lobbyManager != null) lobbyManager.GameStarted.Value = true;
                StartGame();
                ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
                return;
            }

            if (lobbyManager != null)
            {
                lobbyManager.OnGameStarting += OnLobbyGameStarting;
                lobbyManager.OnParticipantLeftMatch += HandleParticipantLeftMatch;

                if (lobbyManager.GameStarted.Value)
                {
                    PullCharacterSelectionsFromLobby(lobbyManager);
                    StartGame();
                }
                else
                {
                    Debug.LogWarning("[NetworkedLevelControllerManager] Lobby exists but GameStarted=false. Waiting for OnGameStarting event…");
                }
            }
            else
            {
                Debug.LogError("[NetworkedLevelControllerManager] No NetworkedLobbyManager found — characters will not spawn!");
            }

            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            base.SceneManager.OnClientPresenceChangeEnd += HandleSpectatorPresence;
        }

        private void PullCharacterSelectionsFromLobby(NetworkedLobbyManager lobby)
        {
            if (lobby == null || _levelControllerWrapper == null)
            {
                Debug.LogError($"[NetworkedLevelControllerManager] PullCharacterSelectionsFromLobby skipped: " +
                               $"lobby={(lobby != null)}, wrapper={(_levelControllerWrapper != null)}");
                return;
            }

            int humansFed = 0;
            foreach (var kv in lobby.PlayerCharacterSelections)
            {
                string code = kv.Value;
                if (string.IsNullOrEmpty(code))
                    code = NetworkedLobbyManager.GetFallbackCharacterCode(humansFed);
                _levelControllerWrapper.AddCharacterSelection(kv.Key, code);
                humansFed++;
            }
            Debug.Log($"[NetworkedLevelControllerManager] Pulled {humansFed} character selections from lobby.");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _gameStartTriggered = false;

            var lobbyManager = LobbyLocator.GetLobbyManager();
            if (lobbyManager != null)
            {
                lobbyManager.OnGameStarting -= OnLobbyGameStarting;
                lobbyManager.OnParticipantLeftMatch -= HandleParticipantLeftMatch;
            }

            if (ServerManager != null)
                ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            if (base.SceneManager != null)
                base.SceneManager.OnClientPresenceChangeEnd -= HandleSpectatorPresence;
        }

        /// <summary>
        /// Called by NetworkedLobbyManager when all players are ready and countdown finishes.
        /// </summary>
        private void OnLobbyGameStarting()
        {
            StartGame();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            MatchResults.Clear();   // a new match: drop the previous one's table
        }
        
        public void AddCharacterSkin(NetworkConnection connection, string name)
        {
            if (_levelControllerWrapper != null)
            {
                _levelControllerWrapper.AddCharacterSelection(connection.ClientId, name);
                Debug.Log($"<color=cyan>Client {connection.ClientId} selected {name} </color>");
            }
            else
            {
                Debug.LogError("[NetworkedLevelControllerManager] NetworkedLevelControllerWrapper not found!");
            }
        }

        // Server-only call: a ServerRpc invoked from OnStartServer fails because the client side is not active yet.
        [Server]
        public void StartGame()
        {
            MpCanary.Mark("StartGame entered (server)");
            Debug.Log($"[NetworkedLevelControllerManager] StartGame called. IsServerInitialized={IsServerInitialized}, _gameStartTriggered={_gameStartTriggered}");

            if (!IsServerInitialized)
                return;

            if (_gameStartTriggered)
            {
                Debug.LogWarning("[NetworkedLevelControllerManager] StartGame ignored: match start already triggered.");
                return;
            }

            if (_levelControllerWrapper == null)
            {
                Debug.LogError("[NetworkedLevelControllerManager] NetworkedLevelControllerWrapper not found! Cannot start game.");
                return;
            }

            var orderedConns = GetOrderedActivePlayerConnections();
            if (orderedConns.Count == 0)
            {
                Debug.LogWarning("[NetworkedLevelControllerManager] StartGame aborted: no active player connections.");
                return;
            }

            _gameStartTriggered = true;
            int gameSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var orderedClientIds = new List<int>(orderedConns.Count);
            foreach (var conn in orderedConns)
                orderedClientIds.Add(conn.ClientId);

            int connectedPlayerCount = orderedConns.Count;
            PlayerPrefs.SetInt("Player_Num", Mathf.Max(1, connectedPlayerCount));

            _levelControllerWrapper.PruneCharacterSelectionsToActiveClients(orderedClientIds);

            int aiSlotCount = LobbyLocator.GetLobbyManager()?.AiSlotCount.Value ?? 0;
            int totalCharacterCount = Mathf.Clamp(connectedPlayerCount + aiSlotCount,
                                                  connectedPlayerCount,
                                                  NetworkedLobbyManager.MAX_PLAYERS);

            if (IsResumingMigratedMatch)
            {
                // Replay the original start exactly (same seed and human order); ApplyMigrationRoster then keeps who is left.
                var resume = HostMigration.HostMigrationResume.Pending.Value;
                gameSeed = resume.Seed;
                _migratedVolumeIndex = resume.VolumeIndex;
                connectedPlayerCount = resume.HumanCodes.Length;
                totalCharacterCount = resume.TotalCharacters;
                _levelControllerWrapper.PruneCharacterSelectionsToActiveClients(new List<int>());
                for (int i = 0; i < resume.HumanCodes.Length; i++)
                    _levelControllerWrapper.AddCharacterSelection(i, resume.HumanCodes[i]);
                MpCanary.Mark($"HostMigration: replaying the start (seed {gameSeed}, {connectedPlayerCount} humans, {totalCharacterCount} characters)");
            }

            Debug.Log($"[NetworkedLevelControllerManager] About to InitializeGame: " +
                      $"humans={connectedPlayerCount}, ai={aiSlotCount}, total={totalCharacterCount}, seed={gameSeed}");

            _levelControllerWrapper.InitializeGame(gameSeed, totalCharacterCount, connectedPlayerCount);
            RecordMigrationReplay(orderedConns, gameSeed, totalCharacterCount, connectedPlayerCount);

            var spawnedCount = _levelControllerWrapper.GetSpawnedCharacters()?.Count ?? 0;
            Debug.Log($"[NetworkedLevelControllerManager] InitializeGame done. Spawned characters in scene: {spawnedCount}");

            if (spawnedCount == 0)
            {
                Debug.LogError("[NetworkedLevelControllerManager] LevelController did not spawn ANY characters. " +
                               "Check: (a) _isInitialized was reset to false by AutoInitGate; (b) MaxCharactersInGame > 0; " +
                               "(c) spawn points are wired in LevelController; (d) PlayerPrefs Selected_Player not empty.");
            }

            // Spawn only once every client has loaded the map: spawned in the menu, Controller.Awake creates an
            // auxiliar LevelController.
            StartCoroutine(WaitForClientsThenSpawn(orderedConns, orderedClientIds, gameSeed));
        }

        private System.Collections.IEnumerator WaitForClientsThenSpawn(
            List<NetworkConnection> orderedConns, List<int> orderedClientIds, int gameSeed)
        {
            const float TIMEOUT_SECONDS = 15f;
            const float POLL_INTERVAL = 0.1f;
            float elapsed = 0f;

            while (elapsed < TIMEOUT_SECONDS)
            {
                bool allReady = true;
                foreach (var conn in orderedConns)
                {
                    if (conn == null || !conn.IsActive) continue;
                    if (conn == base.LocalConnection) continue;
                    // This map specifically: a scene left from the previous match does not count.
                    if (conn.Scenes == null || !conn.Scenes.Contains(gameObject.scene))
                    {
                        allReady = false;
                        break;
                    }
                }
                if (allReady) break;
                yield return new WaitForSeconds(POLL_INTERVAL);
                elapsed += POLL_INTERVAL;
            }

            if (elapsed >= TIMEOUT_SECONDS)
            {
                Debug.LogWarning($"[NetworkedLevelControllerManager] Timed out ({TIMEOUT_SECONDS}s) " +
                                 "waiting for all clients to load the map scene. Spawning anyway — " +
                                 "expect client-side NREs for any conn that's still in the menu.");
            }
            else
            {
                Debug.Log($"[NetworkedLevelControllerManager] All clients ready after {elapsed:F1}s. Spawning cars.");
            }

            MpCanary.Mark("WaitForClientsThenSpawn: clients ready, spawning cars");
            bool resumingMigration = IsResumingMigratedMatch;
            if (resumingMigration)
            {
                orderedConns = ApplyMigrationRoster(orderedConns);
                orderedClientIds.Clear();
                foreach (var conn in orderedConns) orderedClientIds.Add(conn.ClientId);
            }
            SpawnCharactersOnClients(orderedConns);
            MpCanary.Mark("WaitForClientsThenSpawn: cars spawned");

            // Every peer shows 3-2-1 and gets control on the same server tick (see MATCH_COUNTDOWN).
            _resumedAfterMigration.Value = resumingMigration;
            float countdownLead = resumingMigration ? MIGRATION_LEAD_SECONDS : COUNTDOWN_LEAD_SECONDS + Mathf.Max(0f, PreMatchSequence.DurationFor(gameObject.scene) - PreMatchSequence.COUNTDOWN_OVERLAP_SECONDS);
            _countdownStartTick.Value = base.TimeManager.Tick + base.TimeManager.TimeToTicks(countdownLead);
            MatchStartGate.LockUntil(_countdownStartTick.Value + base.TimeManager.TimeToTicks(resumingMigration ? MIGRATION_RESUME_SECONDS : COUNTDOWN_TO_CONTROLS_SECONDS));

            InitializePlayerTracking();

            if (resumingMigration)
            {
                StartCoroutine(RebindLocalPlayerWhenOwned());
                StartCoroutine(ApplyMigratedStateWhenCarsAreReady());
            }
            if (HostMigration.HostMigrationSettings.Enabled)
                gameObject.AddComponent<HostMigration.HostMigrationSnapshotSender>().Initialize(this);

            LoadResolvedScene();

            LevelControllerStartData levelControllerStartData = new LevelControllerStartData()
            {
                AllCharacters = _levelControllerWrapper.GetSpawnedCharacters(),
                DebugMode = _levelControllerWrapper.GetDebugMode(),
                InGameCharactersNameCodes = _levelControllerWrapper.GetCharacterNameCodes(),
                OrderedClientIds = orderedClientIds,
                RandomSeed = gameSeed
            };
            // The host runs it directly: the ObserversRpc's local execution can miss the host observer in a solo match.
            _levelControllerWrapper.InitializeHostHUD(orderedClientIds);
            SentLevelControllerStartData(levelControllerStartData);
            _startData = levelControllerStartData;
            _hasStartData = true;

            // Volume selection cannot rely on the shared DeterministicRandom stream (see VOLUME_SYNC).
            StartCoroutine(ServerSyncVolumeWhenReady());

            SpawnInteractableSyncWrapper();
        }

        /// <summary>Spawns the NetworkedInteractableHandler at runtime: scene-placed, its SceneId is never serialized.</summary>
        [Server]
        private void SpawnInteractableSyncWrapper()
        {
            if (FindFirstObjectByType<NetworkedInteractableHandler>() != null)
                return;

            var prefab = Resources.Load<GameObject>("NetworkedInteractablesSpawnable");
            if (prefab == null)
            {
                Debug.LogError("[NetworkedLevelControllerManager] Resources/NetworkedInteractablesSpawnable missing — " +
                               "interactable pool will not sync. Run Tools > Fast And Fractured > Ability Networking - Setup (Headless).");
                return;
            }

            var instance = Instantiate(prefab);
            base.ServerManager.Spawn(instance);
            Debug.Log("[NetworkedLevelControllerManager] Spawned NetworkedInteractablesSpawnable.");
        }

        #region VOLUME_SYNC
        // The RNG stream differs between peers, so the server replicates its volume index explicitly.
        private const float VOLUME_SYNC_TIMEOUT = 30f;
        private int _serverVolumeIndex = -1;     // for the host migration snapshot
        private int _migratedVolumeIndex = -1;   // set when resuming a migrated match

        private System.Collections.IEnumerator ServerSyncVolumeWhenReady()
        {
            VolumeManager vm = null;
            float elapsed = 0f;
            while ((vm = FindInitializedSceneVolumeManager()) == null && elapsed < VOLUME_SYNC_TIMEOUT)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            if (vm == null)
            {
                Debug.LogWarning("[NetworkedLevelControllerManager] No initialized VolumeManager found in the map scene; volume sync skipped.");
                yield break;
            }

            int index = vm.VolumeList != null ? vm.VolumeList.IndexOf(vm.CurrentVolumeGameObject) : -1;
            // A resumed match keeps its lighting.
            if (_migratedVolumeIndex >= 0 && vm.VolumeList != null && _migratedVolumeIndex < vm.VolumeList.Count && _migratedVolumeIndex != index)
            {
                vm.ChangeCurrentVolume(_migratedVolumeIndex);
                index = _migratedVolumeIndex;
            }
            _migratedVolumeIndex = -1;
            if (index < 0)
            {
                Debug.LogWarning("[NetworkedLevelControllerManager] Could not resolve current volume index; volume sync skipped.");
                yield break;
            }
            _serverVolumeIndex = index;

            // Brightness auto-init is suppressed by the networked scene; initialize it once the volume is active.
            InitializeBrightnessForCurrentVolume("server");

            Debug.Log($"[NetworkedLevelControllerManager] Server volume index {index} ({vm.CurrentVolumeGameObject.name}). Replicating to clients.");
            RpcApplyVolumeIndex(index);
        }

        /// <summary>The scene's VolumeManager once it has initialized; not .Instance, which would create an auxiliar one.</summary>
        private static VolumeManager FindInitializedSceneVolumeManager()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX)
                return null;

            var vm = FindAnyObjectByType<VolumeManager>();
            if (vm == null || vm.gameObject.scene != activeScene)
                return null;

            var initializedField = SingleplayerReflectionCache.AbstractAutoInit_IsInitialized;
            if (initializedField != null && !(bool)initializedField.GetValue(vm))
                return null;
            return vm;
        }

        [ObserversRpc(BufferLast = true)]
        private void RpcApplyVolumeIndex(int volumeIndex)
        {
            if (IsServerInitialized) return; // the server already shows the chosen volume
            StartCoroutine(ClientApplyVolumeIndex(volumeIndex));
        }

        private System.Collections.IEnumerator ClientApplyVolumeIndex(int volumeIndex)
        {
            VolumeManager vm = null;
            float elapsed = 0f;
            while ((vm = FindInitializedSceneVolumeManager()) == null && elapsed < VOLUME_SYNC_TIMEOUT)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            if (vm == null)
            {
                Debug.LogWarning("[NetworkedLevelControllerManager] Client volume sync timed out waiting for VolumeManager.");
                yield break;
            }

            vm.ChangeCurrentVolume(volumeIndex);
            InitializeBrightnessForCurrentVolume("client");
            Debug.Log($"[NetworkedLevelControllerManager] Applied replicated volume index {volumeIndex}.");
        }

        private static void InitializeBrightnessForCurrentVolume(string peer)
        {
            // Not BrightnessManager.Instance: it can create an auxiliar object.
            var brightnessManager = BrightnessManager.ExistingInstance;
            if (brightnessManager == null)
            {
                Debug.LogWarning($"[NetworkedLevelControllerManager] No existing BrightnessManager found on {peer}; brightness initialization skipped.");
                return;
            }

            brightnessManager.ForceInitialize();
            Debug.Log($"[NetworkedLevelControllerManager] Brightness initialized after applying the {peer} volume.");
        }
        #endregion

        #region SPECTATORS
        // A spectator loads the map late and missed SentLevelControllerStartData: send it to that client again.
        private LevelControllerStartData _startData;
        private bool _hasStartData;

        private void HandleSpectatorPresence(FishNet.Managing.Scened.ClientPresenceChangeEventArgs args)
        {
            if (!args.Added || args.Connection == null || args.Scene != gameObject.scene) return;
            var lobby = LobbyLocator.GetLobbyManager();
            if (lobby == null || !lobby.MatchSpectators.Contains(args.Connection.ClientId)) return;
            StartCoroutine(SendSpectatorStart(args.Connection));
        }

        [Server]
        private System.Collections.IEnumerator SendSpectatorStart(NetworkConnection conn)
        {
            // Cars still spawning (spectator during the countdown) reach it with the next observer rebuild.
            for (float waited = 0f; !_hasStartData && waited < START_DATA_WAIT_SECONDS; waited += Time.unscaledDeltaTime)
                yield return null;
            yield return new WaitForSeconds(SPECTATOR_SPAWN_GRACE);
            if (!_hasStartData || conn == null || !conn.IsActive || _matchEnded) yield break;
            MpCanary.Mark($"Spectator {conn.ClientId} joined the match");
            TargetSpectatorStart(conn, _startData);
        }

        /// <summary>Time for the cars' spawn messages to reach a spectator before the start data.</summary>
        private const float SPECTATOR_SPAWN_GRACE = 0.5f;

        [TargetRpc]
        private void TargetSpectatorStart(NetworkConnection conn, LevelControllerStartData data)
        {
            StartCoroutine(ClientStartSpectating(data));
        }

        private System.Collections.IEnumerator ClientStartSpectating(LevelControllerStartData data)
        {
            // Every car of the cast must exist here before the singleplayer level is set up.
            for (float waited = 0f; waited < CAST_SPAWN_WAIT_SECONDS && !AllPresent(data.AllCharacters); waited += Time.unscaledDeltaTime)
                yield return null;
            var cast = data.AllCharacters != null ? data.AllCharacters.FindAll(c => c != null) : new List<GameObject>();
            try
            {
                _levelControllerWrapper.InitializeSpectatorState(cast, data.InGameCharactersNameCodes, data.RandomSeed);
            }
            catch (Exception e) { Debug.LogException(e); }
            MatchSpectatorCamera.Begin(cast);

            static bool AllPresent(List<GameObject> characters)
            {
                if (characters == null) return true;
                foreach (var c in characters) if (c == null) return false;
                return true;
            }
        }
        #endregion

        [ObserversRpc]
        void SentLevelControllerStartData(LevelControllerStartData levelControllerStartData)
        {
            // The host initializes directly in WaitForClientsThenSpawn; this RPC is for remote clients.
            if (!IsServerInitialized && _levelControllerWrapper != null)
            {
                _levelControllerWrapper.InitializeClientState(
                    levelControllerStartData.AllCharacters,
                    levelControllerStartData.InGameCharactersNameCodes,
                    levelControllerStartData.OrderedClientIds,
                    levelControllerStartData.RandomSeed
                );
            }
        }
        void SpawnCharactersOnClients(List<NetworkConnection> orderedConnections)
        {
            if (_levelControllerWrapper == null)
            {
                Debug.LogError("[NetworkedLevelControllerManager] NetworkedLevelControllerWrapper not found!");
                return;
            }
            
            List<GameObject> charactersToSpawn = new List<GameObject>(_levelControllerWrapper.GetSpawnedCharacters());
            if (orderedConnections == null)
                orderedConnections = GetOrderedActivePlayerConnections();
            int assignedPlayers = 0;

            // Car -> character code, to replicate the skin. Both lists are filled in lockstep by CharacterSpawner.
            var skinNameCodeByCar = new Dictionary<GameObject, string>();
            {
                var originalCars = _levelControllerWrapper.GetSpawnedCharacters();
                var nameCodes = _levelControllerWrapper.GetCharacterNameCodes();
                for (int i = 0; i < originalCars.Count && i < nameCodes.Count; i++)
                    if (originalCars[i] != null)
                        skinNameCodeByCar[originalCars[i]] = nameCodes[i];
            }

            Debug.Log($"[NetworkedLevelControllerManager] SpawnCharactersOnClients: " +
                      $"connections={orderedConnections.Count} [{string.Join(",", orderedConnections.ConvertAll(c => c.ClientId))}], " +
                      $"charactersToSpawn={charactersToSpawn.Count}");

            foreach (var conn in orderedConnections)
            {
                if (assignedPlayers < charactersToSpawn.Count)
                {
                    // One failing car must not leave every later player without one.
                    try
                    {
                        GameObject playerVehicleGO = charactersToSpawn[assignedPlayers];
                        if (playerVehicleGO == null)
                        {
                            Debug.LogError($"[NetworkedLevelControllerManager] player car #{assignedPlayers} is null for clientId={conn.ClientId}.");
                            assignedPlayers++;
                            continue;
                        }

                        NetworkObject playerVehicleNob = playerVehicleGO.GetComponent<NetworkObject>();
                        Transform parentT = playerVehicleGO.transform.parent;
                        NetworkObject rootPlayer = parentT != null ? parentT.GetComponent<NetworkObject>() : null;
                        var netVehicle = playerVehicleGO.GetComponent<NetworkedVehicle>();
                        // Capture the authored root-relative pose before Spawn changes the parenting.
                        Vector3 vehicleLocalPosition = playerVehicleGO.transform.localPosition;
                        Quaternion vehicleLocalRotation = playerVehicleGO.transform.localRotation;
                        // Spawn the two NetworkObjects separately: nested, a remote owner initializes the Rigidbody at the prefab origin.
                        playerVehicleGO.transform.SetParent(null, worldPositionStays: true);

                        Debug.Log($"[NetworkedLevelControllerManager] clientId={conn.ClientId}: " +
                                  $"car='{playerVehicleGO.name}', nob={(playerVehicleNob != null)}, " +
                                  $"parent={(parentT != null ? parentT.name : "NULL")}, rootNob={(rootPlayer != null)}, " +
                                  $"netVehicle={(netVehicle != null)}, connScenes={conn.Scenes.Count}");

                        if (rootPlayer != null) Spawn(rootPlayer, conn);
                        if (playerVehicleNob != null) Spawn(playerVehicleNob, conn);
                        if (playerVehicleNob != null && rootPlayer != null)
                            AttachSpawnedGameobjectToParent(playerVehicleNob, rootPlayer,
                                vehicleLocalPosition,
                                vehicleLocalRotation);
                        if (netVehicle != null) netVehicle.InjectInputProvider();

                        // After Spawn, so the SyncVar propagates.
                        ApplyReplicatedSkin(playerVehicleGO, skinNameCodeByCar);

                        Debug.Log($"[NetworkedLevelControllerManager] Spawned player car for clientId={conn.ClientId}.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[NetworkedLevelControllerManager] FAILED to spawn car for clientId={conn.ClientId}: " +
                                       $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                    assignedPlayers++;
                }
                else
                {
                    Debug.LogWarning($"Not enough player vehicles spawned by LevelController for all connected clients. ClientId {conn.ClientId} will not get a car.");
                }
            }

            if (assignedPlayers > 0 && assignedPlayers <= charactersToSpawn.Count)
            {
                charactersToSpawn.RemoveRange(0, assignedPlayers);
            }

            foreach (GameObject aiVehicleGO in charactersToSpawn)
            {
                NetworkObject aiVehicleNob = aiVehicleGO.GetComponent<NetworkObject>();
                var rootPlayer = aiVehicleNob.transform.parent.gameObject.GetComponent<NetworkObject>();
                Vector3 vehicleLocalPosition = aiVehicleGO.transform.localPosition;
                Quaternion vehicleLocalRotation = aiVehicleGO.transform.localRotation;
                aiVehicleGO.transform.SetParent(null, worldPositionStays: true);

                if (rootPlayer != null)
                {
                    SetupAINetworking(aiVehicleGO);
                    
                    Spawn(rootPlayer);
                    Spawn(aiVehicleNob);
                    AttachSpawnedGameobjectToParent(aiVehicleNob, rootPlayer,
                        vehicleLocalPosition,
                        vehicleLocalRotation);

                    ApplyReplicatedSkin(aiVehicleGO, skinNameCodeByCar);
                }
                else
                {
                    Debug.LogError($"AI Vehicle '{aiVehicleGO.name}' created by LevelController is missing a NetworkObject component. It cannot be spawned.");
                }
            }
        }

        private static void ApplyReplicatedSkin(GameObject carGO, Dictionary<GameObject, string> skinNameCodeByCar)
        {
            if (carGO == null) return;
            if (!skinNameCodeByCar.TryGetValue(carGO, out string nameCode) || string.IsNullOrEmpty(nameCode))
                return;

            var stats = carGO.GetComponent<NetworkedStatsController>();
            if (stats == null) stats = carGO.GetComponentInChildren<NetworkedStatsController>();
            if (stats != null)
                stats.SetSkinNameCode(nameCode);
        }

        private List<NetworkConnection> GetOrderedActivePlayerConnections()
        {
            var ordered = new List<NetworkConnection>();
            var seenClientIds = new HashSet<int>();

            // Only the players the room sent into this match.
            var lobby = LobbyLocator.GetLobbyManager();
            bool onlyParticipants = lobby != null && lobby.MatchParticipants.Count > 0 && !IsResumingMigratedMatch;

            foreach (var conn in base.ServerManager.Clients.Values)
            {
                if (conn == null || !conn.IsActive)
                    continue;
                if (onlyParticipants && !lobby.MatchParticipants.Contains(conn.ClientId))
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

        /// <summary>Adds NetworkedAIShoot to AI vehicles at spawn (players and AI share the prefabs).</summary>
        private void SetupAINetworking(GameObject aiVehicleGO)
        {
            // The brain sits on the (possibly inactive) wrapper above the vehicle.
            var aiBrain = aiVehicleGO.GetComponentInParent<EnemyAIBrain>(true);
            if (aiBrain == null)
                aiBrain = aiVehicleGO.GetComponentInChildren<EnemyAIBrain>(true);
            if (aiBrain == null) return;

            var targeting = aiBrain.GetComponent<NetworkedAiPlayerTargeting>();
            if (targeting == null)
                targeting = aiBrain.gameObject.AddComponent<NetworkedAiPlayerTargeting>();
            targeting.Configure(aiBrain);

            var playerShoot = aiVehicleGO.GetComponentInChildren<NetworkedShoot>(true);
            
            var aiShoot = aiVehicleGO.GetComponent<NetworkedAIShoot>();
            if (aiShoot == null)
            {
                aiShoot = aiVehicleGO.AddComponent<NetworkedAIShoot>();
                Debug.Log($"<color=cyan>[NetworkedLevelControllerManager] Added NetworkedAIShoot to AI '{aiVehicleGO.name}'</color>");
            }
            
            // Reuse NetworkedShoot's networked projectile variants (the pool holds the singleplayer prefabs).
            if (playerShoot != null)
                playerShoot.ConfigureAiShoot(aiShoot);
            else
                Debug.LogError($"[NetworkedLevelControllerManager] AI '{aiVehicleGO.name}' has no NetworkedShoot projectile configuration.", aiVehicleGO);
            
            if (playerShoot != null)
                playerShoot.enabled = false;

            foreach (var localShoot in aiVehicleGO.GetComponentsInChildren<LocalShootController>(true))
                localShoot.enabled = false;
        }

        [ObserversRpc(RunLocally = true)]
        public void AttachSpawnedGameobjectToParent(NetworkObject child, NetworkObject newParent,
            Vector3 authoritativeLocalPosition, Quaternion authoritativeLocalRotation)
        {

            // Observers instantiate the child unparented: apply the exact local pose captured on the server.
            child.SetParent(newParent);
            child.transform.SetLocalPositionAndRotation(
                authoritativeLocalPosition, authoritativeLocalRotation);
            newParent.name = $"{newParent.name} {newParent.OwnerId}";
        }
        
        #region GAME_LOOP_AND_DEATH_TRACKING
        private int _charactersAlive => _deathTracker.AliveCount;

        /// <summary>Death/alive bookkeeping keyed by ObjectId; the logic lives in MatchDeathTracker (tested).</summary>
        private readonly MatchDeathTracker _deathTracker = new MatchDeathTracker();

        /// <summary>References captured at registration: the winner can be despawned before the match resolves.</summary>
        private readonly Dictionary<int, NetworkObject> _trackedCharacterNobs = new Dictionary<int, NetworkObject>();

        private bool _matchEnded;

        /// <summary>Server: notifies the result once, then reopens the room for the next match.</summary>
        [Server]
        private void EndMatchOnServer(NetworkObject winner)
        {
            Debug.Log($"<color=magenta>[NetworkedLevelControllerManager] EndMatchOnServer entered. " +
                      $"winner='{(winner != null ? winner.gameObject.name : "DRAW")}', " +
                      $"_matchEnded(before)={_matchEnded}, _charactersAlive={_charactersAlive}</color>");

            if (_matchEnded)
            {
                Debug.LogWarning("[NetworkedLevelControllerManager] EndMatchOnServer ignored: already ended.");
                return;
            }
            _matchEnded = true;
            StartCoroutine(RevealThenEndGame(winner));
        }

        /// <summary>Seconds between the last decisive death and the win/lose screen, so the explosion can be seen.</summary>
        internal const float END_OF_MATCH_REVEAL_DELAY = 3f;

        /// <summary>Server: every human is dead while AIs still fight; the host can skip the rest.</summary>
        public bool OnlyAiAlive => base.IsServerInitialized && !_matchEnded &&
                                   _deathTracker.AliveCount > 1 && !_deathTracker.AnyPlayerAlive;

        /// <summary>Server: ends an AI-only match now, with one of the surviving AIs as winner.</summary>
        [Server]
        public void ServerSkipAiOnlyMatch()
        {
            if (!OnlyAiAlive) return;
            int winnerId = _deathTracker.FirstAliveObjectId();
            _trackedCharacterNobs.TryGetValue(winnerId, out NetworkObject winner);
            MpCanary.Mark($"Host skipped the AI-only end of the match (winner {winnerId})");
            EndMatchOnServer(winner);
        }

        [Server]
        private System.Collections.IEnumerator RevealThenEndGame(NetworkObject winner)
        { 
            try { MatchResultsRpc(BuildMatchResults(winner)); }
            catch (Exception e) { Debug.LogException(e); }
            MatchDecidedRpc(winner);

            _revealEndsAt = Time.time + END_OF_MATCH_REVEAL_DELAY;
            while (Time.time < _revealEndsAt) yield return null;
            EndGameRpc(winner);
            Debug.Log("<color=magenta>[NetworkedLevelControllerManager] EndGameRpc dispatched. The room stays open for the next match.</color>");

            // The room outlives the match: everyone goes to the win/lose screen and back to the room.
            LobbyLocator.GetLobbyManager()?.ServerEndMatch();
            MpCanary.Mark("Match over - room open for the next match");
        }

        #region MATCH_RESULTS
        // End-of-match table for the multiplayer win/lose screen (MatchResults / MultiplayerResultsPanel).
        private readonly Dictionary<int, MatchResults.Participant> _resultsParticipants = new Dictionary<int, MatchResults.Participant>();
        private readonly List<MatchResults.Death> _deathLog = new List<MatchResults.Death>();

        [Server]
        private void RegisterResultsParticipant(int objectId, int? ownerClientId, string characterCode)
        {
            bool human = ownerClientId.HasValue;
            string name = human
                ? (LobbyLocator.GetLobbyManager()?.GetPlayerName(ownerClientId.Value) ?? $"Player {ownerClientId.Value}")
                : AiResultsName(objectId, characterCode);
            _resultsParticipants[objectId] = new MatchResults.Participant
            {
                Id = objectId,
                DisplayName = name,
                CharacterCode = characterCode,
                IsHuman = human,
                OwnerClientId = human ? ownerClientId.Value : -1,
            };
        }

        /// <summary>The bot's name, as the room listed it and the kill feed shows it.</summary>
        private string AiResultsName(int objectId, string characterCode)
        {
            string botName = _trackedCharacterNobs.TryGetValue(objectId, out var nob) && nob != null ? MatchNames.Of(nob.gameObject) : null;
            return string.IsNullOrEmpty(botName) ? $"{MatchResults.CharacterName(characterCode)} (AI)" : botName;
        }

        /// <summary>Tracked character id of a killer, or -1 (no killer, or not a character).</summary>
        [Server]
        private int FindTrackedId(NetworkObject nob)
        {
            if (nob == null) return -1;
            if (_deathTracker.IsKnown(nob.ObjectId)) return nob.ObjectId;
            foreach (var parent in nob.GetComponentsInParent<NetworkObject>(true))
                if (_deathTracker.IsKnown(parent.ObjectId)) return parent.ObjectId;
            foreach (var child in nob.GetComponentsInChildren<NetworkObject>(true))
                if (_deathTracker.IsKnown(child.ObjectId)) return child.ObjectId;
            if (nob.Owner != null && nob.Owner.IsValid && _deathTracker.TryGetCharacterForClient(nob.Owner.ClientId, out int id))
                return id;
            return -1;
        }

        [Server]
        private MatchResultEntry[] BuildMatchResults(NetworkObject winner)
        {
            var participants = new List<MatchResults.Participant>(_resultsParticipants.Count);
            foreach (var kv in _resultsParticipants)
            {
                var p = kv.Value;
                if (_trackedCharacterNobs.TryGetValue(kv.Key, out var nob) && nob != null)
                {
                    var stats = nob.GetComponentInChildren<NetworkedStatsController>(true) ?? nob.GetComponentInParent<NetworkedStatsController>();
                    if (stats != null) p.DamageDealt = Mathf.RoundToInt(stats.TotalDamageDealt.Value);
                }
                participants.Add(p);
            }
            var entries = MatchResults.Build(participants, _deathLog, FindTrackedId(winner));
            MpCanary.Mark($"Match results: {string.Join(" | ", System.Array.ConvertAll(entries, e => $"#{e.Placement} {e.DisplayName} k{e.Kills}"))}");
            return entries;
        }

        [ObserversRpc]
        private void MatchResultsRpc(MatchResultEntry[] entries)
        {
            MatchResults.Store(entries, base.LocalConnection != null ? base.LocalConnection.ClientId : -1);
        }
        #endregion

        
        /// <summary>Server: called by NetworkedStatsController when any character dies.</summary>
        [Server]
        public void ReportDeathOnServer(NetworkObject victim, NetworkObject killer)
        {
            if (!IsServerInitialized) return;
            
            int victimId = ResolveTrackedVictimId(victim);
            if (victimId == -1)
            {
                // Loud on purpose: an untracked death means the match can never end.
                Debug.LogError($"[NetworkedLevelControllerManager] ReportDeathOnServer could not map victim " +
                               $"'{(victim != null ? victim.gameObject.name : "null")}' (ObjectId=" +
                               $"{(victim != null ? victim.ObjectId : -1)}) to any tracked character. " +
                               $"Tracker: {_deathTracker.DescribeTracked()}");
                return;
            }

            var deathResult = _deathTracker.ReportDeath(victimId);
            if (!deathResult.Applied)
            {
                Debug.Log($"[NetworkedLevelControllerManager] Duplicate death report for ObjectId {victimId} ignored.");
                return;
            }
            _deathLog.Add(new MatchResults.Death { VictimId = victimId, KillerId = FindTrackedId(killer) });

            // Every death brings the storm forward, as GameLoopManager.OnCharacterDied does in singleplayer.
            NetworkedSandstormController.ServerInstance?.ServerAdvanceStormForDeath();

            string victimName = victim != null ? victim.gameObject.name : "Unknown";
            string killerName = killer != null ? killer.gameObject.name : "Unknown";
            Debug.Log($"<color=orange>[NetworkedLevelControllerManager] Death: {killerName} eliminated {victimName}. Characters alive: {_deathTracker.AliveCount}</color>");

            HandleDeathOutcome(deathResult);
        }

        /// <summary>Maps the NetworkObject a death came through (car or player root) to the tracked character id.</summary>
        [Server]
        private int ResolveTrackedVictimId(NetworkObject victim)
        {
            if (victim == null) return -1;

            if (_deathTracker.IsKnown(victim.ObjectId))
                return victim.ObjectId;

            foreach (var nob in victim.GetComponentsInParent<NetworkObject>(true))
                if (_deathTracker.IsKnown(nob.ObjectId))
                    return nob.ObjectId;

            foreach (var nob in victim.GetComponentsInChildren<NetworkObject>(true))
                if (_deathTracker.IsKnown(nob.ObjectId))
                    return nob.ObjectId;

            if (victim.Owner != null && victim.Owner.IsValid &&
                _deathTracker.TryGetCharacterForClient(victim.Owner.ClientId, out int mappedId))
                return mappedId;

            return -1;
        }

        /// <summary>Ends the match with the winner, or as a draw when the winner cannot be resolved.</summary>
        [Server]
        private void HandleDeathOutcome(MatchDeathTracker.DeathResult deathResult)
        {
            switch (deathResult.MatchOutcome)
            {
                case MatchDeathTracker.Outcome.Winner:
                    NetworkObject winner = null;
                    if (deathResult.WinnerObjectId != -1)
                    {
                        _trackedCharacterNobs.TryGetValue(deathResult.WinnerObjectId, out winner);
                        if (winner == null)
                            base.ServerManager.Objects.Spawned.TryGetValue(deathResult.WinnerObjectId, out winner);
                    }

                    if (winner == null)
                        Debug.LogError($"[NetworkedLevelControllerManager] Winner ObjectId {deathResult.WinnerObjectId} " +
                                       "could not be resolved to a spawned NetworkObject. Ending match as draw. " +
                                       $"Tracker: {_deathTracker.DescribeTracked()}");
                    EndMatchOnServer(winner);
                    break;

                case MatchDeathTracker.Outcome.Draw:
                    Debug.LogWarning("[NetworkedLevelControllerManager] No characters alive! Game ending in draw.");
                    EndMatchOnServer(null);
                    break;
            }
        }

        [Server]
        public void InitializePlayerTracking()
        {
            _deathTracker.Reset();
            _trackedCharacterNobs.Clear();
            _resultsParticipants.Clear();
            _deathLog.Clear();

            var allCharacters = LevelController.Instance.InGameCharacters;
            var nameCodes = LevelController.Instance.InGameCharactersNameCodes;

            for (int characterIndex = 0; characterIndex < allCharacters.Count; characterIndex++)
            {
                var charGO = allCharacters[characterIndex];
                if (charGO == null) continue;

                var nob = charGO.GetComponent<NetworkObject>();
                if (nob == null) nob = charGO.GetComponentInParent<NetworkObject>();

                if (nob != null)
                {
                    int? ownerClientId = (nob.Owner != null && nob.Owner.IsValid) ? nob.Owner.ClientId : (int?)null;
                    _deathTracker.RegisterCharacter(nob.ObjectId, ownerClientId);
                    _trackedCharacterNobs[nob.ObjectId] = nob;
                    RegisterResultsParticipant(nob.ObjectId, ownerClientId,
                        nameCodes != null && characterIndex < nameCodes.Count ? nameCodes[characterIndex] : string.Empty);
                }
                else
                {
                    Debug.LogError($"[NetworkedLevelControllerManager] Character '{charGO.name}' has no NetworkObject " +
                                   "in itself or its parents — its death can never be tracked and the match may never end.");
                }
            }

            Debug.Log($"<color=cyan>[NetworkedLevelControllerManager] Tracking initialized for {_deathTracker.AliveCount} characters " +
                      $"({_deathTracker.PlayerCount} players + {_deathTracker.AliveCount - _deathTracker.PlayerCount} AI). " +
                      $"{_deathTracker.DescribeTracked()}</color>");
        }

        /// <summary>A client disconnecting mid-match: its car explodes and counts as a death.</summary>
        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            RemovePlayerFromMatch(conn.ClientId);
        }

        /// <summary>A player went back to the room mid-match: for the match it is the same as leaving.</summary>
        private void HandleParticipantLeftMatch(NetworkConnection conn)
        {
            if (conn == null) return;
            RemovePlayerFromMatch(conn.ClientId);

            // An owner always observes its own objects: despawn the car, as a disconnect would.
            var owned = new List<NetworkObject>(conn.Objects);
            owned.Sort((a, b) => Depth(a).CompareTo(Depth(b)));   // parents first; they take their nested objects along
            foreach (var nob in owned)
                if (nob != null && nob.IsSpawned && nob.gameObject.scene == gameObject.scene)
                    base.ServerManager.Despawn(nob);

            static int Depth(NetworkObject nob)
            {
                int depth = 0;
                for (var t = nob != null ? nob.transform : null; t != null; t = t.parent) depth++;
                return depth;
            }
        }

        /// <summary>The player's car explodes and counts as a death, unless it was already dead.</summary>
        private void RemovePlayerFromMatch(int clientId)
        {
            if (!IsServerInitialized || !_gameStartTriggered) return;

            if (!_deathTracker.TryGetCharacterForClient(clientId, out int characterObjectId))
                return;

            if (!_deathTracker.IsAlive(characterObjectId))
            {
                Debug.Log($"[NetworkedLevelControllerManager] Client {clientId} disconnected but their character was already dead.");
                _deathTracker.ForgetClient(clientId);
                return;
            }

            Debug.Log($"<color=orange>[NetworkedLevelControllerManager] Client {clientId} disconnected during active game. Killing their character (ObjectId: {characterObjectId}).</color>");
            ForceKillDisconnectedPlayer(clientId, characterObjectId);
        }

        [Server]
        private void ForceKillDisconnectedPlayer(int clientId, int characterObjectId)
        {
            _deathTracker.ForgetClient(clientId);

            // Still spawned: FishNet fires OnRemoteConnectionState before it despawns the owner's objects.
            NetworkObject victimNob = null;
            base.ServerManager.Objects.Spawned.TryGetValue(characterObjectId, out victimNob);

            string victimName = LobbyLocator.GetLobbyManager()?.GetPlayerName(clientId) ?? $"Player {clientId}";

            DisconnectDeathRpc(victimNob, victimName);

            // Tracked directly: the victim's NetworkObject may be gone once FishNet processes the disconnect.
            var deathResult = _deathTracker.ReportDeath(characterObjectId);
            if (deathResult.Applied)
            {
                _deathLog.Add(new MatchResults.Death { VictimId = characterObjectId, KillerId = -1 });
                Debug.Log($"<color=orange>[NetworkedLevelControllerManager] Disconnect death: {victimName}. Characters alive: {_deathTracker.AliveCount}</color>");
                HandleDeathOutcome(deathResult);
            }

            // FishNet despawns the owned objects after this event returns.
        }

        [ObserversRpc]
        private void DisconnectDeathRpc(NetworkObject victim, string victimName)
        {
            Debug.Log($"<color=magenta>[NetworkedLevelControllerManager] DISCONNECT DEATH: {victimName} has been eliminated (disconnected)</color>");

            if (victim != null)
            {
                var statsCtrl = victim.GetComponent<NetworkedStatsController>();
                if (statsCtrl == null)
                    statsCtrl = victim.GetComponentInChildren<NetworkedStatsController>();
                if (statsCtrl != null)
                {
                    var vfxField = typeof(NetworkedStatsController).GetField("dieExplosionVfx",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (vfxField != null)
                    {
                        var deathVfxPrefab = vfxField.GetValue(statsCtrl) as GameObject;
                        if (deathVfxPrefab != null)
                        {
                            var vfx = Instantiate(deathVfxPrefab, victim.transform.position, Quaternion.identity);
                            Destroy(vfx, DEATH_VFX_LIFETIME_SECONDS);
                        }
                    }
                }

                if (LevelController.Instance != null && LevelController.Instance.characterIcons != null)
                {
                    int victimObjectId = victim.ObjectId;
                    foreach (var icon in LevelController.Instance.characterIcons)
                    {
                        if (icon == null || icon.Character == null) continue;
                        var iconNob = icon.Character.GetComponentInParent<NetworkObject>(true);
                        if (iconNob == null) iconNob = icon.Character.GetComponentInChildren<NetworkObject>(true);
                        if (iconNob != null && iconNob.ObjectId == victimObjectId)
                        {
                            icon.SetPlayerDeadIconIsActive(true);
                            break;
                        }
                    }
                }
            }

            if (HUDManager.HasInstance)
            {
                HUDManager.Instance.UpdateUIElement(UIDynamicElementType.EVENT_TEXT, LocalizationManager.Localize("MP.Feed.Disconnected", victimName));
            }

            NetworkedSpectatorController.Instance?.OnAnyCharacterDied(victim);
        }

        private NetworkObject GetLastCharacterAlive()
        {
            foreach (int aliveId in _deathTracker.AliveObjectIds)
            {
                if (_trackedCharacterNobs.TryGetValue(aliveId, out NetworkObject tracked) && tracked != null)
                    return tracked;
                if (base.ServerManager.Objects.Spawned.TryGetValue(aliveId, out NetworkObject nob))
                    return nob;
            }
            return null;
        }
        
        private float _revealEndsAt;
        private bool _matchEndShown;

        /// <summary>Server: the host asked for the result during the reveal; everyone gets it now.</summary>
        [Server]
        private void ServerEndRevealNow() => _revealEndsAt = 0f;

        [ObserversRpc]
        private void MatchDecidedRpc(NetworkObject winner)
        {
             MultiplayerRoomReturn.ShowDecidedResult = base.IsServerInitialized
                ? (System.Action)ServerEndRevealNow
                : () => ShowMatchEnd(winner);
        }

        [ObserversRpc]
        private void EndGameRpc(NetworkObject winner) => ShowMatchEnd(winner);

        private void ShowMatchEnd(NetworkObject winner)
        {
            if (_matchEndShown) return;
            _matchEndShown = true;
            MultiplayerRoomReturn.ShowDecidedResult = null;

            // The match is over: losing the host from now on is a room matter, not a match one.
            HostDisconnectWatchdog.NotifyMatchEnded();

            // Left during the last seconds (Esc while the end was on its way): already heading to the room.
            if (MultiplayerRoomReturn.LeftMatch) return;

            if (MatchSpectatorCamera.IsSpectating)
            {
                // Watched, not played: no result of its own, straight back to the room.
                MatchSpectatorCamera.End();
                MultiplayerSceneTeardown.LoadMainMenu(ScreensType.LOBBY_ROOM);
                return;
            }

            // Restore the freelook camera before the win/lose screen.
            NetworkedSpectatorController.Instance?.DeactivateSpectator();

            bool iWon = winner != null && winner.IsOwner;
            
            if (iWon)
            {
                Debug.Log("<color=green>🏆 YOU WON! 🏆</color>");
                ShowWinLoseScreen(true);
            }
            else if (winner != null)
            {
                Debug.Log("<color=red>☠ You lost. Better luck next time!</color>");
                ShowWinLoseScreen(false);
            }
            else
            {
                Debug.Log("<color=gray>DRAW - No winner</color>");
                ShowWinLoseScreen(false);
            }
        }

        /// <summary>
        /// Shows the local win/lose screen the singleplayer way: fill GameEndData and load the main menu, which
        /// shows it when finalAnimation is set. Also used by a spectator who leaves early.
        /// </summary>
        public void ShowWinLoseScreen(bool isWin)
        {
            GameplayEvents.RaiseMatchEnded(isWin);
            var gameEndData = ResolveGameEndData();
            if (gameEndData == null)
            {
                Debug.LogError("[NetworkedLevelControllerManager] Could not resolve GameEndData asset — " +
                               "cannot show win/lose screen. Falling back to plain MainMenu load.");
                LoadMainMenuLocally();
                return;
            }

            gameEndData.isWin = isWin;

            var localStats = FindLocalStats();
            if (localStats != null)
            {
                gameEndData.totalDamageDealt      = Mathf.RoundToInt(localStats.TotalDamageDealt.Value).ToString();
                gameEndData.totalDamageTaken      = Mathf.RoundToInt(localStats.TotalDamageTaken.Value).ToString();
                gameEndData.totalDistanceTraveled = Mathf.RoundToInt(localStats.TotalDistanceTraveled.Value).ToString();
            }
            else
            {
                gameEndData.totalDamageDealt      = "0";
                gameEndData.totalDamageTaken      = "0";
                gameEndData.totalDistanceTraveled = "0";
            }
            // Kills come from the match table, sent just before EndGameRpc.
            gameEndData.killCount = MatchResults.TryGetLocal(out var myResult) ? myResult.Kills.ToString() : "0";

            // A non-null finalAnimation makes the main menu open on the win/lose screen.
            string characterCode = ResolveLocalCharacterCode();
            gameEndData.finalAnimation = ResolveFinalAnimation(localStats, characterCode, isWin);
            LevelUtilities.ParseCharacterNameCode(characterCode, out _, out int skin);
            gameEndData.finalAnimationSkin = skin;

            MultiplayerPostMatchFlow.ArmAfterMultiplayerMatch(InstanceFinder.IsServerStarted);

            LoadMainMenuLocally();
        }

        private GameEndData ResolveGameEndData()
        {
            var lc = LevelController.HasInstance ? LevelController.Instance : FindObjectOfType<LevelController>(true);
            if (lc == null) return null;
            var field = SingleplayerReflectionCache.LevelController_GameEndData;
            return field?.GetValue(lc) as GameEndData;
        }

        /// <summary>The local character code from the spawn data (not the shared PlayerPrefs, which a clone may overwrite).</summary>
        private string ResolveLocalCharacterCode()
        {
            string code = _levelControllerWrapper != null ? _levelControllerWrapper.LocalPlayerNameCode : null;
            return string.IsNullOrEmpty(code) ? PlayerPrefs.GetString("Selected_Player", "Pepe_0") : code;
        }

        private static GameObject ResolveFinalAnimation(NetworkedStatsController localStats, string characterCode, bool isWin)
        {
            var coreStats = localStats != null ? localStats.GetComponent<StatsController>() : null;
            return coreStats != null ? coreStats.GetFinalAnimation(characterCode, isWin) : null;
        }

        /// <summary>Local load of the main menu, marked as an intentional leave for HostDisconnectWatchdog.</summary>
        private void LoadMainMenuLocally()
        {
            HostDisconnectWatchdog.NotifyMatchEnded();
            MultiplayerSceneTeardown.LoadMainMenu();
        }

        [Server]
        private void LoadResolvedScene()
        {
            var lobbyManager = LobbyLocator.GetLobbyManager();
            int resolvedMapId = lobbyManager?.ResolvedMapId.Value ?? -1;
            if (resolvedMapId < 0) return;

            var catalog = FastAndFractured.Multiplayer.Lobby.MapCatalogLoader.Get();
            if (catalog == null) return;

            if (!catalog.TryGetById(resolvedMapId, out var entry) || string.IsNullOrEmpty(entry.SceneName))
            {
                Debug.LogWarning($"[NetworkedLevelControllerManager] ResolvedMapId {resolvedMapId} not found or has no SceneName. Using current scene.");
                return;
            }

            string sceneToLoad = entry.GetSceneNameForMultiplayer();

            // The lobby already loaded the map; loading it again (ReplaceScenes.All) destroys the spawned cars.
            // Check every loaded scene by name: the active scene is not updated synchronously.
            var unitySm = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (loaded.name == sceneToLoad)
                {
                    Debug.Log($"[NetworkedLevelControllerManager] LoadResolvedScene: '{sceneToLoad}' " +
                              $"is already loaded (active='{unitySm.name}'). Skipping redundant load.");
                    return;
                }
            }

            Debug.Log($"[NetworkedLevelControllerManager] LoadResolvedScene: loading '{sceneToLoad}' " +
                      $"(active='{unitySm.name}', no matching loaded scene found).");

            var sceneLoadData = new FishNet.Managing.Scened.SceneLoadData(sceneToLoad)
            {
                ReplaceScenes = FishNet.Managing.Scened.ReplaceOption.All
            };
            InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
        }

        private NetworkedStatsController FindLocalStats()
        {
            if (LocalConnection != null)
            {
                foreach (var nob in LocalConnection.Objects)
                {
                    var stats = nob.GetComponentInChildren<NetworkedStatsController>();
                    if (stats != null)
                    {
                        return stats;
                    }
                }
            }
            return null;
        }
        #endregion
    }

    [Serializable]
    public struct LevelControllerStartData
    {
        public List<GameObject> AllCharacters;
        public bool DebugMode;
        public List<string> InGameCharactersNameCodes;
        public List<int> OrderedClientIds;
        public int RandomSeed;
    }
}

