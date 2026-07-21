using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(NetworkObject))]
public class StartLobbyController : NetworkBehaviour
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

    [Header("Doors: Exit")]
    [SerializeField] private DoorData[] exitDoors;
    [SerializeField] private float exitDoorOffset = 1f;

    [Header("Settings")]
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private float countdownDuration = 10f;

    [Header("Trigger Zone")]
    [SerializeField] private BoxCollider zoneTrigger;

    [Header("Reconnect Spawns")]
    [SerializeField] private Transform[] reconnectSpawnPoints;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Cleanup")]
    [SerializeField] private GameObject[] objectsToDisableOnStart;

    private Vector3[] entranceClosedPos;
    private Vector3[] exitClosedPos;
    private readonly HashSet<ulong> playersInZone = new();
    private Coroutine countdownCoroutine;
    private float overlapCheckTimer;

    private readonly NetworkVariable<int> networkPlayersInZone = new();
    private readonly NetworkVariable<int> networkTotalPlayers = new();
    private readonly NetworkVariable<int> networkCountdown = new();
    public readonly NetworkVariable<bool> NetworkMissionActive = new();

    public bool IsMissionActive => NetworkMissionActive.Value;

    public override void OnNetworkSpawn()
    {
        StoreClosedPositions();

        if (IsServer)
        {
            StartCoroutine(AnimateDoors(entranceDoors, entranceClosedPos, entranceDoorOffset, true, doorSpeed));
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            RefreshCounts();
        }

        networkPlayersInZone.OnValueChanged += (_, _) => UpdateUI();
        networkTotalPlayers.OnValueChanged += (_, _) => UpdateUI();
        networkCountdown.OnValueChanged += OnCountdownChanged;

        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkMissionActive.Value) return;
        LockPlayerRoleSlots(clientId);
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
        if (zoneTrigger == null || NetworkMissionActive.Value) return;

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
            if (playersInZone.Add(id))
            {
                changed = true;
                LockPlayerRoleSlots(id);
                CheckStartConditions();
            }
        }

        var toRemove = new List<ulong>();
        foreach (var id in playersInZone)
        {
            if (!currentInside.Contains(id))
                toRemove.Add(id);
        }

        foreach (var id in toRemove)
        {
            playersInZone.Remove(id);
            changed = true;

            if (!NetworkMissionActive.Value)
                UnlockPlayerRoleSlots(id);

            CancelCountdown();
        }

        if (changed)
            RefreshCounts();
    }

    private void LockPlayerRoleSlots(ulong clientId)
    {
        if (MissionManager.Instance != null && MissionManager.Instance.IsSpawned)
            MissionManager.Instance.LockRoleSlotsForClientClientRpc(clientId);
    }

    private void UnlockPlayerRoleSlots(ulong clientId)
    {
        if (MissionManager.Instance != null && MissionManager.Instance.IsSpawned)
            MissionManager.Instance.UnlockRoleSlotsForClientClientRpc(clientId);
    }

    private bool HasAtLeastOnePrimaryRole()
    {
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            var player = kvp.Value.PlayerObject;
            if (player == null) continue;

            var sync = player.GetComponent<NetworkInventorySync>();
            if (sync != null && sync.NetworkHasPrimaryRole)
                return true;
        }
        return false;
    }

    private void CheckStartConditions()
    {
        if (countdownCoroutine != null) return;
        if (NetworkMissionActive.Value) return;

        int total = networkTotalPlayers.Value;
        bool allInside = total > 0 && playersInZone.Count >= total;
        bool hasPrimary = HasAtLeastOnePrimaryRole();

        if (allInside && hasPrimary)
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
            if (total <= 0 || playersInZone.Count < total || !HasAtLeastOnePrimaryRole())
            {
                CancelCountdown();
                yield break;
            }

            yield return new WaitForSeconds(1f);
            remaining--;
        }

        networkCountdown.Value = 0;
        countdownCoroutine = null;

        StartMission();
    }

    private void StartMission()
    {
        NetworkMissionActive.Value = true;
        DisableObjectsOnStartClientRpc();

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            LockPlayerRoleSlots(kvp.Key);

        StartCoroutine(AnimateDoors(entranceDoors, entranceClosedPos, entranceDoorOffset, false, doorSpeed));
        StartCoroutine(AnimateDoors(exitDoors, exitClosedPos, exitDoorOffset, true, doorSpeed));
    }

    [ClientRpc]
    private void DisableObjectsOnStartClientRpc()
    {
        if (objectsToDisableOnStart == null) return;
        foreach (var obj in objectsToDisableOnStart)
        {
            if (obj != null)
                obj.SetActive(false);
        }
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
        if (playerCountText != null)
            playerCountText.text = $"{networkPlayersInZone.Value}/{networkTotalPlayers.Value}";
    }

    private void OnCountdownChanged(int oldValue, int newValue)
    {
        if (countdownText != null)
            countdownText.text = newValue > 0 ? $"{newValue} sec." : "";
    }

    private void RefreshCounts()
    {
        networkPlayersInZone.Value = playersInZone.Count;
        networkTotalPlayers.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
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
