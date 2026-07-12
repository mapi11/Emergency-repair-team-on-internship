using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class SimpleLocalNetworkMenu : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    [Header("Player")]
    [SerializeField] private string playerName = "Player";

    private UnityTransport transport;

    private void Awake()
    {
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
            GUILayout.Label("NetworkManager not found");
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 340, 280), GUI.skin.box);

        GUILayout.Label("Emergency Repair Crew - Local Multiplayer Test");

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            GUILayout.Label("Address:");
            address = GUILayout.TextField(address);

            GUILayout.Label("Port:");
            string portText = GUILayout.TextField(port.ToString());

            GUILayout.Label("Player Name:");
            playerName = GUILayout.TextField(playerName, 20);

            if (ushort.TryParse(portText, out ushort parsedPort))
            {
                port = parsedPort;
            }



            GUILayout.Space(10);

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
        else
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
            GUILayout.Label($"Local Client Id: {NetworkManager.Singleton.LocalClientId}");
            GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

            GUILayout.Space(10);

            if (GUILayout.Button("Shutdown"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }

    private void ApplyPlayerName()
    {
        string trimmedName = playerName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            trimmedName = "Player";
        }

        LocalPlayerSettings.PlayerName = trimmedName;
    }

    private void ApplyConnectionData()
    {
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
        ApplyPlayerName();
        ApplyConnectionData();

        bool started = NetworkManager.Singleton.StartHost();

        Debug.Log(started ? "✅ Host started" : "❌ Host failed");
    }

    private void StartClient()
    {
        ApplyPlayerName();
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