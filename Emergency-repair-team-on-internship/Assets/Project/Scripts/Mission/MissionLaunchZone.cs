using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class MissionLaunchZone : NetworkBehaviour
{
    [SerializeField] private GameObject missionManagerPrefab;
    [SerializeField] private TMP_Text countdownText;

    private readonly HashSet<ulong> playersInZone = new();
    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (MissionManager.Instance == null || !MissionManager.Instance.IsSpawned)
                SpawnMissionManager();

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        SubscribeToCountdown();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        UnsubscribeFromCountdown();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (ulong id in playersInZone)
                UnlockPlayerRoleSlots(id);
        }

        playersInZone.Clear();
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

    private void SpawnMissionManager()
    {
        if (missionManagerPrefab == null)
        {
            Debug.LogError("[MissionLaunchZone] missionManagerPrefab not assigned!");
            return;
        }

        GameObject go = Instantiate(missionManagerPrefab);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        playersInZone.Remove(clientId);
        MissionManager.Instance?.CancelCountdown();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            UnlockPlayerRoleSlots(clientId);
    }

    private bool IsServerPlayerInZone(ulong clientId)
    {
        if (!IsServer || zoneCollider == null) return false;

        var playerObj = NetworkManager.Singleton.ConnectedClients[clientId]?.PlayerObject;
        if (playerObj == null) return false;

        Collider playerCollider = playerObj.GetComponentInChildren<Collider>();
        if (playerCollider == null) return false;

        return zoneCollider.bounds.Intersects(playerCollider.bounds);
    }

    private void OnTriggerEnter(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        var player = networkObject.GetComponent<PlayerController>();
        if (player == null) return;

        ulong clientId = networkObject.OwnerClientId;

        if (IsServer)
        {
            playersInZone.Add(clientId);
            LockPlayerRoleSlots(clientId);

            var roleComp = networkObject.GetComponent<NetworkPlayerRole>();
            if (roleComp != null)
                roleComp.OnRoleChangedEvent += OnPlayerRoleChanged;

            CheckLaunchConditions();
        }
        else if (IsSpawned)
        {
            EnterZoneServerRpc(clientId);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        var player = networkObject.GetComponent<PlayerController>();
        if (player == null) return;

        ulong clientId = networkObject.OwnerClientId;

        if (IsServer)
        {
            playersInZone.Remove(clientId);
            UnlockPlayerRoleSlots(clientId);

            var roleComp = networkObject.GetComponent<NetworkPlayerRole>();
            if (roleComp != null)
                roleComp.OnRoleChangedEvent -= OnPlayerRoleChanged;

            MissionManager.Instance?.CancelCountdown();
        }
        else if (IsSpawned)
        {
            ExitZoneServerRpc(clientId);
        }
    }

    private void OnPlayerRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        CheckLaunchConditions();
    }

    private void LockPlayerRoleSlots(ulong clientId)
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.LockRoleSlotsForClientClientRpc(clientId);
    }

    private void UnlockPlayerRoleSlots(ulong clientId)
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.UnlockRoleSlotsForClientClientRpc(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void EnterZoneServerRpc(ulong clientId)
    {
        if (!IsServerPlayerInZone(clientId))
            return;

        if (playersInZone.Add(clientId))
            LockPlayerRoleSlots(clientId);

        CheckLaunchConditions();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ExitZoneServerRpc(ulong clientId)
    {
        playersInZone.Remove(clientId);
        UnlockPlayerRoleSlots(clientId);

        var playerObj = NetworkManager.Singleton.ConnectedClients[clientId]?.PlayerObject;
        if (playerObj != null)
        {
            var roleComp = playerObj.GetComponent<NetworkPlayerRole>();
            if (roleComp != null)
                roleComp.OnRoleChangedEvent -= OnPlayerRoleChanged;
        }

        MissionManager.Instance?.CancelCountdown();
    }

    private void CheckLaunchConditions()
    {
        if (MissionManager.Instance == null || MissionManager.Instance.IsMissionActive) return;

        if (!MissionManager.Instance.IsSpawned)
        {
            Debug.LogWarning("[MissionLaunchZone] MissionManager not spawned yet");
            return;
        }

        if (!AreLaunchConditionsMet())
            return;

        MissionManager.Instance.StartCountdown("mission", AreLaunchConditionsMet);
    }

    public bool AreLaunchConditionsMet()
    {
        if (MissionManager.Instance == null || MissionManager.Instance.IsMissionActive)
            return false;

        if (!MissionManager.Instance.IsSpawned)
            return false;

        var clients = NetworkManager.Singleton.ConnectedClients;
        if (clients == null || clients.Count == 0)
            return false;

        foreach (var kvp in clients)
        {
            var playerObj = kvp.Value.PlayerObject;
            if (playerObj == null) return false;

            if (!playersInZone.Contains(kvp.Key) || !IsServerPlayerInZone(kvp.Key))
                return false;

            var role = playerObj.GetComponent<NetworkPlayerRole>();
            if (role == null || role.CurrentRole == PlayerRole.None) return false;
        }

        return true;
    }
}
