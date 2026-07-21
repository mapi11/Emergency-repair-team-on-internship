using Unity.Netcode;
using UnityEngine;

public class LobbyItemSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private float spacing = 2f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        if (cubePrefab == null)
        {
            Debug.LogError("[LobbyItemSpawner] cubePrefab not assigned.");
            return;
        }

        int count = NetworkConnectionManager.Instance != null
            ? NetworkConnectionManager.Instance.MaxPlayers
            : 1;

        float startX = -(count - 1) * spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = transform.position + new Vector3(startX + i * spacing, 0, 0);
            GameObject go = Instantiate(cubePrefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();
        }
    }
}
