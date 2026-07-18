using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class MissionReturnZone : MonoBehaviour
{
    [SerializeField] private TMP_Text countdownText;

    private readonly HashSet<ulong> playersInZone = new();

    private void Start()
    {
        SubscribeToCountdown();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCountdown();
    }

    private void SubscribeToCountdown()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.countdownDisplay.OnValueChanged += OnCountdownChanged;
        }
        else
        {
            StartCoroutine(WaitForMissionManager());
        }
    }

    private IEnumerator WaitForMissionManager()
    {
        while (MissionManager.Instance == null)
            yield return null;
        MissionManager.Instance.countdownDisplay.OnValueChanged += OnCountdownChanged;
    }

    private void UnsubscribeFromCountdown()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.countdownDisplay.OnValueChanged -= OnCountdownChanged;
    }

    private void OnCountdownChanged(int oldValue, int newValue)
    {
        if (countdownText == null) return;
        countdownText.text = newValue > 0 ? newValue.ToString() : "";
    }

    private void OnTriggerEnter(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        if (networkObject.GetComponent<PlayerController>() == null) return;

        ulong clientId = networkObject.OwnerClientId;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            playersInZone.Add(clientId);
            CheckReturnConditions();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        if (networkObject.GetComponent<PlayerController>() == null) return;

        ulong clientId = networkObject.OwnerClientId;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            playersInZone.Remove(clientId);
            MissionManager.Instance?.CancelCountdown();
        }
    }

    private void CheckReturnConditions()
    {
        if (MissionManager.Instance == null || !MissionManager.Instance.IsMissionActive)
            return;

        if (!AreReturnConditionsMet())
            return;

        MissionManager.Instance.StartCountdown("lobby", AreReturnConditionsMet);
    }

    public bool AreReturnConditionsMet()
    {
        if (MissionManager.Instance == null || !MissionManager.Instance.IsMissionActive)
            return false;

        var clients = NetworkManager.Singleton.ConnectedClients;

        if (clients == null || clients.Count == 0)
            return false;

        foreach (var kvp in clients)
        {
            var playerObj = kvp.Value.PlayerObject;
            if (playerObj == null) return false;

            if (!playersInZone.Contains(kvp.Key)) return false;
        }

        return true;
    }
}
