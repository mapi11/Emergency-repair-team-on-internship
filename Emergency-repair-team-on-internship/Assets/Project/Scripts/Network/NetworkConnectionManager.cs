using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkConnectionManager : MonoBehaviour
{
    public static NetworkConnectionManager Instance { get; private set; }

    private static readonly Dictionary<ulong, string> clientProfiles = new Dictionary<ulong, string>();
    public static readonly HashSet<string> PreMissionProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly string localSessionId = Guid.NewGuid().ToString("N");

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("Local Connection")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    [Header("Relay")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string relayConnectionType = "dtls";

    [Header("Connection Screen")]
    [SerializeField] private ConnectionScreenManager connectionScreenPrefab;

    [Header("Timeout")]
    [SerializeField] private float clientConnectTimeout = 45f;

    [Header("Debug")]
    [SerializeField] private bool isBusy;
    [SerializeField] private string status = "Offline";
    [SerializeField] private string currentJoinCode = "";

    private UnityTransport transport;
    private bool waitingForClientConnection;
    private float clientConnectionTimer;

    public event Action<string> StatusChanged;

    public bool IsBusy => isBusy;
    public string Status => status;
    public string CurrentJoinCode => currentJoinCode;
    public string RelayConnectionType => relayConnectionType;

    public int MaxPlayers { get; set; }
    public static bool IsLobbyLocked { get; set; }
    public static int FixedPlayerCount { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        MaxPlayers = maxPlayers;

        FindTransportIfNeeded();
    }

    private void OnEnable()
    {
        RegisterNetworkCallbacks();
    }

    private void OnDisable()
    {
        UnregisterNetworkCallbacks();
    }

    private void Update()
    {
        UpdateClientConnectionTimeout();
    }

    public void SetRelayConnectionType(string value)
    {
        relayConnectionType = NormalizeRelayConnectionType(value);

        GameSessionData.ConnectionType = relayConnectionType;

        SetStatus($"Connection type: {relayConnectionType}");
    }

    public void SetLocalConnectionData(string newAddress, ushort newPort)
    {
        address = newAddress;
        port = newPort;
    }

    public void ApplyPlayerSettings(string profileId, string playerName, Color32 playerColor)
    {
        LocalPlayerSettings.SetProfileId(profileId);
        LocalPlayerSettings.SetPlayerName(playerName);
        LocalPlayerSettings.SetPlayerColor(playerColor);
    }

    public void RandomizePlayerColor(string profileId)
    {
        LocalPlayerSettings.Load(profileId);
        LocalPlayerSettings.GenerateAndSaveRandomColor();

        SetStatus("Random color selected");
    }

    public void StartLocalHost(string profileId, string playerName, Color32 playerColor)
    {
        if (isBusy)
            return;

        ShowConnectionScreen(playerColor);

        ApplyPlayerSettings(profileId, playerName, playerColor);
        ApplyLocalConnectionData();

        RegisterNetworkCallbacks();
        SetConnectionPayload();

        bool started = NetworkManager.Singleton.StartHost();

        if (!started)
        {
            SetStatus("Local Host failed");
            DismissConnectionScreen();
            return;
        }

        SetStatus("Local Host started");

        GameSessionData.JoinCode = $"LOCAL {address}:{port}";
        GameSessionData.ConnectionType = "local";

        LoadLobbySceneAsServer();
    }

    public void StartLocalClient(string profileId, string playerName, Color32 playerColor)
    {
        if (isBusy)
            return;

        ShowConnectionScreen(playerColor);

        ApplyPlayerSettings(profileId, playerName, playerColor);
        ApplyLocalConnectionData();

        RegisterNetworkCallbacks();
        SetConnectionPayload();

        bool started = NetworkManager.Singleton.StartClient();

        if (started)
        {
            waitingForClientConnection = true;
            clientConnectionTimer = 0f;

            GameSessionData.JoinCode = $"LOCAL {address}:{port}";
            GameSessionData.ConnectionType = "local";

            SetStatus("Local Client starting...");
        }
        else
        {
            SetStatus("Local Client failed");
            DismissConnectionScreen();
        }
    }

    public async Task StartOnlineHostAsync(string profileId, string playerName, Color32 playerColor)
    {
        if (isBusy)
            return;

        isBusy = true;
        SetStatus("Starting online host...");

        try
        {
            ApplyPlayerSettings(profileId, playerName, playerColor);
            FindTransportIfNeeded();

            if (transport == null)
            {
                SetStatus("UnityTransport not found");
                return;
            }

            relayConnectionType = NormalizeRelayConnectionType(relayConnectionType);

            ShowConnectionScreen(playerColor);

            await EnsureUnityServicesAsync();

            int maxConnections = Mathf.Max(1, MaxPlayers - 1);

            Debug.Log($"🌐 Creating Relay allocation. MaxConnections={maxConnections}, Type={relayConnectionType}");

            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(
                allocation,
                relayConnectionType
            );

            transport.SetRelayServerData(relayServerData);

            // Для wss обязательно true. Для udp/dtls false.
            transport.UseWebSockets = relayConnectionType == "wss";

            currentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            currentJoinCode = SanitizeRelayJoinCode(currentJoinCode);

            GUIUtility.systemCopyBuffer = currentJoinCode;

            GameSessionData.JoinCode = currentJoinCode;
            GameSessionData.ConnectionType = relayConnectionType;

            RegisterNetworkCallbacks();
            SetConnectionPayload();

            bool started = NetworkManager.Singleton.StartHost();

            if (!started)
            {
                SetStatus("Online Host failed");
                DismissConnectionScreen();
                return;
            }

            SetStatus($"Online Host started. Code: {currentJoinCode}");

            Debug.Log($"✅ Online Host started. Join Code: {currentJoinCode}");
            Debug.Log($"📋 Join Code copied to clipboard: {currentJoinCode}");

            LoadLobbySceneAsServer();
        }
        catch (Exception exception)
        {
            string errorMessage = GetShortExceptionMessage(exception);

            SetStatus($"Host error: {errorMessage}");
            DismissConnectionScreen();

            Debug.LogError($"❌ Online Host error: {exception}");
        }
        finally
        {
            isBusy = false;
        }
    }

    public async Task JoinOnlineAsync(string profileId, string playerName, string joinCode, Color32 playerColor)
    {
        if (isBusy)
            return;

        isBusy = true;
        SetStatus("Joining online...");

        try
        {
            ApplyPlayerSettings(profileId, playerName, playerColor);
            FindTransportIfNeeded();

            if (transport == null)
            {
                SetStatus("UnityTransport not found");
                return;
            }

            relayConnectionType = NormalizeRelayConnectionType(relayConnectionType);

            string code = SanitizeRelayJoinCode(joinCode);

            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Join Code is empty");
                return;
            }

            ShowConnectionScreen(playerColor);

            await EnsureUnityServicesAsync();

            Debug.Log($"🌐 Joining Relay allocation. Code={code}, Type={relayConnectionType}");

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(
                joinAllocation,
                relayConnectionType
            );

            transport.SetRelayServerData(relayServerData);

            // Для wss обязательно true. Для udp/dtls false.
            transport.UseWebSockets = relayConnectionType == "wss";

            currentJoinCode = code;

            GameSessionData.JoinCode = code;
            GameSessionData.ConnectionType = relayConnectionType;

            RegisterNetworkCallbacks();
            SetConnectionPayload();

            bool started = NetworkManager.Singleton.StartClient();

            if (started)
            {
                waitingForClientConnection = true;
                clientConnectionTimer = 0f;

                SetStatus($"Online Client starting... Code: {code}");
            }
            else
            {
                SetStatus("Online Client failed");
                DismissConnectionScreen();
            }

            Debug.Log(started
                ? $"✅ Online Client starting. Join Code: {code}"
                : "❌ Online Client failed");
        }
        catch (Exception exception)
        {
            string errorMessage = GetShortExceptionMessage(exception);

            SetStatus($"Join error: {errorMessage}");
            DismissConnectionScreen();

            Debug.LogError($"❌ Join Online error: {exception}");
        }
        finally
        {
            isBusy = false;
        }
    }

    public void ShutdownNetwork()
    {
        waitingForClientConnection = false;
        clientConnectionTimer = 0f;
        isBusy = false;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SetStatus("Shutdown");
    }

    public static void SnapshotPreMissionProfiles()
    {
        PreMissionProfiles.Clear();
        foreach (var profileId in clientProfiles.Values)
            PreMissionProfiles.Add(profileId);
    }

    public static string GetConnectionPayloadId()
    {
        return localSessionId;
    }

    private static void SetConnectionPayload()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(GetConnectionPayloadId());
    }

    private void ShowConnectionScreen(Color32 playerColor)
    {
        if (connectionScreenPrefab == null)
            return;

        var screen = Instantiate(connectionScreenPrefab);
        DontDestroyOnLoad(screen.gameObject);
        screen.Show(playerColor);
    }

    private static void DismissConnectionScreen()
    {
        var screen = FindObjectOfType<ConnectionScreenManager>();
        if (screen != null)
            screen.Dismiss();
    }

    private void LoadLobbySceneAsServer()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogError("Enable Scene Management is disabled in NetworkManager.");
            SetStatus("Enable Scene Management is disabled");
            return;
        }

        Debug.Log($"🌍 Loading network scene: {lobbySceneName}");

        NetworkManager.Singleton.SceneManager.LoadScene(
            lobbySceneName,
            LoadSceneMode.Single
        );
    }

    private void FindTransportIfNeeded()
    {
        if (transport != null)
            return;

        transport = GetComponent<UnityTransport>();

        if (transport == null && NetworkManager.Singleton != null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }
    }

    private void ApplyLocalConnectionData()
    {
        FindTransportIfNeeded();

        if (transport == null)
        {
            SetStatus("UnityTransport not found");
            return;
        }

        transport.SetConnectionData(address, port);

        Debug.Log($"🌐 Local connection data: {address}:{port}");
    }

    private async Task EnsureUnityServicesAsync()
    {
        string authProfile = SanitizeAuthenticationProfile(LocalPlayerSettings.ProfileId);

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitializationOptions options = new InitializationOptions();
            options.SetProfile(authProfile);

            await UnityServices.InitializeAsync(options);

            Debug.Log($"🔧 Unity Services initialized. Auth Profile: {authProfile}");
        }
        else
        {
            Debug.Log($"🔧 Unity Services already initialized. Current Auth Profile: {AuthenticationService.Instance.Profile}");
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"🔐 Signed in anonymously. PlayerId: {AuthenticationService.Instance.PlayerId}, Profile: {AuthenticationService.Instance.Profile}");
        }
    }

    private void RegisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApproval;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;

        NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApproval;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApproval;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
    }

    private void OnConnectionApproval(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        string profileId = System.Text.Encoding.UTF8.GetString(request.Payload);
        if (string.IsNullOrEmpty(profileId))
            profileId = "Unknown";

        clientProfiles[request.ClientNetworkId] = profileId;

        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "Lobby is full.";
            response.CreatePlayerObject = false;
            return;
        }

        if (IsLobbyLocked || (MissionManager.Instance != null && MissionManager.Instance.IsMissionActive))
        {
            if (!PreMissionProfiles.Contains(profileId))
            {
                response.Approved = false;
                response.Reason = "Mission is already in progress.";
                response.CreatePlayerObject = false;
                return;
            }
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    private void OnServerStarted()
    {
        Debug.Log("🟢 Server started");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"✅ Client connected: {clientId}");

        if (NetworkManager.Singleton == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            waitingForClientConnection = false;
            clientConnectionTimer = 0f;

            if (NetworkManager.Singleton.IsHost)
            {
                SetStatus($"Host connected. Code: {currentJoinCode}");
            }
            else
            {
                SetStatus($"Connected as client {clientId}");
            }
        }
        else
        {
            SetStatus($"Remote client connected: {clientId}");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        clientProfiles.Remove(clientId);

        string reason = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.DisconnectReason
            : "";

        Debug.LogWarning($"⚠ Client disconnected: {clientId}" +
            (string.IsNullOrEmpty(reason) ? "" : $" Reason: {reason}"));

        if (NetworkManager.Singleton == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            waitingForClientConnection = false;
            clientConnectionTimer = 0f;
            DismissConnectionScreen();

            SetStatus(string.IsNullOrEmpty(reason)
                ? $"Disconnected. Local ClientId: {clientId}"
                : $"Disconnected: {reason}");
        }
        else
        {
            SetStatus($"Remote client disconnected: {clientId}");
        }
    }

    private void OnTransportFailure()
    {
        waitingForClientConnection = false;
        clientConnectionTimer = 0f;
        DismissConnectionScreen();

        SetStatus("Transport failure");

        Debug.LogError("❌ Transport failure");
    }

    private void UpdateClientConnectionTimeout()
    {
        if (!waitingForClientConnection)
            return;

        if (NetworkManager.Singleton == null)
        {
            waitingForClientConnection = false;
            clientConnectionTimer = 0f;
            return;
        }

        if (NetworkManager.Singleton.IsConnectedClient)
        {
            waitingForClientConnection = false;
            clientConnectionTimer = 0f;
            return;
        }

        clientConnectionTimer += Time.deltaTime;

        if (clientConnectionTimer < clientConnectTimeout)
            return;

        waitingForClientConnection = false;
        clientConnectionTimer = 0f;

        SetStatus("Connection timed out");
        DismissConnectionScreen();

        Debug.LogError("❌ Client connection timed out. StartClient was called, but connection was not established.");

        NetworkManager.Singleton.Shutdown();
    }

    private void SetStatus(string newStatus)
    {
        status = newStatus;
        GameSessionData.LastStatus = newStatus;

        StatusChanged?.Invoke(status);

        Debug.Log($"[NetworkConnectionManager] {status}");
    }

    private string NormalizeRelayConnectionType(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return "dtls";
        }

        rawType = rawType.Trim().ToLowerInvariant();

        if (rawType == "udp")
        {
            return "udp";
        }

        if (rawType == "dtls")
        {
            return "dtls";
        }

        if (rawType == "wss")
        {
            return "wss";
        }

        return "dtls";
    }

    private string SanitizeAuthenticationProfile(string rawProfile)
    {
        if (string.IsNullOrWhiteSpace(rawProfile))
        {
            return "Default";
        }

        string result = "";

        for (int i = 0; i < rawProfile.Length; i++)
        {
            char c = rawProfile[i];

            bool allowed =
                char.IsLetterOrDigit(c) ||
                c == '-' ||
                c == '_';

            if (allowed)
            {
                result += c;
            }
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            result = "Default";
        }

        if (result.Length > 30)
        {
            result = result.Substring(0, 30);
        }

        return result;
    }

    private string SanitizeRelayJoinCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return "";
        }

        rawCode = rawCode.Trim().ToUpperInvariant();

        string result = "";

        for (int i = 0; i < rawCode.Length; i++)
        {
            char c = rawCode[i];

            c = ReplaceCyrillicLookalike(c);

            bool allowed =
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9');

            if (allowed)
            {
                result += c;
            }
        }

        if (result.Length > 20)
        {
            result = result.Substring(0, 20);
        }

        return result;
    }

    private char ReplaceCyrillicLookalike(char c)
    {
        switch (c)
        {
            case 'А': return 'A';
            case 'В': return 'B';
            case 'С': return 'C';
            case 'Е': return 'E';
            case 'Н': return 'H';
            case 'К': return 'K';
            case 'М': return 'M';
            case 'О': return 'O';
            case 'Р': return 'P';
            case 'Т': return 'T';
            case 'Х': return 'X';
            case 'У': return 'Y';

            case 'а': return 'A';
            case 'в': return 'B';
            case 'с': return 'C';
            case 'е': return 'E';
            case 'н': return 'H';
            case 'к': return 'K';
            case 'м': return 'M';
            case 'о': return 'O';
            case 'р': return 'P';
            case 'т': return 'T';
            case 'х': return 'X';
            case 'у': return 'Y';

            default:
                return c;
        }
    }

    private string GetShortExceptionMessage(Exception exception)
    {
        if (exception == null)
        {
            return "Unknown error";
        }

        Exception baseException = exception.GetBaseException();

        string message = baseException.Message;

        if (string.IsNullOrWhiteSpace(message))
        {
            message = exception.Message;
        }

        if (message.Length > 90)
        {
            message = message.Substring(0, 90) + "...";
        }

        return $"{baseException.GetType().Name}: {message}";
    }
}