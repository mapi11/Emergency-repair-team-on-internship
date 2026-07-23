using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(NetworkObject))]
public class PreStartLobbyController : NetworkBehaviour
{
    [System.Serializable]
    public class DoorData
    {
        public Transform door;
        public Vector3 openDirection = Vector3.right;
    }

    [Header("Doors: Entrance")]
    [SerializeField] private DoorData[] entranceDoors;
    [SerializeField] private float entranceDoorOffset = 1f;
    [SerializeField] private float doorSpeed = 2f;

    [Header("Doors: Exit")]
    [SerializeField] private DoorData[] exitDoors;
    [SerializeField] private float exitDoorOffset = 1f;

    [Header("Trigger Zone")]
    [SerializeField] private BoxCollider zoneTrigger;

    [Header("Default Spawns")]
    [SerializeField] private GameObject defaultSpawnRoot;

    [Header("Reconnect Spawns")]
    [SerializeField] private Transform[] reconnectSpawnPoints;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Timer")]
    [SerializeField] private float countdownDuration = 10f;

    private Vector3[] entranceClosedPos;
    private Vector3[] exitClosedPos;
    private readonly HashSet<ulong> playersInside = new();
    private int totalPlayerCount;
    private Coroutine countdownCoroutine;
    private float overlapCheckTimer;

    private readonly NetworkVariable<int> networkPlayersInside = new();
    private readonly NetworkVariable<int> networkTotalPlayers = new();
    private readonly NetworkVariable<int> networkCountdown = new();

    private bool isLockedIn;
    public bool IsLockedIn => isLockedIn;
    public readonly NetworkVariable<bool> NetworkSpawnsDisabled = new();

    public bool AllPlayersInZone =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.ConnectedClientsIds.Count > 0 &&
        playersInside.Count >= NetworkManager.Singleton.ConnectedClientsIds.Count;

    public bool IsCountdownActive => countdownCoroutine != null;

    public override void OnNetworkSpawn()
    {
        StoreClosedPositions();

        if (IsServer)
        {
            if (NetworkConnectionManager.ConnectionLocked || NetworkConnectionManager.IsLobbyLocked)
            {
                NetworkSpawnsDisabled.Value = true;
                isLockedIn = true;
            }

            StartCoroutine(AnimateDoors(entranceDoors, entranceClosedPos, entranceDoorOffset, true, doorSpeed));

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnected;

            RefreshCounts();
        }

        networkPlayersInside.OnValueChanged += (_, _) => UpdateUI();
        networkTotalPlayers.OnValueChanged += (_, _) => UpdateUI();
        networkCountdown.OnValueChanged += OnCountdownChanged;

        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerDisconnected;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        overlapCheckTimer -= Time.deltaTime;
        if (overlapCheckTimer > 0) return;
        overlapCheckTimer = 0.1f;

        CheckOverlap();
    }

    private void CheckOverlap()
    {
        if (zoneTrigger == null) return;

        var currentInside = new HashSet<ulong>();
        Collider[] hits = Physics.OverlapBox(
            zoneTrigger.bounds.center,
            zoneTrigger.bounds.extents,
            zoneTrigger.transform.rotation);

        foreach (var hit in hits)
        {
            var netObj = hit.GetComponentInParent<NetworkObject>();
            if (netObj == null) continue;
            if (hit.GetComponentInParent<PlayerController>() == null) continue;

            currentInside.Add(netObj.OwnerClientId);
        }

        bool changed = false;

        foreach (var id in currentInside)
        {
            if (playersInside.Add(id))
            {
                changed = true;
                CheckCountdown();
            }
        }

        var toRemove = new List<ulong>();
        foreach (var id in playersInside)
        {
            if (!currentInside.Contains(id))
                toRemove.Add(id);
        }

        foreach (var id in toRemove)
        {
            playersInside.Remove(id);
            changed = true;
            CancelCountdown();
        }

        if (changed)
            RefreshCounts();
    }

    private void StoreClosedPositions()
    {
        if (entranceDoors != null)
        {
            entranceClosedPos = new Vector3[entranceDoors.Length];
            for (int i = 0; i < entranceDoors.Length; i++)
                if (entranceDoors[i].door != null)
                    entranceClosedPos[i] = entranceDoors[i].door.localPosition;
        }

        if (exitDoors != null)
        {
            exitClosedPos = new Vector3[exitDoors.Length];
            for (int i = 0; i < exitDoors.Length; i++)
                if (exitDoors[i].door != null)
                    exitClosedPos[i] = exitDoors[i].door.localPosition;
        }
    }

