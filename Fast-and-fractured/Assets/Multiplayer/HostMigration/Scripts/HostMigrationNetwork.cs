using Assets.SimpleLocalization.Scripts;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastAndFractured.Multiplayer.HostMigration
{
    /// <summary>
    /// Host migration (a no-op unless HostMigrationSettings.Enabled). The host sends snapshots during the match;
    /// when it is lost, the successor named in the last one opens a server and reloads the map from it
    /// (HostMigrationResume) and everyone else reconnects to it.
    /// </summary>
    public sealed class HostMigrationNetwork : MonoBehaviour
    {
        public static readonly string LocalToken = System.Guid.NewGuid().ToString("N");

        /// <summary>Server: migration token of each connected client.</summary>
        public static readonly Dictionary<int, string> TokensByClientId = new Dictionary<int, string>();

        /// <summary>Server: clients that turned off "take over the room" in their settings.</summary>
        public static readonly HashSet<int> UnwillingToHost = new HashSet<int>();

        public static MatchSnapshotBroadcast? LatestSnapshot { get; private set; }
        private static float _latestSnapshotTime;
        private const float MAX_SNAPSHOT_AGE = 5f;
        private const float SERVER_START_TIMEOUT = 5f;
        private const float MAX_RECONNECT_WAIT = 5f;
        private const float CLIENT_START_TIMEOUT = 3f;
        private const float RECONNECT_RETRY_INTERVAL = 0.25f;
        private const float DEFAULT_RECONNECT_TIMEOUT = 20f;
        private const float MENU_LOAD_TIMEOUT = 15f;
        private const float SCREEN_TRANSITION_SECONDS = 0.5f;

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

        private static void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped) { TokensByClientId.Clear(); UnwillingToHost.Clear(); }
        }

        private static void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) { TokensByClientId.Remove(conn.ClientId); UnwillingToHost.Remove(conn.ClientId); }
        }

        private static void OnTokenReceived(NetworkConnection conn, MigrationTokenBroadcast msg, Channel channel)
        {
            if (conn == null || string.IsNullOrEmpty(msg.Token)) return;
            TokensByClientId[conn.ClientId] = msg.Token;
            if (msg.WillingToHost) UnwillingToHost.Remove(conn.ClientId);
            else UnwillingToHost.Add(conn.ClientId);
        }

        public static string TokenOf(NetworkConnection conn) =>
            conn != null && TokensByClientId.TryGetValue(conn.ClientId, out string token) ? token : null;

        /// <summary>
        /// Seconds without hearing from the host before a client gives up. Tugboat's own timeout is 30 min, so a
        /// crashed host would block the migration that long.
        /// </summary>
        public const int CLIENT_TIMEOUT_SECONDS = 5;

        private static void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            InstanceFinder.ClientManager.Broadcast(new MigrationTokenBroadcast { Token = LocalToken, WillingToHost = MultiplayerSettings.WillingToHost });
            if (HostMigrationSettings.Enabled && !InstanceFinder.IsServerStarted)
                MultiplayerTransport.SetTimeout(CLIENT_TIMEOUT_SECONDS, asServer: false);
        }

        private static void OnSnapshotReceived(MatchSnapshotBroadcast snapshot, Channel channel)
        {
            LatestSnapshot = snapshot;
            _latestSnapshotTime = Time.unscaledTime;
        }

        public static void ClearSnapshot() => LatestSnapshot = null;

        /// <summary>HostDisconnectWatchdog, unexpected host loss in a match: true when this peer migrates.</summary>
        public static bool TryHandleHostLoss()
        {
            if (!HostMigrationSettings.Enabled || Migrating || _instance == null) return false;
            if (!LatestSnapshot.HasValue || !LatestSnapshot.Value.MigrationEnabled) return false;
            if (Time.unscaledTime - _latestSnapshotTime > MAX_SNAPSHOT_AGE) return false;
            // A client that runs the server is the host that left.
            if (InstanceFinder.IsServerStarted) return false;

            var snapshot = LatestSnapshot.Value;
            bool successor = HostMigrationRoster.IsSuccessor(snapshot, LocalToken);
            if (!successor && (snapshot.SuccessorAddresses == null || snapshot.SuccessorAddresses.Length == 0)) return false;

            Migrating = true;
            _instance.StartCoroutine(successor ? _instance.BecomeHost(snapshot) : _instance.RejoinNewHost(snapshot));
            return true;
        }

        private const string TRANSIT_SCENE = "HostMigrationTransit";

        /// <summary>Freezes the last frame and drops the old map into an empty scene (faster than via the main menu).</summary>
        private IEnumerator LeaveCurrentMatchScene(bool becomingHost)
        {
            HostMigrationOverlay.Show(becomingHost
                ? LocalizationManager.Localize("MP.Migration.NewHost")
                : LocalizationManager.Localize("MP.Migration.Reconnecting"));
            MpCanary.Mark("HostMigration: host lost, leaving the map scene");
            yield return new WaitForEndOfFrame();   // let the overlay capture the frame first

            // As in MultiplayerSceneTeardown: abilities clean up a frame before the map goes.
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
            MultiplayerTransport.SetPort(snapshot.Port);
            MultiplayerTransport.StartServer(nm);
            float waited = 0f;
            while (!nm.ServerManager.Started && waited < SERVER_START_TIMEOUT) { waited += Time.unscaledDeltaTime; yield return null; }
            if (!nm.ServerManager.Started) { Fail("could not open a server"); yield break; }
            MultiplayerTransport.SetLocalHostTarget();
            nm.ClientManager.StartConnection();

            var expected = new HashSet<string>();
            for (int i = 1; i < snapshot.SuccessorTokens.Length; i++) expected.Add(snapshot.SuccessorTokens[i]);
            waited = 0f;
            // Later arrivals join the reloaded map as spectators.
            float timeout = Mathf.Min(MAX_RECONNECT_WAIT, ConfigReconnectTimeout());
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
            string address = snapshot.SuccessorAddresses[0];
            MpCanary.Mark($"HostMigration: reconnecting to the new host at {address}:{snapshot.Port}");

            float deadline = Time.unscaledTime + ConfigReconnectTimeout();
            while (Time.unscaledTime < deadline)
            {
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                MultiplayerTransport.SetClientTarget(address, snapshot.Port);
                nm.ClientManager.StartConnection();
                float attempt = 0f;
                while (attempt < CLIENT_START_TIMEOUT && !nm.ClientManager.Started) { attempt += Time.unscaledDeltaTime; yield return null; }
                if (nm.ClientManager.Started)
                {
                    MpCanary.Mark("HostMigration: reconnected to the new host");
                    Migrating = false;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(RECONNECT_RETRY_INTERVAL);
            }
            Fail("the new host did not answer");
        }

        private static float ConfigReconnectTimeout()
        {
            var config = Resources.Load<HostMigrationConfig>(HostMigrationSettings.CONFIG_RESOURCES_PATH);
            return config != null ? config.ReconnectTimeout : DEFAULT_RECONNECT_TIMEOUT;
        }

        private static void Fail(string reason)
        {
            MpCanary.Mark($"HostMigration FAILED: {reason}");
            Migrating = false;
            HostMigrationResume.Pending = null;
            ConnectionLostNotifier.Show(LocalizationManager.Localize("MP.Migration.Failed"));
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
                if (nm.ServerManager.Started) nm.ServerManager.StopConnection(true);
            }
            HostMigrationOverlay.Hide();
            if (_instance != null) _instance.StartCoroutine(ReturnToHub());
        }

        /// <summary>Out of a match after a failed migration: load the menu, or back to the hub if already there.</summary>
        private static IEnumerator ReturnToHub()
        {
            if (SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX)
            {
                MultiplayerSceneTeardown.LoadMainMenu();
                float waited = 0f;
                while ((SceneManager.GetActiveScene().buildIndex != LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX || !MainMenuManager.HasInstance) && waited < MENU_LOAD_TIMEOUT)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return null;
                yield break;
            }
            if (MainMenuManager.HasInstance)
                MainMenuManager.Instance.TransitionBetweenScreens(Enums.ScreensType.MULTIPLAYER_HUB, SCREEN_TRANSITION_SECONDS);
        }
    }
}
