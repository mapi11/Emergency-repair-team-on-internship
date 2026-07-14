using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyHudUI : MonoBehaviour
{
    [SerializeField] private TMP_Text bottomText;
    [SerializeField] private Button copyCodeButton;

    private void Awake()
    {
        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(OnCopyCode);
    }

    private void Update()
    {
        if (bottomText == null)
            return;

        string mode = "Offline";

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                mode = "Host";
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                mode = "Client";
            }
            else if (NetworkManager.Singleton.IsServer)
            {
                mode = "Server";
            }
        }

        string code = GameSessionData.JoinCode;

        if (string.IsNullOrWhiteSpace(code))
        {
            code = "-";
        }

        bottomText.text =
            $"{GameSessionData.GameVersion}   |   Mode: {mode}   |   Join Code: {code}   |   Connection: {GameSessionData.ConnectionType}";
    }

    private void OnCopyCode()
    {
        string code = GameSessionData.JoinCode;

        if (string.IsNullOrWhiteSpace(code))
            return;

        GUIUtility.systemCopyBuffer = code;
    }
}