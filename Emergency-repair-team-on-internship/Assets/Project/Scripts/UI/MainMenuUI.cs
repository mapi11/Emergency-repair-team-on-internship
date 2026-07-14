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

    [Header("Eyes")]
    [SerializeField] private RectTransform leftEyeCenter;
    [SerializeField] private RectTransform rightEyeCenter;
    [SerializeField] private RectTransform leftPupil;
    [SerializeField] private RectTransform rightPupil;
    [SerializeField] private float maxPupilOffset = 10f;
    [SerializeField] private float eyeTrackingSpeed = 8f;

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

    private void Update()
    {
        UpdateEyes();
    }

    private void OnDestroy()
    {
        if (connectionManager != null)
        {
            connectionManager.StatusChanged -= OnStatusChanged;
        }
    }

    private void UpdateEyes()
    {
        if (leftEyeCenter == null || rightEyeCenter == null || leftPupil == null || rightPupil == null)
            return;

        MovePupil(leftPupil, leftEyeCenter);
        MovePupil(rightPupil, rightEyeCenter);
    }

    private void MovePupil(RectTransform pupil, RectTransform eyeCenter)
    {
        Vector3 cursorPos = Input.mousePosition;
        Vector2 canvasPos;
        RectTransform canvasRect = eyeCenter.root as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cursorPos, null, out canvasPos))
            return;

        Vector2 eyePos = canvasRect.InverseTransformPoint(eyeCenter.position);
        Vector2 dir = canvasPos - eyePos;
        float dist = dir.magnitude;

        if (dist > maxPupilOffset)
            dir = dir.normalized * maxPupilOffset;

        Vector3 targetPos = pupil.localPosition;
        targetPos.x = dir.x;
        targetPos.y = dir.y;
        pupil.localPosition = Vector3.Lerp(pupil.localPosition, targetPos, eyeTrackingSpeed * Time.deltaTime);
    }

    private void InitializeFields()
    {
        string profileId = GetProfileId();

        LocalPlayerSettings.Load(profileId);
        selectedColor = LocalPlayerSettings.PlayerColor;

        GameSessionData.SelectedColorIndex = PlayerPrefs.GetInt($"PlayerColorIndex_{LocalPlayerSettings.ProfileId}", 0);
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
}