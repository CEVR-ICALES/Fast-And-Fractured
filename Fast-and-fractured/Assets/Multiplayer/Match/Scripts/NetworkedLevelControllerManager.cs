
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
        // The 3-2-1 (GameReadyScript) and the moment the cars get control (GameLoopManager's
        // delayUntilGameStarts timer) both started on each peer when ITS level finished
        // initialising, and a remote client finishes later than the host - so the countdowns
        // did not match, and the host's cars could drive during it (playtest feedback 2026-09-30).
        // The server publishes the tick the "3" appears on: the on-screen countdown follows its RPC
        // and MatchStartGate holds every car until "Start".
        private readonly FishNet.Object.Synchronizing.SyncVar<uint> _countdownStartTick = new FishNet.Object.Synchronizing.SyncVar<uint>();

        // Time for the slowest client to finish its level init after the cars spawn.
        private const float COUNTDOWN_LEAD_SECONDS = 1.5f;
        // GameReadyScript: "3" at StartEventCountdown, "Start" 3 s later - when the gate opens.
        private const float COUNTDOWN_TO_CONTROLS_SECONDS = 3f;

        // A match resumed after a host migration skips the 3-2-1: the frozen-frame overlay goes
        // away at the start tick and the cars are released this long after (players see where
        // they are first).
        private readonly FishNet.Object.Synchronizing.SyncVar<bool> _resumedAfterMigration = new FishNet.Object.Synchronizing.SyncVar<bool>();
        private const float MIGRATION_LEAD_SECONDS = 0.75f;
        private const float MIGRATION_RESUME_SECONDS = 1f;

        private bool _countdownSuppressed, _countdownSignalled, _countdownShown, _countdownRpcSent;
        private void LateUpdate()
        {
            if (_countdownStartTick.Value == 0 || base.TimeManager == null) return;
            uint start = _countdownStartTick.Value;
            bool resumed = _resumedAfterMigration.Value;
            MatchStartGate.LockUntil(start + base.TimeManager.TimeToTicks(resumed ? MIGRATION_RESUME_SECONDS : COUNTDOWN_TO_CONTROLS_SECONDS));

            // The server says when the "3" appears. A remote client's own estimate of the server
            // tick lagged ~1 s right after loading the map (measured 2026-09-30), so the on-screen
            // countdown follows the server's RPC, not that estimate.
            if (base.IsServerInitialized && !_countdownRpcSent && base.TimeManager.Tick >= start)
            {
                _countdownRpcSent = true;
                RpcStartCountdown();
            }

            if (_countdownShown) return;
            if (!_countdownSignalled)
            {
                // Until the server says so, no 3-2-1 may run anywhere. Each player root's
                // GameReadyScript schedules one when it initialises, and which one is "ours" is
                // not settled yet early on (a migrated host only owns its car a few frames after
                // spawn): cancelling just the one found first left another running behind the
                // migration overlay, and its "1" showed when the match resumed.
                foreach (var script in FindObjectsByType<GameReadyScript>(FindObjectsSortMode.None))
                    script.CancelInvoke("StartEventCountdown");
            }
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
                // This peer's own init scheduled the countdown at its own time; wait for the server.
                _countdownSuppressed = true;
                countdown.CancelInvoke("StartEventCountdown");
            }
            if (!_countdownSignalled) return;
            _countdownShown = true;
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

        /// <summary>
        /// Server, migrated match: keep the characters that are still in the match - each
        /// reconnected human with its own car, the surviving AIs - and remove the rest (the host
        /// that left, the dead, anyone who did not come back) before anything is network-spawned.
        /// Returns the connections that get a car, in the order the cars were reordered to.
        /// </summary>
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

            // Put each kept car where it was when the host was lost (moving its root, so the
            // root-relative pose the spawn code captures stays the authored one).
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
        /// Server, migrated match: restore the saved state once the cars have initialised (when
        /// the 3-2-1 starts; they are held by MatchStartGate until "Start"). Applying it in the
        /// spawn frame threw from the cars' endurance VFX, which were not set up yet, and the
        /// exception also cut the rest of the match start short (playtest 2026-10-04).
        /// </summary>
        /// <summary>
        /// Server, migrated match: once this host's own car is started on its client side, make
        /// it the local player (see NetworkedLevelControllerWrapper.RebindLocalPlayerAfterMigration).
        /// </summary>
        private System.Collections.IEnumerator RebindLocalPlayerWhenOwned()
        {
            float waited = 0f;
            GameObject localCar = null;
            while (IsServerInitialized && waited < 5f && (localCar = _levelControllerWrapper.FindLocalPlayerCar()) == null)
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

        /// <summary>
        /// Server, migrated match: endurance, motion, the storm and the map. Each part is applied
        /// on its own, so one failing does not leave the others unrestored.
        /// </summary>
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

            var transport = base.NetworkManager.TransportManager.Transport;
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
                Port = transport != null ? transport.GetPort() : (ushort)7770,
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
                // Host migration: this peer took over a match in progress; rebuild it now.
                if (lobbyManager != null) lobbyManager.GameStarted.Value = true;
                StartGame();
                ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
                return;
            }

            if (lobbyManager != null)
            {
                lobbyManager.OnGameStarting += OnLobbyGameStarting;

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
                lobbyManager.OnGameStarting -= OnLobbyGameStarting;

            if (ServerManager != null)
                ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
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

        // Server-only — called from OnStartServer and OnLobbyGameStarting (both server contexts).
        // Was a [ServerRpc] historically but invoking it from OnStartServer on a freshly-spawned
        // NetworkObject crashes ("Cannot complete action because client is not active") because
        // FishNet routes the call through the client-side which isn't initialized yet.
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
                // Replay the original start exactly (same seed, same human characters in the same
                // order): the singleplayer spawner then rebuilds the same cast in the same order.
                // ApplyMigrationRoster later keeps only who is still in the match.
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

            // Defer the actual networked spawn + RPC until every remote client has finished
            // loading the map scene. Otherwise FishNet spawns the NetworkObjects on the client
            // while it's still in the menu scene; Controller.Awake() then calls
            // LevelController.Instance, which doesn't exist yet, creates a broken auxiliar
            // singleton, and the auto-init cascade NREs in StartLevelWithSpawnedCharacters.
            // (FishNet itself warns: "spawned but it's recommended to not spawn objects for
            // connections until they have loaded start scenes".)
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
                    // The host's local connection is already in scene by definition.
                    if (conn == base.LocalConnection) continue;
                    if (conn.Scenes == null || conn.Scenes.Count == 0)
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
            _countdownStartTick.Value = base.TimeManager.Tick + base.TimeManager.TimeToTicks(resumingMigration ? MIGRATION_LEAD_SECONDS : COUNTDOWN_LEAD_SECONDS);
            MatchStartGate.LockUntil(_countdownStartTick.Value + base.TimeManager.TimeToTicks(resumingMigration ? MIGRATION_RESUME_SECONDS : COUNTDOWN_TO_CONTROLS_SECONDS));

            // Initialize player tracking
            InitializePlayerTracking();

            if (resumingMigration)
            {
                StartCoroutine(RebindLocalPlayerWhenOwned());
                StartCoroutine(ApplyMigratedStateWhenCarsAreReady());
            }
            if (HostMigration.HostMigrationSettings.Enabled)
                gameObject.AddComponent<HostMigration.HostMigrationSnapshotSender>().Initialize(this);

            // Load the voted map scene (idempotent, skips if already loaded)
            LoadResolvedScene();

            // Send game data to clients
            LevelControllerStartData levelControllerStartData = new LevelControllerStartData()
            {
                AllCharacters = _levelControllerWrapper.GetSpawnedCharacters(),
                DebugMode = _levelControllerWrapper.GetDebugMode(),
                InGameCharactersNameCodes = _levelControllerWrapper.GetCharacterNameCodes(),
                OrderedClientIds = orderedClientIds,
                RandomSeed = gameSeed
            };
            // Host path runs DIRECTLY, never through the ObserversRpc. The RPC's local execution on
            // the host depends on the host observer being registered by the time it is sent; in a
            // solo-host match that race is lost and the RPC body never ran locally, so
            // InitializeHostHUD (guard registration, state-machine takeover, HUD binding) silently
            // skipped — the same class of bug as the ServerRpc-from-host no-op on car NOBs.
            _levelControllerWrapper.InitializeHostHUD(orderedClientIds);
            SentLevelControllerStartData(levelControllerStartData);

            // Volume selection cannot rely on the shared DeterministicRandom stream (see VOLUME_SYNC).
            StartCoroutine(ServerSyncVolumeWhenReady());

            SpawnInteractableSyncWrapper();
        }

        /// <summary>
        /// Spawns the NetworkedInteractableHandler as a runtime NetworkObject. Scene-placing it
        /// (what the migration tool briefly did) leaves the NetworkObject with an unserialized
        /// SceneId and FishNet refuses to initialize it — same pitfall LobbyServerBootstrapper
        /// documents for the lobby manager, same Resources-prefab solution.
        /// </summary>
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
        // VolumeManager.Initialize picks a volume with DeterministicRandom. The RNG stream order
        // diverges between peers (the server consumes extra randoms for AI/spawn logic, and a
        // client that initializes before the networked seed arrives falls back to a random seed
        // in LevelController), so host and clients end up with different volumes. The server
        // replicates its chosen index explicitly instead.
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
            // A match resumed after a host migration keeps the lighting it had: the new host's
            // own pick is random and the scene visibly changed colour on resuming.
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

            // Brightness auto-initialization is suppressed only by the network-map scene
            // overrides. Initialize it now that the server's authoritative volume is active,
            // before publishing that same selection to observers.
            InitializeBrightnessForCurrentVolume("server");

            Debug.Log($"[NetworkedLevelControllerManager] Server volume index {index} ({vm.CurrentVolumeGameObject.name}). Replicating to clients.");
            RpcApplyVolumeIndex(index);
        }

        /// <summary>
        /// Finds the game-scene VolumeManager once it has finished its own Initialize (so a later
        /// auto-init won't overwrite what we apply). Uses FindAnyObjectByType instead of
        /// VolumeManager.Instance because the singleton getter would create a broken auxiliary
        /// instance while the scene copy isn't awake yet.
        /// </summary>
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
            // Never use BrightnessManager.Instance here: if scene teardown/load ordering is
            // unusual, the singleton getter creates an auxiliary object and recreates the race
            // this network-only initialization path is intended to avoid.
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

        [ObserversRpc]
        void SentLevelControllerStartData(LevelControllerStartData levelControllerStartData)
        {
            // Host initialization happens directly in WaitForClientsThenSpawn (see the comment
            // there) — this RPC only initializes remote clients.
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
            
            // Get spawned characters from singleton via wrapper
            List<GameObject> charactersToSpawn = new List<GameObject>(_levelControllerWrapper.GetSpawnedCharacters());
            if (orderedConnections == null)
                orderedConnections = GetOrderedActivePlayerConnections();
            int assignedPlayers = 0;

            // Map each car GameObject to its character name code so we can replicate the skin.
            // GetSpawnedCharacters() (InGameCharacters) and GetCharacterNameCodes()
            // (InGameCharactersNameCodes) are filled in lockstep by CharacterSpawner, so index i
            // of one matches index i of the other. Keyed by GameObject so it survives the
            // charactersToSpawn.RemoveRange below.
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
                    // Per-car guard: a failure spawning one player's car must NOT abort the loop,
                    // or every player after the failing one silently gets no car.
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
                        // FishNet detaches nested NetworkObjects while spawning them. Capture the
                        // authored root-relative pose before either Spawn call can change parenting.
                        Vector3 vehicleLocalPosition = playerVehicleGO.transform.localPosition;
                        Quaternion vehicleLocalRotation = playerVehicleGO.transform.localRotation;
                        // Spawn the two NetworkObjects independently. Leaving the vehicle nested
                        // makes Spawn(root) recursively spawn it; on a remote owner FishNet then
                        // initializes the predicted Rigidbody at the prefab origin instead of the
                        // server world pose. Re-parent through FishNet only after both are spawned.
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

                        // Replicate the skin (see NetworkedStatsController._skinNameCode). Must run
                        // after Spawn so the SyncVar propagates.
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
                    // Setup AI networking components before spawn
                    SetupAINetworking(aiVehicleGO);
                    
                    Spawn(rootPlayer);
                    Spawn(aiVehicleNob);
                    AttachSpawnedGameobjectToParent(aiVehicleNob, rootPlayer,
                        vehicleLocalPosition,
                        vehicleLocalRotation);

                    // Replicate the AI's skin so remote clients show the same one as the host.
                    ApplyReplicatedSkin(aiVehicleGO, skinNameCodeByCar);
                }
                else
                {
                    Debug.LogError($"AI Vehicle '{aiVehicleGO.name}' created by LevelController is missing a NetworkObject component. It cannot be spawned.");
                }
            }
        }

        /// <summary>
        /// Server-side: pushes a spawned car's character name code onto its NetworkedStatsController
        /// so the skin replicates to every client (see NetworkedStatsController._skinNameCode).
        /// </summary>
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

            foreach (var conn in base.ServerManager.Clients.Values)
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

        /// <summary>
        /// Dynamically adds NetworkedAIShoot to AI vehicles at spawn time.
        /// This allows using the same prefabs for players and AI.
        /// </summary>
        private void SetupAINetworking(GameObject aiVehicleGO)
        {
            // The brain lives on the NetworkedEnemyAI wrapper above the vehicle and may be
            // inactive during setup. Searching only active children made every AI take the
            // player/local shooting path instead.
            var aiBrain = aiVehicleGO.GetComponentInParent<EnemyAIBrain>(true);
            if (aiBrain == null)
                aiBrain = aiVehicleGO.GetComponentInChildren<EnemyAIBrain>(true);
            if (aiBrain == null) return;

            var targeting = aiBrain.GetComponent<NetworkedAiPlayerTargeting>();
            if (targeting == null)
                targeting = aiBrain.gameObject.AddComponent<NetworkedAiPlayerTargeting>();
            targeting.Configure(aiBrain);

            var playerShoot = aiVehicleGO.GetComponentInChildren<NetworkedShoot>(true);
            
            // Add NetworkedAIShoot if not present
            var aiShoot = aiVehicleGO.GetComponent<NetworkedAIShoot>();
            if (aiShoot == null)
            {
                aiShoot = aiVehicleGO.AddComponent<NetworkedAIShoot>();
                Debug.Log($"<color=cyan>[NetworkedLevelControllerManager] Added NetworkedAIShoot to AI '{aiVehicleGO.name}'</color>");
            }
            
            // Reuse the character-specific networked variants already serialized on
            // NetworkedShoot. ObjectPoolManager contains the single-player prefabs and is not
            // a valid source for server-spawned NetworkObjects.
            if (playerShoot != null)
                playerShoot.ConfigureAiShoot(aiShoot);
            else
                Debug.LogError($"[NetworkedLevelControllerManager] AI '{aiVehicleGO.name}' has no NetworkedShoot projectile configuration.", aiVehicleGO);
            
            // Disable NetworkedShoot if present (player component, not for AI)
            if (playerShoot != null)
                playerShoot.enabled = false;

            foreach (var localShoot in aiVehicleGO.GetComponentsInChildren<LocalShootController>(true))
                localShoot.enabled = false;
        }

        [ObserversRpc(RunLocally = true)]
        public void AttachSpawnedGameobjectToParent(NetworkObject child, NetworkObject newParent,
            Vector3 authoritativeLocalPosition, Quaternion authoritativeLocalRotation)
        {

            // The child NetworkObject is instantiated unparented on observers. Keeping its
            // temporary world position (the default SetParent behaviour) converts that world
            // pose into a huge local offset from the correctly spawned NetworkedPlayer root.
            // Its Rigidbody may advance before this RPC arrives, so the observer's temporary
            // local pose is unsafe too. Apply the exact local pose captured on the server.
            child.SetParent(newParent);
            child.transform.SetLocalPositionAndRotation(
                authoritativeLocalPosition, authoritativeLocalRotation);
            newParent.name = $"{newParent.name} {newParent.OwnerId}";
        }
        
        #region GAME_LOOP_AND_DEATH_TRACKING
        /// <summary>
        /// Total characters (Players + AI) alive. Decrements with each death.
        /// </summary>
        private int _charactersAlive => _deathTracker.AliveCount;

        /// <summary>
        /// Death/alive bookkeeping (players + AI, keyed by NetworkObject.ObjectId).
        /// Pure logic lives in MatchDeathTracker so EditMode tests cover the win condition.
        /// </summary>
        private readonly MatchDeathTracker _deathTracker = new MatchDeathTracker();

        /// <summary>
        /// Direct NetworkObject references captured at registration. Resolving the winner via
        /// ServerManager.Objects.Spawned fails when the winning car was despawned in the same
        /// window (e.g. its owner disconnected right as the match ended) — a live match ended
        /// as "draw" because of exactly that. Keyed by the same ObjectId the tracker uses.
        /// </summary>
        private readonly Dictionary<int, NetworkObject> _trackedCharacterNobs = new Dictionary<int, NetworkObject>();

        private bool _matchEnded;

        /// <summary>Seconds the win/lose screen stays up before the server tears down.</summary>
        private const float SERVER_SHUTDOWN_DELAY = 8f;

        /// <summary>
        /// Server-side match end: notify clients (win/lose screen) once, then schedule a server
        /// shutdown so the host doesn't keep running an empty match. Guarded so multiple
        /// near-simultaneous deaths can't double-fire.
        /// </summary>
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

        /// <summary>
        /// Seconds between the last decisive death and the win/lose screen. With two players the
        /// screen used to appear on the same frame as the kill, before the explosion could even be
        /// seen (playtest feedback 2026-09-30). The map stays loaded meanwhile, so a coroutine on
        /// this component is safe here.
        /// </summary>
        internal const float END_OF_MATCH_REVEAL_DELAY = 3f;

        /// <summary>
        /// Server: every human is dead but AIs are still fighting. The host can then skip the
        /// rest (playtest feedback 2026-09-30) instead of watching bots until one is left.
        /// </summary>
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
            yield return new WaitForSeconds(END_OF_MATCH_REVEAL_DELAY);

            // Before EndGameRpc (same reliable channel, so it arrives first): the win/lose screen
            // reads it when the menu loads.
            try { MatchResultsRpc(BuildMatchResults(winner)); }
            catch (Exception e) { Debug.LogException(e); }
            EndGameRpc(winner);
            Debug.Log($"<color=magenta>[NetworkedLevelControllerManager] EndGameRpc dispatched. " +
                      $"Scheduling server teardown in {SERVER_SHUTDOWN_DELAY}s…</color>");

            // CANNOT run the teardown as a coroutine on THIS component: the host's own
            // EndGameRpc handler loads the MainMenu seconds from now, which unloads the map
            // scene and destroys this spawned NetworkObject — the coroutine dies before ever
            // reaching StopConnection, leaving the server (and its advertised room) running.
            // That is exactly the "host can't create another room after a match" bug. Run it
            // on a DontDestroyOnLoad runner that survives the scene swap instead.
            MatchTeardownRunner.ScheduleStop(SERVER_SHUTDOWN_DELAY);
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
                : $"{MatchResults.CharacterName(characterCode)} (AI)";
            _resultsParticipants[objectId] = new MatchResults.Participant
            {
                Id = objectId,
                DisplayName = name,
                CharacterCode = characterCode,
                IsHuman = human,
                OwnerClientId = human ? ownerClientId.Value : -1,
            };
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

        /// <summary>Scene-swap-proof server teardown after the win/lose screen delay.</summary>
        private sealed class MatchTeardownRunner : MonoBehaviour
        {
            public static void ScheduleStop(float delay)
            {
                var go = new GameObject("[MatchTeardown]");
                DontDestroyOnLoad(go);
                var runner = go.AddComponent<MatchTeardownRunner>();
                runner.StartCoroutine(runner.StopAfter(delay));
            }

            private System.Collections.IEnumerator StopAfter(float delay)
            {
                MpCanary.Mark($"MatchTeardown scheduled ({delay}s)");
                yield return new WaitForSeconds(delay);

                Debug.Log($"<color=magenta>[MatchTeardown] Match ended — stopping server now. " +
                          $"IsClientStarted={InstanceFinder.IsClientStarted}, IsServerStarted={InstanceFinder.IsServerStarted}</color>");
                // Mark the upcoming client/server stop as intentional so the watchdog doesn't
                // fire its "host disconnected" path on the host's own client side.
                HostDisconnectWatchdog.NotifyIntentionalLeave();
                HostDisconnectWatchdog.NotifyMatchEnded();
                if (InstanceFinder.ClientManager != null && InstanceFinder.IsClientStarted)
                {
                    Debug.Log("[MatchTeardown] Calling ClientManager.StopConnection()");
                    InstanceFinder.ClientManager.StopConnection();
                }
                if (InstanceFinder.ServerManager != null && InstanceFinder.IsServerStarted)
                {
                    Debug.Log("[MatchTeardown] Calling ServerManager.StopConnection(true)");
                    InstanceFinder.ServerManager.StopConnection(true);
                }
                MpCanary.Mark("MatchTeardown executed (server/client stopped)");
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Server-side death tracking entry point. Called by NetworkedStatsController.HandleServerSideDeath
        /// when any character (Player or AI) dies. Was previously a [ServerRpc] but the only callers
        /// already run on the server, so the RPC roundtrip was unnecessary.
        /// </summary>
        [Server]
        public void ReportDeathOnServer(NetworkObject victim, NetworkObject killer)
        {
            if (!IsServerInitialized) return;
            
            int victimId = ResolveTrackedVictimId(victim);
            if (victimId == -1)
            {
                // This used to fail silently — a death that doesn't match the tracked set means
                // the match can never end. Make it loud so a prefab/hierarchy change that moves
                // the NetworkObject the death is reported through gets caught immediately.
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

        /// <summary>
        /// Maps the NetworkObject a death was reported through to the tracked character id.
        /// Deaths come from NetworkedStatsController (on the car), while tracking registered
        /// whatever NetworkObject InGameCharacters resolved to — depending on prefab layout
        /// these can be the car's nested NOB or the player root's. Try both directions plus
        /// the owner mapping before giving up.
        /// </summary>
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

        /// <summary>
        /// Applies a tracker outcome: ends the match with the winner, or — when the winner's
        /// NetworkObject can't be resolved — still ends it as a draw instead of hanging forever
        /// with the server open (the old code silently skipped EndMatchOnServer in that case).
        /// </summary>
        [Server]
        private void HandleDeathOutcome(MatchDeathTracker.DeathResult deathResult)
        {
            switch (deathResult.MatchOutcome)
            {
                case MatchDeathTracker.Outcome.Winner:
                    NetworkObject winner = null;
                    if (deathResult.WinnerObjectId != -1)
                    {
                        // Registration-time reference first (survives Spawned-dictionary removal
                        // races); Spawned lookup as fallback.
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

        /// <summary>
        /// Initializes character tracking at start. Includes both players and AI.
        /// </summary>
        [Server]
        public void InitializePlayerTracking()
        {
            _deathTracker.Reset();
            _trackedCharacterNobs.Clear();
            _resultsParticipants.Clear();
            _deathLog.Clear();

            // Use the singleton's InGameCharacters list which contains both Players and AI
            var allCharacters = LevelController.Instance.InGameCharacters;
            var nameCodes = LevelController.Instance.InGameCharactersNameCodes;

            for (int characterIndex = 0; characterIndex < allCharacters.Count; characterIndex++)
            {
                var charGO = allCharacters[characterIndex];
                if (charGO == null) continue;

                var nob = charGO.GetComponent<NetworkObject>();
                // If not found on GO, try parent (vehicles are children of player root)
                if (nob == null) nob = charGO.GetComponentInParent<NetworkObject>();

                if (nob != null)
                {
                    // Map player connections to their character ObjectId for disconnect handling
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

        /// <summary>
        /// Handles remote client connection state changes. When a client disconnects
        /// during an active game, their car explodes and counts as a death.
        /// </summary>
        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (!IsServerInitialized || !_gameStartTriggered) return;
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;

            int clientId = conn.ClientId;

            if (!_deathTracker.TryGetCharacterForClient(clientId, out int characterObjectId))
                return;

            // Check if this character is still alive (not already dead)
            if (!_deathTracker.IsAlive(characterObjectId))
            {
                Debug.Log($"[NetworkedLevelControllerManager] Client {clientId} disconnected but their character was already dead.");
                _deathTracker.ForgetClient(clientId);
                return;
            }

            Debug.Log($"<color=orange>[NetworkedLevelControllerManager] Client {clientId} disconnected during active game. Killing their character (ObjectId: {characterObjectId}).</color>");
            ForceKillDisconnectedPlayer(clientId, characterObjectId);
        }

        /// <summary>
        /// Forces a disconnected player's character to die: spawns death VFX on all clients,
        /// marks character icon as dead, and reports the death to game loop tracking.
        /// </summary>
        [Server]
        private void ForceKillDisconnectedPlayer(int clientId, int characterObjectId)
        {
            _deathTracker.ForgetClient(clientId);

            // The character NetworkObject should still be spawned at this point
            // (FishNet fires OnRemoteConnectionState before ClientDisconnected despawns objects).
            NetworkObject victimNob = null;
            base.ServerManager.Objects.Spawned.TryGetValue(characterObjectId, out victimNob);

            string victimName = victimNob != null ? victimNob.gameObject.name : $"Player_{clientId}";

            // Notify all clients to play death effects (VFX + icon + killfeed)
            DisconnectDeathRpc(victimNob, victimName);

            // Track the death directly (can't route through ReportDeathServerRpc
            // because victimNob may become null after FishNet processes the disconnect)
            var deathResult = _deathTracker.ReportDeath(characterObjectId);
            if (deathResult.Applied)
            {
                _deathLog.Add(new MatchResults.Death { VictimId = characterObjectId, KillerId = -1 });
                Debug.Log($"<color=orange>[NetworkedLevelControllerManager] Disconnect death: {victimName}. Characters alive: {_deathTracker.AliveCount}</color>");
                HandleDeathOutcome(deathResult);
            }

            // Don't manually despawn — FishNet's ClientDisconnected will handle
            // despawning owned objects after this event returns.
        }

        /// <summary>
        /// ObserversRpc to play disconnect death effects on all clients.
        /// The victim NetworkObject is still valid when this RPC is sent because
        /// FishNet fires OnRemoteConnectionState before despawning owned objects.
        /// </summary>
        [ObserversRpc]
        private void DisconnectDeathRpc(NetworkObject victim, string victimName)
        {
            Debug.Log($"<color=magenta>[NetworkedLevelControllerManager] DISCONNECT DEATH: {victimName} has been eliminated (disconnected)</color>");

            // Spawn death VFX using the character's own VFX prefab
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
                            Destroy(vfx, 5f);
                        }
                    }
                }

                // Mark character icon as dead
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

            // Show kill feed message
            if (HUDManager.HasInstance)
            {
                HUDManager.Instance.UpdateUIElement(UIDynamicElementType.EVENT_TEXT, $"{victimName} disconnected");
            }

            // Notify spectator to refresh targets if the disconnected character was being spectated.
            NetworkedSpectatorController.Instance?.OnAnyCharacterDied(victim);
        }

        /// <summary>
        /// Finds the last character standing.
        /// </summary>
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
        
        /// <summary>
        /// Notifies all clients that the game has ended.
        /// </summary>
        [ObserversRpc]
        private void EndGameRpc(NetworkObject winner)
        {
            // Remote clients are about to receive a server-side StopConnection in ~8s
            // (SERVER_SHUTDOWN_DELAY). Without this notification, the resulting client-stop
            // would trigger HostDisconnectWatchdog's "host disconnected" return-to-hub flow
            // on TOP of the WinLoseScreen — they'd never get to see who won.
            HostDisconnectWatchdog.NotifyMatchEnded();
            HostDisconnectWatchdog.NotifyIntentionalLeave();

            string winnerName = winner != null ? winner.gameObject.name : "DRAW";

            // Tear down spectator camera before showing win/lose screen so the freelook camera
            // is restored to its original target (avoids the screen showing a spectated vehicle).
            NetworkedSpectatorController.Instance?.DeactivateSpectator();

            // Check if I am the winner
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
        /// Shows the end-of-match win/lose screen for the LOCAL client. Rather than trying to
        /// activate a WinLoseScreenBehaviour on the map scene (there is none — it lives in the
        /// MainMenu, and touching its .Instance getter spawns a broken "auxiliar" singleton whose
        /// null serialized fields NRE in OnEnable → nobody sees the screen), we replicate exactly
        /// what the singleplayer GameEndHandler does:
        ///   1. Write the shared GameEndData asset (isWin, stats, finalAnimation).
        ///   2. Load the MainMenu scene locally.
        /// On MainMenu load, StartMode.ExecuteAfterMainMenuManager() sees a non-null finalAnimation
        /// and shows the real WinLoseScreen. This is the proven SP path, now reused for MP.
        /// </summary>
        // Public: also invoked by NetworkedSpectatorController.LeaveMatch so a spectating loser
        // who quits early gets the same defeat screen + MainMenu flow as a normal match end.
        public void ShowWinLoseScreen(bool isWin)
        {
            var gameEndData = ResolveGameEndData();
            if (gameEndData == null)
            {
                Debug.LogError("[NetworkedLevelControllerManager] Could not resolve GameEndData asset — " +
                               "cannot show win/lose screen. Falling back to plain MainMenu load.");
                LoadMainMenuLocally();
                return;
            }

            gameEndData.isWin = isWin;

            // Stats from the local player's networked stats controller.
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
            // Kills were never filled in multiplayer (the screen showed the default): take them
            // from the match table, which the server sent just before EndGameRpc.
            gameEndData.killCount = MatchResults.TryGetLocal(out var myResult) ? myResult.Kills.ToString() : "0";

            // finalAnimation drives StartMode: a non-null value makes the MainMenu show the
            // win/lose stats screen instead of the normal menu. Resolve the character-specific
            // win or lose object from the local player's StatsController (same source SP uses).
            gameEndData.finalAnimation = ResolveFinalAnimation(localStats, isWin);

            // WIN_LOSE screen buttons are SP defaults; arm the MP override so "retry" routes
            // back to the multiplayer hub (host recreates the room, clients browse for it).
            MultiplayerPostMatchFlow.ArmAfterMultiplayerMatch(InstanceFinder.IsServerStarted);

            LoadMainMenuLocally();
        }

        /// <summary>Reflection into LevelController.gameEndDataScriptableObject (private SP field).</summary>
        private GameEndData ResolveGameEndData()
        {
            var lc = LevelController.HasInstance ? LevelController.Instance : FindObjectOfType<LevelController>(true);
            if (lc == null) return null;
            var field = SingleplayerReflectionCache.LevelController_GameEndData;
            return field?.GetValue(lc) as GameEndData;
        }

        /// <summary>
        /// The character-specific win/lose animation prefab, pulled from the local player's
        /// StatsController the same way GameEndHandler does (GetWinObjectByString / GetLoseObjectByString
        /// keyed by a character name code). The code comes from the wrapper's LocalPlayerNameCode
        /// (the character the local player ACTUALLY drove, from authoritative spawn data) — NOT
        /// from PlayerPrefs, which editor clones share through the registry, so any peer's write
        /// can clobber the local value mid-match (observed: host played Pepe, read "Josefino_0",
        /// got null animation → no win screen). Falls back to prefs, then to the first available
        /// win/lose object so the screen always has SOMETHING to show for the played character.
        /// </summary>
        private GameObject ResolveFinalAnimation(NetworkedStatsController localStats, bool isWin)
        {
            var coreStats = localStats != null ? localStats.GetComponent<StatsController>() : null;
            if (coreStats == null) return null;

            string code = _levelControllerWrapper != null ? _levelControllerWrapper.LocalPlayerNameCode : null;
            if (string.IsNullOrEmpty(code))
                code = PlayerPrefs.GetString("Selected_Player", "Pepe_0");

            var animation = isWin ? coreStats.GetWinObjectByString(code) : coreStats.GetLoseObjectByString(code);
            if (animation == null)
            {
                // Exact key miss (e.g. stale/mismatched code): fall back to the first configured
                // object so StartMode still shows the win/lose screen instead of the plain menu.
                var candidates = isWin ? coreStats.WinObjects : coreStats.LoseObjects;
                if (candidates != null)
                {
                    foreach (var pair in candidates)
                    {
                        if (pair != null && pair.GameObject != null)
                        {
                            Debug.LogWarning($"[NetworkedLevelControllerManager] No {(isWin ? "win" : "lose")} animation for code '{code}' — " +
                                             $"falling back to '{pair.StringValue}'.");
                            animation = pair.GameObject;
                            break;
                        }
                    }
                }
            }
            return animation;
        }

        /// <summary>
        /// Local (non-networked) load of the MainMenu scene. Marks the intentional leave so the
        /// HostDisconnectWatchdog doesn't also fire its "host disconnected" path when the client
        /// stops shortly after.
        /// </summary>
        private void LoadMainMenuLocally()
        {
            HostDisconnectWatchdog.NotifyMatchEnded();
            HostDisconnectWatchdog.NotifyIntentionalLeave();
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

            // Modern flow: NetworkedLobbyManager.LoadMapSceneAfterAnimation already loaded the
            // map scene before this manager was even spawned. We must NOT load it again because
            // ReplaceScenes=All would Destroy the cars we just spawned a few frames ago.
            //
            // The old check (sceneToLoad == ActiveScene.name) is unreliable: FishNet's scene
            // transitions don't always synchronously update Unity's "active scene" before this
            // runs from OnStartServer, so the comparison can falsely say "different" and trigger
            // a second load that nukes the freshly-instantiated cars (causing a
            // MissingReferenceException in LevelController.OnGameStartDelayComplete a few seconds
            // later when the delay timer iterates InGameCharacters).
            //
            // Iterate ALL loaded scenes by name as the authoritative source.
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

