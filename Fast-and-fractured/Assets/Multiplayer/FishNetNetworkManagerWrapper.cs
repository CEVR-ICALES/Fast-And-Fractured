using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
public class FishNetNetworkManagerWrapper : AbstractSingleton<FishNetNetworkManagerWrapper>, IMyNetworkManager
{
    public event Action<MyNetworkConnectionState> OnServerStateChanged;
    public event Action<MyNetworkConnectionState> OnClientLocalStateChanged;
    public event Action<MyNetworkConnectionState, int, string> OnClientRemoteStateChanged;

    private NetworkManager _fnNetworkManager;
    private string _currentClientTargetAddress;

    protected override void Construct()
    {
        base.Construct();
        _fnNetworkManager = InstanceFinder.NetworkManager;
        if (_fnNetworkManager == null)
        {
            Debug.LogError("FishNet NetworkManager not found Multiplayer will not work.");
            return;
        }

        _fnNetworkManager.ServerManager.OnServerConnectionState += HandleFishNetServerConnectionState;
        _fnNetworkManager.ServerManager.OnRemoteConnectionState += HandleFishNetRemoteClientConnectionStateOnServer;
        _fnNetworkManager.ClientManager.OnClientConnectionState += HandleFishNetClientLocalConnectionState;
        _fnNetworkManager.ClientManager.OnRemoteConnectionState += HandleFishNetClientRemoteConnectionStateWithServer;
    }
    protected override void Initialize()
    {
         
    }

    private void OnDestroy()
    {
        if (_fnNetworkManager != null)
        {
            _fnNetworkManager.ServerManager.OnServerConnectionState -= HandleFishNetServerConnectionState;
            _fnNetworkManager.ServerManager.OnRemoteConnectionState -= HandleFishNetRemoteClientConnectionStateOnServer;
            _fnNetworkManager.ClientManager.OnClientConnectionState -= HandleFishNetClientLocalConnectionState;
            _fnNetworkManager.ClientManager.OnRemoteConnectionState -= HandleFishNetClientRemoteConnectionStateWithServer;
        }
    }

    private void HandleFishNetServerConnectionState(ServerConnectionStateArgs args)
    {
        OnServerStateChanged?.Invoke(ConvertFishNetLocalState(args.ConnectionState));
    }

    private void HandleFishNetRemoteClientConnectionStateOnServer(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        OnClientRemoteStateChanged?.Invoke(ConvertFishNetRemoteState(args.ConnectionState), conn.ClientId, conn.GetAddress());
    }

    private void HandleFishNetClientLocalConnectionState(ClientConnectionStateArgs args)
    {
        OnClientLocalStateChanged?.Invoke(ConvertFishNetLocalState(args.ConnectionState));
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _currentClientTargetAddress = string.Empty;  
        }
    }

     private void HandleFishNetClientRemoteConnectionStateWithServer(RemoteConnectionStateArgs args)
    {
        int localClientId = _fnNetworkManager.ClientManager.Connection.ClientId;
        string serverAddressToReport = _currentClientTargetAddress;  
        if (args.ConnectionState == RemoteConnectionState.Started && _fnNetworkManager.ClientManager.Connection.IsActive)
        {
            serverAddressToReport = _fnNetworkManager.ClientManager.Connection.GetAddress();
            _currentClientTargetAddress = serverAddressToReport; 
        }

        OnClientRemoteStateChanged?.Invoke(ConvertFishNetRemoteState(args.ConnectionState), localClientId, serverAddressToReport);

        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _currentClientTargetAddress = string.Empty; 
        }
    }

    private MyNetworkConnectionState ConvertFishNetLocalState(LocalConnectionState fishNetState)
    {
        switch (fishNetState)
        {
            case LocalConnectionState.Stopped: return MyNetworkConnectionState.Stopped;
            case LocalConnectionState.Starting: return MyNetworkConnectionState.Starting;
            case LocalConnectionState.Started: return MyNetworkConnectionState.Started;
            case LocalConnectionState.Stopping: return MyNetworkConnectionState.Stopping;
            default: return MyNetworkConnectionState.Stopped;
        }
    }

    private MyNetworkConnectionState ConvertFishNetRemoteState(RemoteConnectionState fishNetState)
    {
        switch (fishNetState)
        {
            case RemoteConnectionState.Stopped: return MyNetworkConnectionState.Stopped;
            case RemoteConnectionState.Started: return MyNetworkConnectionState.Started;
            default: return MyNetworkConnectionState.Stopped;
        }
    }

    public void StartHost()
    {
        if (IsServerRunning || IsClientLocallyStarted) return;
        _currentClientTargetAddress = "localhost";  
        _fnNetworkManager.ServerManager.StartConnection();
        _fnNetworkManager.ClientManager.StartConnection();
    }

    void StartClient()
    {

    }
    public void StartServer()
    {
        if (IsServerRunning) return;
        _fnNetworkManager.ServerManager.StartConnection();
    }

    public void StartClient(string address)
    {
        if (IsClientLocallyStarted) return;
        _currentClientTargetAddress = address;  
        _fnNetworkManager.ClientManager.StartConnection(address);
    }

    public void StopConnection()
    {
        bool serverWasRunning = IsServerRunning;
        bool clientWasStarted = IsClientLocallyStarted;

        if (serverWasRunning)
            _fnNetworkManager.ServerManager.StopConnection(true);
        if (clientWasStarted)
            _fnNetworkManager.ClientManager.StopConnection();

 
    }
    public int PlayerCount
    {
        get
        {
            if (_fnNetworkManager != null && _fnNetworkManager.ServerManager.Started)
            {
                return _fnNetworkManager.ServerManager.Clients.Count;
            }
            return 0;
        }
    }
    public bool IsServerRunning => _fnNetworkManager != null && _fnNetworkManager.ServerManager.Started;
    public bool IsClientLocallyStarted => _fnNetworkManager != null && _fnNetworkManager.ClientManager.Started;
    public bool IsClientConnectedToServer => _fnNetworkManager != null && _fnNetworkManager.ClientManager.Started && _fnNetworkManager.ClientManager.Connection.IsActive;
}