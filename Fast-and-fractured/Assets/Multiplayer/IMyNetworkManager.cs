using System;

public enum MyNetworkConnectionState
{
    Stopped,
    Starting,
    Started,
    Stopping
}

public interface IMyNetworkManager
{
    event Action<MyNetworkConnectionState> OnServerStateChanged;
    event Action<MyNetworkConnectionState> OnClientLocalStateChanged;
    event Action<MyNetworkConnectionState, int /*clientId*/, string /*address*/> OnClientRemoteStateChanged;

    void StartHost();
    void StartServer();
    void StartClient(string address);
    void StopConnection();

    bool IsServerRunning { get; }
    bool IsClientLocallyStarted { get; }  
    bool IsClientConnectedToServer { get; }  

    int PlayerCount { get; }
}