    private IEnumerator AnimateDoors(DoorData[] doors, Vector3[] closedPos, float offset, bool opening, float speed)
    {
        if (doors == null) yield break;

        float maxDist = 0f;
        var starts = new Vector3[doors.Length];
        var targets = new Vector3[doors.Length];

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].door == null) continue;
            starts[i] = doors[i].door.localPosition;
            targets[i] = opening
                ? closedPos[i] + doors[i].openDirection.normalized * offset
                : closedPos[i];

            float d = Vector3.Distance(starts[i], targets[i]);
            if (d > maxDist) maxDist = d;
        }

        if (maxDist < 0.001f) yield break;

        float dur = maxDist / Mathf.Max(speed, 0.01f);
        float el = 0;

        while (el < dur)
        {
            float t = el / dur;
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].door == null) continue;
                doors[i].door.localPosition = Vector3.Lerp(starts[i], targets[i], t);
            }
            el += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].door != null)
                doors[i].door.localPosition = targets[i];
        }
    }

    private void UpdateUI()
    {
        int inside = networkPlayersInside.Value;
        int total = networkTotalPlayers.Value;

        if (playerCountText != null)
            playerCountText.text = $"{inside}/{total}";
    }

    private void OnCountdownChanged(int oldValue, int newValue)
    {
        if (countdownText != null)
            countdownText.text = newValue > 0 ? $"{newValue} sec." : "";
    }

    private void RefreshCounts()
    {
        networkPlayersInside.Value = playersInside.Count;
        networkTotalPlayers.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
    }

    private void CheckCountdown()
    {
        if (isLockedIn) return;
        if (countdownCoroutine != null) return;

        int total = networkTotalPlayers.Value;
        if (total > 0 && playersInside.Count >= total)
            countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    private void CancelCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        networkCountdown.Value = 0;
    }

    private IEnumerator CountdownRoutine()
    {
        StartCoroutine(AnimateDoors(exitDoors, exitClosedPos, exitDoorOffset, false, doorSpeed));

        int remaining = Mathf.CeilToInt(countdownDuration);

        while (remaining > 0)
        {
            networkCountdown.Value = remaining;

            int total = networkTotalPlayers.Value;
            if (total <= 0 || playersInside.Count < total)
            {
                CancelCountdown();
                yield break;
            }

            yield return new WaitForSeconds(1f);
            remaining--;
        }

        networkCountdown.Value = 0;
        countdownCoroutine = null;

        OnTimerFinished();
    }

    private void OnTimerFinished()
    {
        NetworkConnectionManager.ConnectionLocked = true;
        NetworkConnectionManager.IsLobbyLocked = true;
        NetworkConnectionManager.SnapshotPreMissionProfiles();
        isLockedIn = true;
        NetworkSpawnsDisabled.Value = true;

        if (defaultSpawnRoot != null)
            defaultSpawnRoot.SetActive(false);
        totalPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        NetworkConnectionManager.FixedPlayerCount = totalPlayerCount;

        SyncPlayerCountClientRpc(totalPlayerCount);
        ApplyRoleItemLimits();
        ActivateInventoryClientRpc(totalPlayerCount);

        StartCoroutine(AnimateDoors(entranceDoors, entranceClosedPos, entranceDoorOffset, false, doorSpeed));
        StartCoroutine(AnimateDoors(exitDoors, exitClosedPos, exitDoorOffset, true, doorSpeed));
    }

    private void ApplyRoleItemLimits()
    {
        var clients = NetworkManager.Singleton.ConnectedClients.Values
            .OrderBy(c => c.ClientId)
            .ToList();

        int total = clients.Count;

        for (int i = 0; i < clients.Count; i++)
        {
            var player = clients[i].PlayerObject;
            if (player == null) continue;

            var sync = player.GetComponent<NetworkInventorySync>();
            if (sync == null) continue;

            int max;

            if (total == 1)
            {
                max = 3;
            }
            else if (total == 2)
            {
                max = 2;
            }
            else
            {
                max = 1;
            }

            sync.SetMaxRoleItems(max);
        }
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        playersInside.Remove(clientId);
        RefreshCounts();
        CancelCountdown();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (isLockedIn)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            SyncPlayerCountClientRpc(totalPlayerCount, rpcParams);
            ActivateInventoryClientRpc(totalPlayerCount, rpcParams);

            NetworkConnectionManager.RestorePlayerRoleItems(clientId);

            var reconnected = NetworkManager.Singleton.ConnectedClients[clientId]?.PlayerObject;
            if (reconnected != null)
            {
                var sync = reconnected.GetComponent<NetworkInventorySync>();
                if (sync != null)
                {
                    int max = totalPlayerCount switch
                    {
                        1 => 3,
                        2 => 2,
                        _ => 1
                    };
                    sync.SetMaxRoleItems(max);
                }
            }
            return;
        }
        RefreshCounts();
        CheckCountdown();
    }

    [ClientRpc]
    private void SyncPlayerCountClientRpc(int count, ClientRpcParams clientRpcParams = default)
    {
        NetworkConnectionManager.FixedPlayerCount = count;
    }

    [ClientRpc]
    private void ActivateInventoryClientRpc(int totalPlayers, ClientRpcParams clientRpcParams = default)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient) return;

        var localObj = net.LocalClient?.PlayerObject;
        if (localObj == null) return;

        int slotCount = totalPlayers switch
        {
            1 => 5,
            2 => 4,
            _ => 3
        };

        var inventory = localObj.GetComponent<Inventory>();
        if (inventory != null)
            inventory.ResizeTo(slotCount);

        var invUI = FindFirstObjectByType<InventoryUI>();
        if (invUI != null)
            invUI.Initialize(inventory);
    }

    public Transform GetReconnectSpawn(ulong clientId)
    {
        if (reconnectSpawnPoints == null || reconnectSpawnPoints.Length == 0)
            return null;

        int index = (int)(clientId % (ulong)reconnectSpawnPoints.Length);
        Transform spawn = reconnectSpawnPoints[index];
        return spawn != null && spawn.gameObject.activeInHierarchy ? spawn : null;
    }

    public bool IsReconnectSpawn(Transform t)
    {
        if (reconnectSpawnPoints == null) return false;
        for (int i = 0; i < reconnectSpawnPoints.Length; i++)
            if (reconnectSpawnPoints[i] == t) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        DrawDoorGizmos(entranceDoors, entranceDoorOffset, Color.green);
        DrawDoorGizmos(exitDoors, exitDoorOffset, Color.blue);
    }

    private void DrawDoorGizmos(DoorData[] doors, float offset, Color color)
    {
        if (doors == null) return;

        Gizmos.color = color;
        foreach (var d in doors)
        {
            if (d.door == null) continue;
            Vector3 from = d.door.position;
            Vector3 to = from + d.door.TransformDirection(d.openDirection.normalized * offset);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(to, 0.08f);
        }
    }
}
