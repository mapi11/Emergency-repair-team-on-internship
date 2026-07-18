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

public class SimpleLocalNetworkMenu : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    [Header("Online Relay")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string relayJoinCode = "";
    [SerializeField] private string relayConnectionType = "wss";

    [Header("Local Player")]
    [SerializeField] private string profileId = "Host";
    [SerializeField] private string playerName = "Player";

    [Header("Debug")]
    [SerializeField] private bool isBusy;
    [SerializeField] private string status = "Offline";
    [SerializeField] private float clientConnectTimeout = 12f;

    private UnityTransport transport;

    private bool waitingForClientConnection;
    private float clientConnectionTimer;

    private void Awake()
    {
        LocalPlayerSettings.Load(profileId);
        playerName = LocalPlayerSettings.PlayerName;

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

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            GUILayout.BeginArea(new Rect(20, 20, 440, 120), GUI.skin.box);
            GUILayout.Label("NetworkManager not found");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 460, 680), GUI.skin.box);

        GUILayout.Label("Emergency Repair Crew - Multiplayer Test");
        GUILayout.Label($"Status: {status}");
        GUILayout.Space(8);

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            DrawOfflineMenu();
        }
        else
        {
            DrawOnlineMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawOfflineMenu()
    {
        GUI.enabled = !isBusy;

        GUILayout.Label("Profile Id:");
        profileId = GUILayout.TextField(profileId, 20);

        if (GUILayout.Button("Load Profile"))
        {
            LoadProfileFromInput();
        }

        GUILayout.Space(8);

        GUILayout.Label("Player Name:");
        playerName = GUILayout.TextField(playerName, 20);

        GUILayout.Space(8);

        Color32 color = LocalPlayerSettings.PlayerColor;

        GUILayout.Label($"Color: R{color.r} G{color.g} B{color.b}");

        Color oldBackgroundColor = GUI.backgroundColor;

        GUI.backgroundColor = color;

        if (GUILayout.Button("Randomize Color"))
        {
            LocalPlayerSettings.Load(profileId);
            LocalPlayerSettings.GenerateAndSaveRandomColor();
        }

        GUI.backgroundColor = oldBackgroundColor;

        GUILayout.Space(16);

        GUILayout.Label("LOCAL TEST");

        GUILayout.Label("Address:");
        address = GUILayout.TextField(address);

        GUILayout.Label("Port:");
        string portText = GUILayout.TextField(port.ToString());

        if (ushort.TryParse(portText, out ushort parsedPort))
        {
            port = parsedPort;
        }

        if (GUILayout.Button("Start Local Host"))
        {
            StartLocalHost();
        }

        if (GUILayout.Button("Start Local Client"))
        {
            StartLocalClient();
        }

        if (GUILayout.Button("Start Local Server"))
        {
            StartLocalServer();
        }

        GUILayout.Space(16);

        GUILayout.Label("ONLINE RELAY");

        GUILayout.Label("Relay Join Code:");
        relayJoinCode = GUILayout.TextField(relayJoinCode, 20).Trim().ToUpperInvariant();

        GUILayout.Label("Relay Connection Type:");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("UDP"))
        {
            relayConnectionType = "udp";
        }

        if (GUILayout.Button("DTLS"))
        {
            relayConnectionType = "dtls";
        }

        if (GUILayout.Button("WSS"))
        {
            relayConnectionType = "wss";
        }

        GUILayout.EndHorizontal();

        GUILayout.Label($"Selected: {relayConnectionType}");

        if (GUILayout.Button("Start Online Host"))
        {
            _ = StartOnlineHostAsync();
        }

        if (GUILayout.Button("Join Online"))
        {
            _ = JoinOnlineAsync();
        }

        GUILayout.Space(8);
        GUILayout.Label($"Max Players: {maxPlayers}");
        GUILayout.Label($"Connection Type: {relayConnectionType}");

        GUI.enabled = true;
    }

    private void DrawOnlineMenu()
    {
        string mode = GetNetworkMode();

        GUILayout.Label($"Mode: {mode}");
        GUILayout.Label($"Is Connected Client: {NetworkManager.Singleton.IsConnectedClient}");
        GUILayout.Label($"Is Listening: {NetworkManager.Singleton.IsListening}");

        GUILayout.Space(8);

        GUILayout.Label($"Profile: {LocalPlayerSettings.ProfileId}");
        GUILayout.Label($"Player Name: {LocalPlayerSettings.PlayerName}");

        Color32 color = LocalPlayerSettings.PlayerColor;
        GUILayout.Label($"Color: R{color.r} G{color.g} B{color.b}");

        GUILayout.Space(8);

        GUILayout.Label($"Local Client Id: {NetworkManager.Singleton.LocalClientId}");
        GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

        string connectedIds = "";

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (connectedIds.Length > 0)
            {
                connectedIds += ", ";
            }

            connectedIds += clientId.ToString();
        }

        GUILayout.Label($"Connected Client Ids: {connectedIds}");

        if (!string.IsNullOrWhiteSpace(relayJoinCode))
        {
            GUILayout.Space(8);
            GUILayout.Label($"Relay Join Code: {relayJoinCode}");
        }

        if (waitingForClientConnection)
        {
            GUILayout.Space(8);
            GUILayout.Label($"Waiting for connection: {clientConnectionTimer:F1}s");
        }

        GUILayout.Space(12);

        if (GUILayout.Button("Shutdown"))
        {
            ShutdownNetwork();
        }
    }

    private string GetNetworkMode()
    {
        if (NetworkManager.Singleton == null)
        {
            return "No NetworkManager";
        }

        if (NetworkManager.Singleton.IsHost)
        {
            return "Host";
        }

        if (NetworkManager.Singleton.IsServer)
        {
            return "Server";
        }

        if (NetworkManager.Singleton.IsClient)
        {
            return "Client";
        }

        return "Offline";
    }

    private void LoadProfileFromInput()
    {
        LocalPlayerSettings.Load(profileId);
        playerName = LocalPlayerSettings.PlayerName;

        status = $"Loaded profile: {LocalPlayerSettings.ProfileId}";

        Debug.Log($"👤 Loaded profile: {LocalPlayerSettings.ProfileId}");
    }

    private void ApplyLocalSettings()
    {
        LocalPlayerSettings.Load(profileId);
        LocalPlayerSettings.SetPlayerName(playerName);

        Debug.Log($"👤 Applied local settings: Profile={LocalPlayerSettings.ProfileId}, Name={LocalPlayerSettings.PlayerName}");
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
            Debug.LogError("UnityTransport not found on NetworkBootstrap.");
            status = "UnityTransport not found";
            return;
        }

        transport.SetConnectionData(address, port);

        Debug.Log($"🌐 Local connection data: {address}:{port}");
    }

    private static void SetConnectionPayload()
    {
        if (NetworkManager.Singleton == null)
            return;

        string profileId = NetworkConnectionManager.GetConnectionPayloadId();
        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(profileId);
    }

    private void StartLocalHost()
    {
        ApplyLocalSettings();
        ApplyLocalConnectionData();

        RegisterNetworkCallbacks();
        SetConnectionPayload();

        bool started = NetworkManager.Singleton.StartHost();

        status = started ? "Local Host started" : "Local Host failed";

        Debug.Log(started ? "✅ Local Host started" : "❌ Local Host failed");
    }

    private void StartLocalClient()
    {
        ApplyLocalSettings();
        ApplyLocalConnectionData();

        RegisterNetworkCallbacks();
        SetConnectionPayload();

        bool started = NetworkManager.Singleton.StartClient();

        if (started)
        {
            waitingForClientConnection = true;
            clientConnectionTimer = 0f;
            status = "Local Client starting...";
        }
        else
        {
            status = "Local Client failed";
        }

        Debug.Log(started ? "✅ Local Client starting" : "❌ Local Client failed");
    }

    private void StartLocalServer()
    {
        ApplyLocalConnectionData();

        RegisterNetworkCallbacks();

        bool started = NetworkManager.Singleton.StartServer();

        status = started ? "Local Server started" : "Local Server failed";

        Debug.Log(started ? "✅ Local Server started" : "❌ Local Server failed");
    }

    private async Task StartOnlineHostAsync()
    {
        if (isBusy)
            return;

        isBusy = true;
        status = "Starting online host...";

        try
        {
            ApplyLocalSettings();
            FindTransportIfNeeded();

            if (transport == null)
            {
                status = "UnityTransport not found";
                Debug.LogError("UnityTransport not found.");
                return;
            }

            await EnsureUnityServicesAsync();

            ConfigureTransportForRelay();

            int maxConnections = Mathf.Max(1, maxPlayers - 1);

            Debug.Log($"🌐 Creating Relay allocation. Max connections: {maxConnections}");

            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(
                allocation,
                relayConnectionType
            );

            transport.SetRelayServerData(relayServerData);

            relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            relayJoinCode = relayJoinCode.Trim().ToUpperInvariant();

            RegisterNetworkCallbacks();
            SetConnectionPayload();

            bool started = NetworkManager.Singleton.StartHost();

            status = started
                ? $"Online Host started. Code: {relayJoinCode}"
                : "Online Host failed";

            Debug.Log(started
                ? $"✅ Online Host started. Join Code: {relayJoinCode}"
                : "❌ Online Host failed");
        }
        catch (Exception exception)
        {
            string errorMessage = GetShortExceptionMessage(exception);

            status = $"Host error: {errorMessage}";

            Debug.LogError($"❌ Online Host error: {exception}");
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task JoinOnlineAsync()
    {
        if (isBusy)
            return;

        isBusy = true;
        status = "Joining online...";

        try
        {
            ApplyLocalSettings();
            FindTransportIfNeeded();

            if (transport == null)
            {
                status = "UnityTransport not found";
                Debug.LogError("UnityTransport not found.");
                return;
            }

            string code = relayJoinCode.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(code))
            {
                status = "Join Code is empty";
                Debug.LogWarning("Relay Join Code is empty.");
                return;
            }

            await EnsureUnityServicesAsync();

            ConfigureTransportForRelay();

            Debug.Log($"🌐 Joining Relay allocation. Code: {code}");

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(
                joinAllocation,
                relayConnectionType
            );

            transport.SetRelayServerData(relayServerData);

            RegisterNetworkCallbacks();
            SetConnectionPayload();

            bool started = NetworkManager.Singleton.StartClient();

            if (started)
            {
                waitingForClientConnection = true;
                clientConnectionTimer = 0f;
                status = $"Online Client starting... Code: {code}";
            }
            else
            {
                status = "Online Client failed";
            }

            Debug.Log(started
                ? $"✅ Online Client starting. Join Code: {code}"
                : "❌ Online Client failed");
        }
        catch (Exception exception)
        {
            string errorMessage = GetShortExceptionMessage(exception);

            status = $"Join error: {errorMessage}";

            Debug.LogError($"❌ Join Online error: {exception}");
        }
        finally
        {
            isBusy = false;
        }
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

    private void ConfigureTransportForRelay()
    {
        FindTransportIfNeeded();

        if (transport == null)
        {
            Debug.LogError("UnityTransport not found for Relay.");
            return;
        }

        string type = relayConnectionType.Trim().ToLowerInvariant();

        if (type != "udp" && type != "dtls" && type != "wss")
        {
            type = "wss";
            relayConnectionType = type;
        }

        transport.UseWebSockets = type == "wss";

        Debug.Log($"🔧 Relay transport configured: Type={type}, UseWebSockets={transport.UseWebSockets}");
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

    private void RegisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
    }

    private void OnServerStarted()
    {
        Debug.Log("🟢 Server started");

        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.IsHost)
        {
            status = $"Host listening. Code: {relayJoinCode}";
        }
        else
        {
            status = "Server listening";
        }
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
                status = $"Host connected. Local ClientId: {clientId}. Code: {relayJoinCode}";
            }
            else
            {
                status = $"Connected as client {clientId}";
            }
        }
        else
        {
            status = $"Remote client connected: {clientId}. Total: {NetworkManager.Singleton.ConnectedClientsIds.Count}";
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
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

            status = string.IsNullOrEmpty(reason)
                ? $"Disconnected. Local ClientId: {clientId}"
                : $"Disconnected: {reason}";
        }
        else
        {
            status = $"Remote client disconnected: {clientId}. Total: {NetworkManager.Singleton.ConnectedClientsIds.Count}";
        }
    }

    private void OnTransportFailure()
    {
        Debug.LogError("❌ Transport failure detected");

        waitingForClientConnection = false;
        clientConnectionTimer = 0f;

        status = "Transport failure";
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

        status = "Connection timed out";

        Debug.LogError("❌ Client connection timed out. StartClient was called, but connection was not established.");

        NetworkManager.Singleton.Shutdown();
    }

    private void ShutdownNetwork()
    {
        waitingForClientConnection = false;
        clientConnectionTimer = 0f;
        isBusy = false;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        status = "Shutdown";

        Debug.Log("🛑 Network shutdown");
    }
}