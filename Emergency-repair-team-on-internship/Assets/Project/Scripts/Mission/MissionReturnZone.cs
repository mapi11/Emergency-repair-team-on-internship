using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MissionReturnZone : NetworkBehaviour
{
    private readonly HashSet<ulong> playersInZone = new();

    private void OnTriggerEnter(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        if (networkObject.GetComponent<PlayerController>() == null) return;

        playersInZone.Add(networkObject.OwnerClientId);

        if (IsServer)
            CheckReturnConditions();
    }

    private void OnTriggerExit(Collider other)
    {
        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject == null) return;

        if (networkObject.GetComponent<PlayerController>() == null) return;

        playersInZone.Remove(networkObject.OwnerClientId);
    }

    private void CheckReturnConditions()
    {
        if (MissionManager.Instance == null || !MissionManager.Instance.IsMissionActive) return;

        var clients = NetworkManager.Singleton.ConnectedClients;
        if (clients == null || clients.Count == 0) return;

        foreach (var kvp in clients)
        {
            var playerObj = kvp.Value.PlayerObject;
            if (playerObj == null) return;

            if (!playersInZone.Contains(kvp.Key)) return;
        }

        MissionManager.Instance.ReturnToLobbyServerRpc();
    }
}
