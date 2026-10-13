// SCENE SETUP:
//   - Place on the MULTIPLAYER_BROWSER MenuScreen GameObject.
//   - Wire _entryContainer  (Transform, parent for entries)
//   - Wire _entryPrefab     (LobbyBrowserEntry prefab)
//   - Wire _refreshButton   (Button)
//   - Wire _backButton      (Button)
//   - Wire _statusLabel     (TMP_Text, "Searching..." / "No rooms found")
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
            SetStatus("Searching...");
            SetRefreshInteractable(false);

            try
            {
                var listings = await LobbyServiceLocator.Transport.ListLobbiesAsync(
                    LobbyFilter.Default, _cts.Token);

                ClearEntries();

                // If we're currently hosting, inject a synthetic entry for our own room. UDP
                // broadcast loopback is unreliable on Windows when running multiple instances on
                // the same machine, so this guarantees the host always sees their own lobby.
                var ownListing = TryBuildOwnHostListing();
                bool haveOwn   = ownListing != null;

                if (listings.Count == 0 && !haveOwn)
                {
                    SetStatus("No rooms found.");
                }
                else
                {
                    SetStatus(haveOwn ? "You are hosting · others see this room over LAN." : string.Empty);

                    if (haveOwn) AddEntry(ownListing, own: true);

                    foreach (var listing in listings)
                    {
                        // Skip if a remote listing exactly matches our own host (de-dupe loopback).
                        if (haveOwn && listing.Handle.Port == ownListing.Handle.Port &&
                            (listing.Handle.Address == ownListing.Handle.Address ||
                             listing.Handle.Address == "127.0.0.1"))
                            continue;
                        AddEntry(listing);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { SetStatus("Search failed."); }
            finally { SetRefreshInteractable(true); }
        }

        /// <summary>
        /// Build a LobbyListing pointing to the local server if we are currently hosting.
        /// Returns null when we're not the host.
        /// </summary>
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
                RoomName       = "★ Your Room (hosting)",
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

        /// <summary>
        /// ICMP echo to the room's address. LAN hosts usually answer; a firewall that drops echo
        /// requests leaves "-- ms" (the room stays joinable, only the number is missing).
        /// </summary>
        private static async void MeasurePing(LobbyBrowserEntry entry, string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            int best = -1;
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                for (int i = 0; i < 3; i++)
                {
                    var reply = await ping.SendPingAsync(address, 1000);
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
            SetStatus("Connecting...");

            _cts?.Cancel();
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                bool ok = await LobbyServiceLocator.Transport.JoinLobbyAsync(listing.Handle, _cts.Token);
                if (ok)
                    MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.LOBBY_ROOM, _fadeDuration);
                else
                    SetStatus("Connection refused.");
            }
            catch (OperationCanceledException) { SetStatus("Timed out."); }
            catch (Exception ex)               { SetStatus($"Error: {ex.Message}"); }
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
