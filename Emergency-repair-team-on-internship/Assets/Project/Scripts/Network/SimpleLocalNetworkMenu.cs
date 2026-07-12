using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class SimpleLocalNetworkMenu : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    [Header("Local Player")]
    [SerializeField] private string profileId = "Host";
    [SerializeField] private string playerName = "Player";

    private UnityTransport transport;

    private void Awake()
    {
        LocalPlayerSettings.Load(profileId);
        playerName = LocalPlayerSettings.PlayerName;

        transport = GetComponent<UnityTransport>();

        if (transport == null && NetworkManager.Singleton != null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }
    }

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            GUILayout.BeginArea(new Rect(20, 20, 360, 120), GUI.skin.box);
            GUILayout.Label("NetworkManager not found");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 380, 430), GUI.skin.box);

        GUILayout.Label("Emergency Repair Crew - Multiplayer Test");
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

        GUILayout.Space(12);

        GUILayout.Label("Address:");
        address = GUILayout.TextField(address);

        GUILayout.Label("Port:");
        string portText = GUILayout.TextField(port.ToString());

        if (ushort.TryParse(portText, out ushort parsedPort))
        {
            port = parsedPort;
        }

        GUILayout.Space(12);

        if (GUILayout.Button("Start Host"))
        {
            StartHost();
        }

        if (GUILayout.Button("Start Client"))
        {
            StartClient();
        }

        if (GUILayout.Button("Start Server"))
        {
            StartServer();
        }
    }

    private void DrawOnlineMenu()
    {
        string mode = "Unknown";

        if (NetworkManager.Singleton.IsHost)
        {
            mode = "Host";
        }
        else if (NetworkManager.Singleton.IsServer)
        {
            mode = "Server";
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            mode = "Client";
        }

        GUILayout.Label($"Mode: {mode}");
        GUILayout.Label($"Profile: {LocalPlayerSettings.ProfileId}");
        GUILayout.Label($"Player Name: {LocalPlayerSettings.PlayerName}");

        Color32 color = LocalPlayerSettings.PlayerColor;
        GUILayout.Label($"Color: R{color.r} G{color.g} B{color.b}");

        GUILayout.Space(8);

        GUILayout.Label($"Local Client Id: {NetworkManager.Singleton.LocalClientId}");
        GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

        GUILayout.Space(12);

        if (GUILayout.Button("Shutdown"))
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void LoadProfileFromInput()
    {
        LocalPlayerSettings.Load(profileId);
        playerName = LocalPlayerSettings.PlayerName;

        Debug.Log($"👤 Loaded profile: {LocalPlayerSettings.ProfileId}");
    }

    private void ApplyLocalSettings()
    {
        LocalPlayerSettings.Load(profileId);
        LocalPlayerSettings.SetPlayerName(playerName);

        Debug.Log($"👤 Applied local settings: Profile={LocalPlayerSettings.ProfileId}, Name={LocalPlayerSettings.PlayerName}");
    }

    private void ApplyConnectionData()
    {
        if (transport == null)
        {
            transport = GetComponent<UnityTransport>();

            if (transport == null && NetworkManager.Singleton != null)
            {
                transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            }
        }

        if (transport == null)
        {
            Debug.LogError("UnityTransport not found on NetworkBootstrap.");
            return;
        }

        transport.SetConnectionData(address, port);

        Debug.Log($"🌐 Connection data: {address}:{port}");
    }

    private void StartHost()
    {
        ApplyLocalSettings();
        ApplyConnectionData();

        bool started = NetworkManager.Singleton.StartHost();

        Debug.Log(started ? "✅ Host started" : "❌ Host failed");
    }

    private void StartClient()
    {
        ApplyLocalSettings();
        ApplyConnectionData();

        bool started = NetworkManager.Singleton.StartClient();

        Debug.Log(started ? "✅ Client started" : "❌ Client failed");
    }

    private void StartServer()
    {
        ApplyConnectionData();

        bool started = NetworkManager.Singleton.StartServer();

        Debug.Log(started ? "✅ Server started" : "❌ Server failed");
    }
}