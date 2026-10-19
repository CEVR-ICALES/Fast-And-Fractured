using Assets.SimpleLocalization.Scripts;
using System;
using System.Collections.Generic;
using System.Threading;
using Enums;
using FastAndFractured;
using FastAndFractured.Multiplayer.Abstractions;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class LobbyBrowserController : MonoBehaviour
    {
        private const int PING_TIMEOUT_MS = 1000;
        private const int SEARCH_TIMEOUT_SECONDS = 10;

        [SerializeField] private Transform          _entryContainer;
        [SerializeField] private LobbyBrowserEntry  _entryPrefab;
        [SerializeField] private Button             _refreshButton;
        [SerializeField] private Button             _backButton;
        [SerializeField] private TMP_Text           _statusLabel;
        [SerializeField] private float              _fadeDuration = 0.5f;

        private readonly List<LobbyBrowserEntry> _entries = new List<LobbyBrowserEntry>();
        private CancellationTokenSource _cts;
        private bool _joining;

        private void Awake()
        {
            _refreshButton?.onClick.AddListener(StartRefresh);
            _backButton?.onClick.AddListener(() => MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MULTIPLAYER_HUB, _fadeDuration));
        }

        private void OnEnable()  => StartRefresh();
        private void OnDisable() => _cts?.Cancel();
        private void OnDestroy() => _cts?.Cancel();

        private async void StartRefresh()
        {
            if (_joining) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            ClearEntries();
            SetStatus(LocalizationManager.Localize("MP.Browser.Searching"));
            SetRefreshInteractable(false);

            try
            {
                var listings = await LobbyServiceLocator.Transport.ListLobbiesAsync(
                    LobbyFilter.Default, _cts.Token);

                ClearEntries();

                // The host's own room: UDP broadcast loopback is unreliable between instances on one Windows machine.
                var ownListing = TryBuildOwnHostListing();
                bool haveOwn   = ownListing != null;

                if (listings.Count == 0 && !haveOwn)
                {
                    SetStatus(LocalizationManager.Localize("MP.Browser.NoRooms"));
                }
                else
                {
                    SetStatus(haveOwn ? LocalizationManager.Localize("MP.Browser.Hosting") : string.Empty);

                    if (haveOwn) AddEntry(ownListing, own: true);

                    foreach (var listing in listings)
                    {
                        if (haveOwn && listing.Handle.Port == ownListing.Handle.Port &&
                            (listing.Handle.Address == ownListing.Handle.Address ||
                             listing.Handle.Address == "127.0.0.1"))
                            continue;
                        AddEntry(listing);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { SetStatus(LocalizationManager.Localize("MP.Browser.Failed")); }
            finally { SetRefreshInteractable(true); }
        }

        private static LobbyListing TryBuildOwnHostListing()
        {
            if (!InstanceFinder.IsServerStarted) return null;

            ushort port = 0;
            try { port = InstanceFinder.NetworkManager?.TransportManager?.Transport?.GetPort() ?? (ushort)0; }
            catch { /* transport may not expose port */ }

            int currentPlayers = InstanceFinder.ServerManager?.Clients?.Count ?? 0;

            return new LobbyListing
            {
                Handle         = LobbyHandle.FromAddress("127.0.0.1", port),
                RoomName       = LocalizationManager.Localize("MP.Browser.YourRoom"),
                CurrentPlayers = currentPlayers,
                MaxPlayers     = 8,
                PingMs         = 0,
                HostLink       = FastAndFractured.Multiplayer.NetworkLink.Local,
                InMatch        = LobbyLocator.GetLobbyManager()?.GameStarted.Value ?? false,
            };
        }

        private void AddEntry(LobbyListing listing, bool own = false)
        {
            if (_entryPrefab == null || _entryContainer == null) return;
            var entry = Instantiate(_entryPrefab, _entryContainer);
            bool full = listing.CurrentPlayers >= listing.MaxPlayers;
            entry.Populate(listing, OnJoinSelected);
            // A room in a match stays joinable: the newcomer waits in it for the next match.
            entry.SetJoinable(!full);
            entry.SetInMatch(listing.InMatch);
            _entries.Add(entry);
            if (own) entry.SetPing(0);
            else MeasurePing(entry, listing.Handle.Address);
        }

        /// <summary>ICMP echo to the room's address; a firewall dropping it leaves "-- ms".</summary>
        private static async void MeasurePing(LobbyBrowserEntry entry, string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            int best = -1;
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                for (int i = 0; i < 3; i++)
                {
                    var reply = await ping.SendPingAsync(address, PING_TIMEOUT_MS);
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success &&
                        (best < 0 || reply.RoundtripTime < best))
                        best = (int)reply.RoundtripTime;
                }
            }
            catch { /* unreachable or not allowed */ }
            if (entry != null) entry.SetPing(best);
        }

        private void ClearEntries()
        {
            foreach (var e in _entries)
                if (e != null) Destroy(e.gameObject);
            _entries.Clear();
        }

        private async void OnJoinSelected(LobbyListing listing)
        {
            if (_joining) return;
            _joining = true;
            SetRefreshInteractable(false);
            SetStatus(LocalizationManager.Localize("MP.Connecting"));

            _cts?.Cancel();
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(SEARCH_TIMEOUT_SECONDS));
            try
            {
                bool ok = await LobbyServiceLocator.Transport.JoinLobbyAsync(listing.Handle, _cts.Token);
                if (ok)
                    MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, _fadeDuration);
                else
                    SetStatus(LocalizationManager.Localize("MP.ConnectionRefused"));
            }
            catch (OperationCanceledException) { SetStatus(LocalizationManager.Localize("MP.TimedOut")); }
            catch (Exception ex)               { SetStatus(LocalizationManager.Localize("MP.Error", ex.Message)); }
            finally
            {
                _joining = false;
                SetRefreshInteractable(true);
            }
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
        }

        private void SetRefreshInteractable(bool value)
        {
            if (_refreshButton != null) _refreshButton.interactable = value;
        }
    }
}
