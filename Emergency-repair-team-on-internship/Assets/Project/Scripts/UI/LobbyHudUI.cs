using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyHudUI : MonoBehaviour
{
    [SerializeField] private TMP_Text bottomText;

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
}