// Ensamblado: Game.GameplayOrUI (Depende de Game.Network.Abstractions)
using UnityEngine;
using UnityEngine.UI; // Necesario para Button
using TMPro;        // Para TextMeshPro

public class LobbyControllerAbstracted : MonoBehaviour
{
    [Header("UI Elements - TextMeshPro")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button serverOnlyButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_Text statusText;

    private IMyNetworkManager _myNetworkManager;

    void Start()
    {
        _myNetworkManager = FishNetNetworkManagerWrapper.Instance; // O la forma que uses para obtener tu wrapper

        if (_myNetworkManager == null)
        {
            UpdateStatus("IMyNetworkManager no asignado o no encontrado.");
            // Asegurarnos de que los botones están desactivados si no hay network manager
            if (hostButton) hostButton.interactable = false;
            if (serverOnlyButton) serverOnlyButton.interactable = false;
            if (clientButton) clientButton.interactable = false;
            if (stopButton) stopButton.interactable = false;
            if (addressInput) addressInput.interactable = false;
            return;
        }

        // Asignar listeners a los botones
        hostButton.onClick.AddListener(OnClick_Host);
        serverOnlyButton.onClick.AddListener(OnClick_ServerOnly);
        clientButton.onClick.AddListener(OnClick_Client);
        stopButton.onClick.AddListener(OnClick_StopConnection);

        // Suscribirse a eventos de la capa de abstracción
        _myNetworkManager.OnServerStateChanged += HandleServerStateChanged;
        _myNetworkManager.OnClientLocalStateChanged += HandleClientLocalStateChanged;
        _myNetworkManager.OnClientRemoteStateChanged += HandleClientRemoteStateChanged;

        // Estado inicial de la UI basado en el estado de red actual
        UpdateGUIBasedOnNetworkState();
        if (addressInput != null) addressInput.text = "localhost"; // Valor por defecto
    }

    void OnDestroy()
    {
        if (_myNetworkManager != null)
        {
            _myNetworkManager.OnServerStateChanged -= HandleServerStateChanged;
            _myNetworkManager.OnClientLocalStateChanged -= HandleClientLocalStateChanged;
            _myNetworkManager.OnClientRemoteStateChanged -= HandleClientRemoteStateChanged;
        }
    }

    /// <summary>
    /// Actualiza el estado de interactividad de los botones de la UI
    /// basado en el estado actual del IMyNetworkManager.
    /// </summary>
    private void UpdateGUIBasedOnNetworkState()
    {
        if (_myNetworkManager == null) return;

        bool serverRunning = _myNetworkManager.IsServerRunning;
        bool clientStarted = _myNetworkManager.IsClientLocallyStarted;
        // bool clientConnected = _myNetworkManager.IsClientConnectedToServer; // Podría usarse para lógica más fina

        // Lógica para habilitar/deshabilitar botones
        bool canStartNewConnection = !serverRunning && !clientStarted;

        if (hostButton != null) hostButton.interactable = canStartNewConnection;
        if (serverOnlyButton != null) serverOnlyButton.interactable = canStartNewConnection;
        if (clientButton != null) clientButton.interactable = canStartNewConnection;
        if (addressInput != null) addressInput.interactable = canStartNewConnection;

        if (stopButton != null) stopButton.interactable = serverRunning || clientStarted;
    }

    #region MyNetworkManager Event Handlers
    private void HandleServerStateChanged(MyNetworkConnectionState newState)
    {
        string message = "Estado del Servidor (Abstracto): " + newState;
        if (newState == MyNetworkConnectionState.Started) message = "Servidor (abstracto) INICIADO.";
        if (newState == MyNetworkConnectionState.Stopped) message = "Servidor (abstracto) DETENIDO.";
        UpdateStatus(message);
        UpdateGUIBasedOnNetworkState(); // Refleja el nuevo estado en la UI
    }

    private void HandleClientLocalStateChanged(MyNetworkConnectionState newState)
    {
        string message = "Estado Local del Cliente (Abstracto): " + newState;
        if (newState == MyNetworkConnectionState.Starting) message = "Cliente (abstracto) intentando conectar...";
        // Mostrar "detenido" solo si realmente no estamos conectados remotamente y no es porque nos hayamos desconectado de un servidor (ese mensaje vendrá de RemoteStateChanged)
        else if (newState == MyNetworkConnectionState.Stopped && !_myNetworkManager.IsClientConnectedToServer)
        {
            message = "Cliente (abstracto) detenido localmente.";
        }


        // Solo actualiza el status si el mensaje es relevante y no es redundante con OnClientRemoteStateChanged
        bool shouldUpdateStatus = true;
        if (newState == MyNetworkConnectionState.Started && _myNetworkManager.IsClientConnectedToServer)
            shouldUpdateStatus = false; // Ya se mostrará "Conectado a X"
        if (newState == MyNetworkConnectionState.Stopped && statusText != null && statusText.text.Contains("Desconectado de"))
            shouldUpdateStatus = false; // Ya se mostró "Desconectado de X"

        if (shouldUpdateStatus)
        {
            UpdateStatus(message);
        }
        UpdateGUIBasedOnNetworkState(); // Refleja el nuevo estado en la UI
    }

    private void HandleClientRemoteStateChanged(MyNetworkConnectionState newState, int clientId, string address)
    {
        string message;

        bool amIServer = _myNetworkManager.IsServerRunning;

        if (amIServer) // El servidor está recibiendo noticias sobre un cliente remoto
        {
            if (newState == MyNetworkConnectionState.Started)
            {
                message = $"Servidor: Cliente {clientId} conectado desde {address}.";
            }
            else // Stopped
            {
                message = $"Servidor: Cliente {clientId} desconectado.";
            }
        }
        else // Soy un cliente y este evento es sobre mi conexión con el servidor
        {
            // Verificamos si este evento es realmente sobre MI conexión al servidor.
            // El `clientId` aquí será el ID del cliente local asignado por el servidor.
            // `address` es la dirección del servidor.
            if (newState == MyNetworkConnectionState.Started)
            {
                message = $"Cliente: ¡Conectado a {address}! (Mi ClientId: {clientId})";
            }
            else // Stopped
            {
                message = $"Cliente: Desconectado del servidor {address}.";
            }
        }

        UpdateStatus(message);
        UpdateGUIBasedOnNetworkState(); // Refleja el nuevo estado en la UI
    }
    #endregion

    #region Button Clicks
    private void OnClick_Host()
    {
        UpdateStatus("Iniciando Host (abstracto)...");
        _myNetworkManager.StartHost();
        UpdateGUIBasedOnNetworkState(); // Actualización inmediata de la UI
    }

    private void OnClick_ServerOnly()
    {
        UpdateStatus("Iniciando Servidor Dedicado (abstracto)...");
        _myNetworkManager.StartServer();
        UpdateGUIBasedOnNetworkState(); // Actualización inmediata de la UI
    }

    private void OnClick_Client()
    {
        string address = (addressInput != null && !string.IsNullOrWhiteSpace(addressInput.text)) ? addressInput.text : "localhost";
        UpdateStatus($"Conectando a {address} (abstracto)...");
        _myNetworkManager.StartClient(address);
        UpdateGUIBasedOnNetworkState(); // Actualización inmediata de la UI
    }

    private void OnClick_StopConnection()
    {
        UpdateStatus("Deteniendo conexión (abstracto)...");
        _myNetworkManager.StopConnection();
        UpdateGUIBasedOnNetworkState(); // Actualización inmediata de la UI
    }
    #endregion

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log(message);
    }
}