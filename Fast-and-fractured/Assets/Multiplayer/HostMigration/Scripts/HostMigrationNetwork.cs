using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Host migration runtime (everything is a no-op while HostMigrationSettings.Enabled is off).
    ///
    /// During a match the host sends a MatchSnapshotBroadcast every half second
    /// (HostMigrationSnapshotSender). When the connection to the host is lost, the remaining
    /// players do not go back to the hub (HostDisconnectWatchdog asks TryHandleHostLoss first):
    ///  - the successor named in the last snapshot opens a server on the same port, waits for
    ///    the others and reloads the map, where NetworkedLevelControllerManager rebuilds the match
    ///    from the snapshot (HostMigrationResume);
    ///  - everyone else reconnects to the successor's address and is carried into the map by the
    ///    normal global scene load.
    /// The game connection is FishNet over Tugboat to the host's address both in the editor and
    /// in the Steam build (Steam only lists lobbies), so the same path serves both.
    /// </summary>
    public sealed class HostMigrationNetwork : MonoBehaviour
    {
        public static readonly string LocalToken = System.Guid.NewGuid().ToString("N");

        /// <summary>Server: migration token of each connected client.</summary>
        public static readonly Dictionary<int, string> TokensByClientId = new Dictionary<int, string>();

        public static MatchSnapshotBroadcast? LatestSnapshot { get; private set; }
        private static float _latestSnapshotTime;
        private const float MAX_SNAPSHOT_AGE = 5f;

        /// <summary>True while a migration is being carried out on this peer.</summary>
        public static bool Migrating { get; private set; }

        private static HostMigrationNetwork _instance;
        private NetworkManager _hookedManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[HostMigration]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HostMigrationNetwork>();
        }

        private void Update()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || nm == _hookedManager) return;
            _hookedManager = nm;
            nm.ServerManager.OnServerConnectionState += OnServerState;
            nm.ServerManager.OnRemoteConnectionState += OnRemoteState;
            nm.ClientManager.OnClientConnectionState += OnClientState;
            nm.ServerManager.RegisterBroadcast<MigrationTokenBroadcast>(OnTokenReceived, requireAuthentication: false);
            nm.ClientManager.RegisterBroadcast<MatchSnapshotBroadcast>(OnSnapshotReceived);
        }

        // ── Server ─────────────────────────────────────────────────────────────────────
        private static void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped) TokensByClientId.Clear();
        }

        private static void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) TokensByClientId.Remove(conn.ClientId);
        }

        private static void OnTokenReceived(NetworkConnection conn, MigrationTokenBroadcast msg, Channel channel)
        {
            if (conn == null || string.IsNullOrEmpty(msg.Token)) return;
            TokensByClientId[conn.ClientId] = msg.Token;
        }

        public static string TokenOf(NetworkConnection conn) =>
            conn != null && TokensByClientId.TryGetValue(conn.ClientId, out string token) ? token : null;

        // ── Client ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Seconds without hearing from the host before a client gives up on it. Tugboat is set
        /// to 1800 (30 min): a host that quits cleanly is noticed at once, but one that crashes
        /// or loses its connection would have kept everyone waiting half an hour before the
        /// migration could even start.
        /// </summary>
        public const int CLIENT_TIMEOUT_SECONDS = 5;

        private static void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            InstanceFinder.ClientManager.Broadcast(new MigrationTokenBroadcast { Token = LocalToken });
            if (HostMigrationSettings.Enabled && !InstanceFinder.IsServerStarted)
                InstanceFinder.TransportManager.Transport.SetTimeout(CLIENT_TIMEOUT_SECONDS, asServer: false);
        }

        private static void OnSnapshotReceived(MatchSnapshotBroadcast snapshot, Channel channel)
        {
            LatestSnapshot = snapshot;
            _latestSnapshotTime = Time.unscaledTime;
        }

        public static void ClearSnapshot() => LatestSnapshot = null;

        // ── Migration ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// Called by HostDisconnectWatchdog when the connection to the host dropped unexpectedly
        /// during a match. Returns true when this peer takes the migration path instead of the
        /// usual return to the hub.
        /// </summary>
        public static bool TryHandleHostLoss()
        {
            if (!HostMigrationSettings.Enabled || Migrating || _instance == null) return false;
            if (!LatestSnapshot.HasValue || !LatestSnapshot.Value.MigrationEnabled) return false;
            if (Time.unscaledTime - _latestSnapshotTime > MAX_SNAPSHOT_AGE) return false;
            // A client that runs the server itself is the host that left; nothing to migrate to.
            if (InstanceFinder.IsServerStarted) return false;

            var snapshot = LatestSnapshot.Value;
            bool successor = HostMigrationRoster.IsSuccessor(snapshot, LocalToken);
            if (!successor && (snapshot.SuccessorAddresses == null || snapshot.SuccessorAddresses.Length == 0)) return false;

            Migrating = true;
            _instance.StartCoroutine(successor ? _instance.BecomeHost(snapshot) : _instance.RejoinNewHost(snapshot));
            return true;
        }

        private const string TRANSIT_SCENE = "HostMigrationTransit";

        /// <summary>
        /// Freeze the screen on the last frame and drop the old map behind it, into an empty
        /// scene created on the spot. Going through the main menu (loading it, running its
        /// intro) was slow and showed the menu for a moment, which is not how a host migration
        /// is expected to look.
        /// </summary>
        private IEnumerator LeaveCurrentMatchScene(bool becomingHost)
        {
            HostMigrationOverlay.Show(becomingHost
                ? "The host left the match\n<size=70%>You are the new host - continuing...</size>"
                : "The host left the match\n<size=70%>Reconnecting to the new host...</size>");
            MpCanary.Mark("HostMigration: host lost, leaving the map scene");
            yield return new WaitForEndOfFrame();   // let the overlay capture the frame first

            // Same preparation as MultiplayerSceneTeardown: abilities clean up while the map's
            // services still exist, one frame before the map goes.
            NetworkedUniqueAbility.PrepareAllForSceneTeardown();
            yield return null;

            var transit = SceneManager.GetSceneByName(TRANSIT_SCENE);
            if (!transit.IsValid()) transit = SceneManager.CreateScene(TRANSIT_SCENE);
            SceneManager.SetActiveScene(transit);
            var unloads = new List<AsyncOperation>();
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene == transit || !scene.isLoaded) continue;
                var op = SceneManager.UnloadSceneAsync(scene);
                if (op != null) unloads.Add(op);
            }
            foreach (var op in unloads)
                while (!op.isDone) yield return null;
            MpCanary.Mark("HostMigration: map unloaded");
        }

        private IEnumerator BecomeHost(MatchSnapshotBroadcast snapshot)
        {
            yield return LeaveCurrentMatchScene(becomingHost: true);
            MpCanary.Mark($"HostMigration: this peer is the new host (port {snapshot.Port})");

            HostMigrationResume.Pending = snapshot;
            var nm = InstanceFinder.NetworkManager;
            var tugboat = nm != null ? nm.TransportManager.Transport as Tugboat : null;
            if (tugboat != null) tugboat.SetPort(snapshot.Port);
            nm.ServerManager.StartConnection();
            float waited = 0f;
            while (!nm.ServerManager.Started && waited < 5f) { waited += Time.unscaledDeltaTime; yield return null; }
            if (!nm.ServerManager.Started) { Fail("could not open a server"); yield break; }
            if (tugboat != null) tugboat.SetClientAddress("localhost");
            nm.ClientManager.StartConnection();

            // Give the others time to find us.
            var expected = new HashSet<string>();
            for (int i = 1; i < snapshot.SuccessorTokens.Length; i++) expected.Add(snapshot.SuccessorTokens[i]);
            waited = 0f;
            // Reconnecting takes well under a second on a LAN and about one over the internet;
            // anyone later than this joins the reloaded map as a spectator.
            float timeout = Mathf.Min(5f, ConfigReconnectTimeout());
            while (waited < timeout)
            {
                int arrived = 0;
                foreach (var token in TokensByClientId.Values) if (expected.Contains(token)) arrived++;
                if (arrived >= expected.Count && nm.ClientManager.Started) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            MpCanary.Mark($"HostMigration: {TokensByClientId.Count} player(s) connected after {waited:F1}s, reloading '{snapshot.SceneName}'");

            nm.SceneManager.LoadGlobalScenes(new FishNet.Managing.Scened.SceneLoadData(snapshot.SceneName)
            {
                ReplaceScenes = FishNet.Managing.Scened.ReplaceOption.All
            });
            Migrating = false;
        }

        private IEnumerator RejoinNewHost(MatchSnapshotBroadcast snapshot)
        {
            yield return LeaveCurrentMatchScene(becomingHost: false);
            var nm = InstanceFinder.NetworkManager;
            var tugboat = nm != null ? nm.TransportManager.Transport as Tugboat : null;
            string address = snapshot.SuccessorAddresses[0];
            MpCanary.Mark($"HostMigration: reconnecting to the new host at {address}:{snapshot.Port}");

            float deadline = Time.unscaledTime + ConfigReconnectTimeout();
            while (Time.unscaledTime < deadline)
            {
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                if (tugboat != null) { tugboat.SetClientAddress(address); tugboat.SetPort(snapshot.Port); }
                nm.ClientManager.StartConnection();
                float attempt = 0f;
                while (attempt < 3f && !nm.ClientManager.Started) { attempt += Time.unscaledDeltaTime; yield return null; }
                if (nm.ClientManager.Started)
                {
                    MpCanary.Mark("HostMigration: reconnected to the new host");
                    Migrating = false;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Fail("the new host did not answer");
        }

        private static float ConfigReconnectTimeout()
        {
            var config = Resources.Load<HostMigrationConfig>(HostMigrationSettings.CONFIG_RESOURCES_PATH);
            return config != null ? config.ReconnectTimeout : 20f;
        }

        private static void Fail(string reason)
        {
            MpCanary.Mark($"HostMigration FAILED: {reason}");
            Migrating = false;
            HostMigrationResume.Pending = null;
            ConnectionLostNotifier.Show("Could not continue the match - returning to the multiplayer hub.");
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                if (nm.ServerManager.Started) nm.ServerManager.StopConnection(true);
            }
            HostMigrationOverlay.Hide();
            if (_instance != null) _instance.StartCoroutine(ReturnToHub());
        }

        /// <summary>The migration happens in an empty scene: load the menu, then open the hub.</summary>
        private static IEnumerator ReturnToHub()
        {
            if (SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX)
            {
                MultiplayerSceneTeardown.LoadMainMenu();
                float waited = 0f;
                while ((SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX || !MainMenuManager.HasInstance) && waited < 15f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return null;
            }
            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(Enums.ScreensType.MULTIPLAYER_HUB, 0.5f);
        }
    }
}
