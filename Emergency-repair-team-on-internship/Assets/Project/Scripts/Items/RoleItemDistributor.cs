using Unity.Netcode;
using UnityEngine;

public class RoleItemDistributor : NetworkBehaviour
{
    [SerializeField] private GameObject instructionBookPrefab;
    [SerializeField] private GameObject toolPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private static readonly (int bookCount, int toolCount)[] Distribution =
    {
        (1, 2), // 1 player: P1 = 1 Book + 2 Tools
        (1, 1), // 2 players: P1 = 1 Book + 1 Tool, P2 = 2 Tools
        (1, 1), // 3 players: P1 = 1 Book + 1 Tool
        (1, 0), // 4 players: P1 = 1 Book
    };

    public override void OnNetworkSpawn()
    {
        if (!IsServer) enabled = false;
    }

    public void Distribute()
    {
        if (!IsServer) return;

        int total = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (total < 1 || total > 4) return;
        if (instructionBookPrefab == null || toolPrefab == null) return;

        int index = total - 1;

        for (int p = 0; p < total; p++)
        {
            if (p >= spawnPoints.Length || spawnPoints[p] == null) continue;

            int book = 0, tools = 0;

            if (p == 0)
            {
                (book, tools) = Distribution[index];
            }
            else if (total == 2)
            {
                tools = 2;
            }
            else
            {
                tools = 1;
            }

            for (int b = 0; b < book; b++)
                SpawnItem(instructionBookPrefab, spawnPoints[p]);

            for (int t = 0; t < tools; t++)
                SpawnItem(toolPrefab, spawnPoints[p]);
        }
    }

    private void SpawnItem(GameObject prefab, Transform point)
    {
        Vector3 pos = point.position + Random.insideUnitSphere * 0.3f;
        pos.y = point.position.y;

        var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        go.GetComponent<NetworkObject>().Spawn();
    }
}
