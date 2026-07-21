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
    [SerializeField] private TMP_Dropdown colorDropdown;
    [SerializeField] private TMP_Dropdown handDropdown;

    [Header("Voice")]
    [SerializeField] private TMP_Dropdown microphoneDropdown;
    [SerializeField] private Button refreshMicrophonesButton;
    [SerializeField] private TMP_Text microphoneText;

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

        int savedIndex = PlayerPrefs.GetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", -1);

        if (savedIndex >= 0 && savedIndex < GameSessionData.ColorValues.Length &&
            ColorsMatch(GameSessionData.ColorValues[savedIndex], selectedColor))
        {
            GameSessionData.SelectedColorIndex = savedIndex;
        }
        else
        {
            GameSessionData.SelectedColorIndex = FindColorIndex(selectedColor);
        }

        GameSessionData.SelectedHandIndex = PlayerPrefs.GetInt($"PlayerHandIndex_{LocalPlayerSettings.ProfileId}", 0);

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

        InitColorDropdown();
        InitHandDropdown();
        InitMicrophoneDropdown();
    }

    private void InitColorDropdown()
    {
        if (colorDropdown == null)
            return;

        colorDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < GameSessionData.ColorNames.Length; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(GameSessionData.ColorNames[i]));
        }

        colorDropdown.AddOptions(options);
        colorDropdown.SetValueWithoutNotify(GameSessionData.SelectedColorIndex);
        colorDropdown.onValueChanged.AddListener(OnColorChanged);
    }

    private void InitHandDropdown()
    {
        if (handDropdown == null)
            return;

        handDropdown.ClearOptions();

        handDropdown.AddOptions(new System.Collections.Generic.List<string> { "Right", "Left" });
        handDropdown.SetValueWithoutNotify(GameSessionData.SelectedHandIndex);
        handDropdown.onValueChanged.AddListener(OnHandChanged);
    }

    private void OnColorChanged(int index)
    {
        GameSessionData.SelectedColorIndex = index;
        selectedColor = GameSessionData.ColorValues[index];
        LocalPlayerSettings.Load(GetProfileId());
        LocalPlayerSettings.SetPlayerColor(selectedColor);
        PlayerPrefs.SetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", index);
        PlayerPrefs.Save();
        RefreshColor();
    }

    private void OnHandChanged(int index)
    {
        GameSessionData.SelectedHandIndex = index;
        PlayerPrefs.SetInt($"PlayerHandIndex_{LocalPlayerSettings.ProfileId}", index);
        PlayerPrefs.Save();
    }

    private void BindButtons()
    {
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

        if (refreshMicrophonesButton != null)
        {
            refreshMicrophonesButton.onClick.AddListener(InitMicrophoneDropdown);
        }
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

    private void InitMicrophoneDropdown()
    {
        if (microphoneDropdown == null)
            return;

        VoiceChatSettings.Load();

        microphoneDropdown.onValueChanged.RemoveListener(OnMicrophoneChanged);
        microphoneDropdown.ClearOptions();

        string[] devices = Microphone.devices;

        if (devices == null || devices.Length == 0)
        {
            microphoneDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "No microphones found"
        });

            microphoneDropdown.interactable = false;

            if (microphoneText != null)
            {
                microphoneText.text = "Microphone: not found";
            }

            VoiceChatSettings.SetSelectedMicrophone("");
            return;
        }

        microphoneDropdown.interactable = true;

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();

        for (int i = 0; i < devices.Length; i++)
        {
            options.Add(devices[i]);
        }

        microphoneDropdown.AddOptions(options);

        int selectedIndex = 0;

        string savedMicrophone = VoiceChatSettings.SelectedMicrophoneName;

        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i] == savedMicrophone)
            {
                selectedIndex = i;
                break;
            }
        }

        microphoneDropdown.SetValueWithoutNotify(selectedIndex);

        VoiceChatSettings.SetSelectedMicrophone(devices[selectedIndex]);

        RefreshMicrophoneText();

        microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);
    }

    private void OnMicrophoneChanged(int index)
    {
        string[] devices = Microphone.devices;

        if (devices == null || devices.Length == 0)
            return;

        if (index < 0 || index >= devices.Length)
            return;

        VoiceChatSettings.SetSelectedMicrophone(devices[index]);

        RefreshMicrophoneText();
    }

    private void RefreshMicrophoneText()
    {
        if (microphoneText == null)
            return;

        string microphoneName = VoiceChatSettings.SelectedMicrophoneName;

        if (string.IsNullOrWhiteSpace(microphoneName))
        {
            microphoneText.text = "Microphone: not selected";
        }
        else
        {
            microphoneText.text = $"Microphone: {microphoneName}";
        }
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

    private static bool ColorsMatch(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private static int FindColorIndex(Color32 color)
    {
        for (int i = 0; i < GameSessionData.ColorValues.Length; i++)
        {
            if (ColorsMatch(GameSessionData.ColorValues[i], color))
                return i;
        }

        return 0;
    }
}
