using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MissionLaunchZone : NetworkBehaviour
{
    [SerializeField] private GameObject missionManagerPrefab;

    private readonly HashSet<ulong> playersInZone = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (MissionManager.Instance == null || !MissionManager.Instance.IsSpawned)
                SpawnMissionManager();

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
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

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        playersInZone.Remove(clientId);
    }

    private void OnTriggerEnter(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        var player = networkObject.GetComponent<PlayerController>();
        if (player == null) return;

        ulong clientId = networkObject.OwnerClientId;
        playersInZone.Add(clientId);

        var roleComp = networkObject.GetComponent<NetworkPlayerRole>();
        if (roleComp != null)
            roleComp.OnRoleChangedEvent += OnPlayerRoleChanged;

        if (IsServer)
            CheckLaunchConditions();
    }

    private void OnTriggerExit(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        var player = networkObject.GetComponent<PlayerController>();
        if (player == null) return;

        ulong clientId = networkObject.OwnerClientId;
        playersInZone.Remove(clientId);

        var roleComp = networkObject.GetComponent<NetworkPlayerRole>();
        if (roleComp != null)
            roleComp.OnRoleChangedEvent -= OnPlayerRoleChanged;
    }

    private void OnPlayerRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        if (IsServer)
            CheckLaunchConditions();
    }

    private void CheckLaunchConditions()
    {
        if (MissionManager.Instance == null || MissionManager.Instance.IsMissionActive) return;

        if (!MissionManager.Instance.IsSpawned)
        {
            Debug.LogWarning("[MissionLaunchZone] MissionManager not spawned yet");
            return;
        }

        var clients = NetworkManager.Singleton.ConnectedClients;
        if (clients == null || clients.Count == 0) return;

        foreach (var kvp in clients)
        {
            var playerObj = kvp.Value.PlayerObject;
            if (playerObj == null) return;

            if (!playersInZone.Contains(kvp.Key)) return;

            var role = playerObj.GetComponent<NetworkPlayerRole>();
            if (role == null || role.CurrentRole == PlayerRole.None) return;
        }

        MissionManager.Instance.StartMissionServerRpc();
    }
}
