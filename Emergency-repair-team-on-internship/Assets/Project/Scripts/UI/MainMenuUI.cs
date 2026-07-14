using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private TMP_InputField profileInput;

    [Header("Player")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Image colorPreview;
    [SerializeField] private TMP_Text colorText;
    [SerializeField] private Button randomColorButton;

    [Header("Local")]
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_InputField portInput;
    [SerializeField] private Button startLocalHostButton;
    [SerializeField] private Button startLocalClientButton;

    [Header("Online")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button startOnlineHostButton;
    [SerializeField] private Button joinOnlineButton;
    [SerializeField] private Button pasteCodeButton;
    [SerializeField] private Button copyCodeButton;

    [Header("Connection Type")]
    [SerializeField] private Button udpButton;
    [SerializeField] private Button dtlsButton;
    [SerializeField] private Button wssButton;
    [SerializeField] private TMP_Text connectionTypeText;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text footerText;

    private Color32 selectedColor;

    private NetworkConnectionManager connectionManager;

    private void Awake()
    {
        connectionManager = NetworkConnectionManager.Instance;

        if (connectionManager == null)
        {
            connectionManager = FindFirstObjectByType<NetworkConnectionManager>();
        }
    }

    private void Start()
    {
        if (connectionManager == null)
        {
            Debug.LogError("NetworkConnectionManager not found.");
            SetStatus("NetworkConnectionManager not found");
            return;
        }

        connectionManager.StatusChanged += OnStatusChanged;

        InitializeFields();
        BindButtons();
        RefreshColor();
        RefreshConnectionType();
        RefreshFooter();
        SetStatus(connectionManager.Status);
    }

    private void OnDestroy()
    {
        if (connectionManager != null)
        {
            connectionManager.StatusChanged -= OnStatusChanged;
        }
    }

    private void InitializeFields()
    {
        string profileId = GetProfileId();

        LocalPlayerSettings.Load(profileId);
        selectedColor = LocalPlayerSettings.PlayerColor;

        if (playerNameInput != null)
        {
            playerNameInput.text = LocalPlayerSettings.PlayerName;
        }

        if (addressInput != null)
        {
            addressInput.text = "127.0.0.1";
        }

        if (portInput != null)
        {
            portInput.text = "7777";
        }
    }

    private void BindButtons()
    {
        if (randomColorButton != null)
        {
            randomColorButton.onClick.AddListener(OnRandomColorClicked);
        }

        if (startLocalHostButton != null)
        {
            startLocalHostButton.onClick.AddListener(OnStartLocalHostClicked);
        }

        if (startLocalClientButton != null)
        {
            startLocalClientButton.onClick.AddListener(OnStartLocalClientClicked);
        }

        if (startOnlineHostButton != null)
        {
            startOnlineHostButton.onClick.AddListener(OnStartOnlineHostClicked);
        }

        if (joinOnlineButton != null)
        {
            joinOnlineButton.onClick.AddListener(OnJoinOnlineClicked);
        }

        if (pasteCodeButton != null)
        {
            pasteCodeButton.onClick.AddListener(OnPasteCodeClicked);
        }

        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        }

        if (udpButton != null)
        {
            udpButton.onClick.AddListener(() => SetConnectionType("udp"));
        }

        if (dtlsButton != null)
        {
            dtlsButton.onClick.AddListener(() => SetConnectionType("dtls"));
        }

        if (wssButton != null)
        {
            wssButton.onClick.AddListener(() => SetConnectionType("wss"));
        }
    }

    private void OnRandomColorClicked()
    {
        string profileId = GetProfileId();

        LocalPlayerSettings.Load(profileId);
        LocalPlayerSettings.GenerateAndSaveRandomColor();

        selectedColor = LocalPlayerSettings.PlayerColor;

        RefreshColor();
    }

    private void OnStartLocalHostClicked()
    {
        ApplyLocalConnectionFields();

        connectionManager.StartLocalHost(
        GetProfileId(),
        GetPlayerName(),
        selectedColor
        );
    }

    private void OnStartLocalClientClicked()
    {
        ApplyLocalConnectionFields();

        connectionManager.StartLocalClient(
    GetProfileId(),
    GetPlayerName(),
    selectedColor
);
    }

    private void OnStartOnlineHostClicked()
    {
        _ = connectionManager.StartOnlineHostAsync(
    GetProfileId(),
    GetPlayerName(),
    selectedColor
);
    }

    private void OnJoinOnlineClicked()
    {
        string joinCode = "";

        if (joinCodeInput != null)
        {
            joinCode = joinCodeInput.text;
        }

        _ = connectionManager.JoinOnlineAsync(
    GetProfileId(),
    GetPlayerName(),
    joinCode,
    selectedColor
);
    }

    private void OnPasteCodeClicked()
    {
        if (joinCodeInput == null)
            return;

        joinCodeInput.text = GUIUtility.systemCopyBuffer.Trim().ToUpperInvariant();
    }

    private void OnCopyCodeClicked()
    {
        if (connectionManager == null)
            return;

        string code = connectionManager.CurrentJoinCode;

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("No Join Code to copy");
            return;
        }

        GUIUtility.systemCopyBuffer = code;

        SetStatus($"Copied Join Code: {code}");
    }

    private void SetConnectionType(string type)
    {
        connectionManager.SetRelayConnectionType(type);
        RefreshConnectionType();
    }

    private void ApplyLocalConnectionFields()
    {
        string address = "127.0.0.1";
        ushort port = 7777;

        if (addressInput != null && !string.IsNullOrWhiteSpace(addressInput.text))
        {
            address = addressInput.text.Trim();
        }

        if (portInput != null && ushort.TryParse(portInput.text, out ushort parsedPort))
        {
            port = parsedPort;
        }

        connectionManager.SetLocalConnectionData(address, port);
    }

    private string GetProfileId()
    {
        if (profileInput == null)
        {
            return "Default";
        }

        if (string.IsNullOrWhiteSpace(profileInput.text))
        {
            return "Default";
        }

        return profileInput.text.Trim();
    }

    private string GetPlayerName()
    {
        if (playerNameInput == null)
        {
            return "Player";
        }

        if (string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            return "Player";
        }

        return playerNameInput.text.Trim();
    }

    private void RefreshColor()
    {
        Color32 color = selectedColor;

        if (colorPreview != null)
        {
            colorPreview.color = color;
        }

        if (colorText != null)
        {
            colorText.text = $"Color: R{color.r} G{color.g} B{color.b}";
        }
    }

    private void RefreshConnectionType()
    {
        if (connectionManager == null)
            return;

        if (connectionTypeText != null)
        {
            connectionTypeText.text = $"Connection: {connectionManager.RelayConnectionType}";
        }
    }

    private void RefreshFooter()
    {
        if (footerText != null)
        {
            footerText.text = $"{GameSessionData.GameVersion}";
        }
    }

    private void OnStatusChanged(string newStatus)
    {
        SetStatus(newStatus);
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = $"Status: {value}";
        }
    }
}