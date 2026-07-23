using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
public class ElevatorController : NetworkBehaviour
{
    [System.Serializable]
    public class DoorData
    {
        public Transform door;
        public Vector3 closeDirection = Vector3.right;
    }

    [System.Serializable]
    public class ElevatorFloor
    {
        public Transform position;
        public ButtonPress callButton;
        public ButtonPress insideButton;
    }

    [Header("Floors")]
    [SerializeField] private ElevatorFloor[] floors;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Doors")]
    [SerializeField] private DoorData[] doors;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private float doorOffset = 1f;

    [Header("Trigger")]
    [SerializeField] private BoxCollider elevatorTrigger;

    [Header("Player Count UI")]
    [SerializeField] private TextMeshProUGUI playerCountText;

    private Vector3[] doorOpenPositions;
    private readonly HashSet<ulong> playersInside = new();
    private int currentFloorIndex;

    private readonly NetworkVariable<State> currentState = new(State.Idle);
    private readonly NetworkVariable<int> networkPlayersInside = new();
    private readonly NetworkVariable<int> networkTotalPlayers = new();

    private enum State : byte { Idle, Closing, Moving, Opening }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (doors != null)
            {
                doorOpenPositions = new Vector3[doors.Length];
                for (int i = 0; i < doors.Length; i++)
                    if (doors[i].door != null)
                        doorOpenPositions[i] = doors[i].door.localPosition;
            }

            if (floors != null)
            {
                for (int i = 0; i < floors.Length; i++)
                {
                    int index = i;
                    if (floors[i].callButton != null)
                        floors[i].callButton.onPressed.AddListener(() => OnCallButtonPressed(index));
                    if (floors[i].insideButton != null)
                        floors[i].insideButton.onPressed.AddListener(() => OnInsideFloorButtonPressed(index));
                }
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            RefreshCounts();
        }

        currentState.OnValueChanged += OnStateChanged;
        networkPlayersInside.OnValueChanged += OnPlayerCountChanged;
        networkTotalPlayers.OnValueChanged += OnTotalCountChanged;

        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (floors != null)
            {
                for (int i = 0; i < floors.Length; i++)
                {
                    if (floors[i].callButton != null)
                        floors[i].callButton.onPressed.RemoveAllListeners();
                    if (floors[i].insideButton != null)
                        floors[i].insideButton.onPressed.RemoveAllListeners();
                }
            }

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        currentState.OnValueChanged -= OnStateChanged;
        networkPlayersInside.OnValueChanged -= OnPlayerCountChanged;
        networkTotalPlayers.OnValueChanged -= OnTotalCountChanged;
    }

    private void OnStateChanged(State oldValue, State newValue)
    {
        UpdateUI();
    }

    private void OnPlayerCountChanged(int oldValue, int newValue)
    {
        UpdateUI();
    }

    private void OnTotalCountChanged(int oldValue, int newValue)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        int inside = networkPlayersInside.Value;
        int total = networkTotalPlayers.Value;

        if (playerCountText != null)
            playerCountText.text = $"{inside}/{total}";

        bool allInside = total > 0 && inside >= total;
        bool buttonsActive = allInside && currentState.Value == State.Idle;

        if (floors != null)
        {
            foreach (var f in floors)
            {
                if (f.insideButton != null)
                    f.insideButton.SetCanInteract(buttonsActive);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        playersInside.Add(netObj.OwnerClientId);
        RefreshCounts();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        playersInside.Remove(netObj.OwnerClientId);
        RefreshCounts();
    }

    private void RefreshCounts()
    {
        networkPlayersInside.Value = playersInside.Count;
        networkTotalPlayers.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
    }

    private void OnCallButtonPressed(int floorIndex)
    {
        if (!IsServer) return;
        if (currentState.Value != State.Idle) return;
        if (!IsValidFloor(floorIndex)) return;

        Vector3 from = transform.position;
        Vector3 to = floors[floorIndex].position.position;
        if (Vector3.Distance(from, to) < 0.01f) return;

        StartCoroutine(MoveToFloorRoutine(from, to, floorIndex));
    }

    private void OnInsideFloorButtonPressed(int floorIndex)
    {
        if (!IsServer) return;
        if (currentState.Value != State.Idle) return;
        if (!IsValidFloor(floorIndex)) return;
        if (floorIndex == currentFloorIndex) return;

        int total = networkTotalPlayers.Value;
        if (total <= 0 || playersInside.Count < total) return;

        Vector3 from = transform.position;
        Vector3 to = floors[floorIndex].position.position;

        StartCoroutine(TransportRoutine(from, to, floorIndex));
    }

    private bool IsValidFloor(int index)
    {
        return index >= 0 && floors != null && index < floors.Length
            && floors[index] != null && floors[index].position != null;
    }

    private IEnumerator MoveToFloorRoutine(Vector3 from, Vector3 to, int floorIndex)
    {
        currentState.Value = State.Closing;
        yield return StartCoroutine(MoveDoorsCoroutine(true, doorSpeed));

        currentState.Value = State.Moving;
        yield return StartCoroutine(MoveCoroutine(from, to));

        currentFloorIndex = floorIndex;

        currentState.Value = State.Opening;
        yield return StartCoroutine(MoveDoorsCoroutine(false, doorSpeed));

        currentState.Value = State.Idle;
    }

    private IEnumerator TransportRoutine(Vector3 from, Vector3 to, int floorIndex)
    {
        currentState.Value = State.Closing;
        yield return StartCoroutine(MoveDoorsCoroutine(true, doorSpeed));

        ParentPlayers(true);

        currentState.Value = State.Moving;
        yield return StartCoroutine(MoveCoroutine(from, to));

        currentFloorIndex = floorIndex;

        currentState.Value = State.Opening;
        yield return StartCoroutine(MoveDoorsCoroutine(false, doorSpeed));

        ParentPlayers(false);

        currentState.Value = State.Idle;
    }

    private IEnumerator MoveCoroutine(Vector3 from, Vector3 to)
    {
        float duration = Vector3.Distance(from, to) / Mathf.Max(moveSpeed, 0.01f);
        float elapsed = 0f;
        Vector3 prevPos = transform.position;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 newPos = Vector3.Lerp(from, to, t);
            Vector3 delta = newPos - prevPos;

            transform.position = newPos;
            MovePlayersBy(delta);

            prevPos = newPos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalDelta = to - transform.position;
        transform.position = to;
        MovePlayersBy(finalDelta);
    }

    private void MovePlayersBy(Vector3 delta)
    {
        foreach (ulong clientId in playersInside)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                continue;

            var netObj = client.PlayerObject;
            if (netObj == null) continue;

            netObj.transform.position += delta;
        }
    }

    private IEnumerator MoveDoorsCoroutine(bool closing, float speed)
    {
        if (doors == null || doors.Length == 0 || doorOpenPositions == null)
            yield break;

        float maxDistance = 0f;
        var startPositions = new Vector3[doors.Length];
        var targetPositions = new Vector3[doors.Length];

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].door == null) continue;

            startPositions[i] = doors[i].door.localPosition;
            targetPositions[i] = closing
                ? doorOpenPositions[i] + doors[i].closeDirection.normalized * doorOffset
                : doorOpenPositions[i];

            float dist = Vector3.Distance(startPositions[i], targetPositions[i]);
            if (dist > maxDistance) maxDistance = dist;
        }

        if (maxDistance < 0.001f) yield break;

        float duration = maxDistance / Mathf.Max(speed, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].door == null) continue;
                doors[i].door.localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].door != null)
                doors[i].door.localPosition = targetPositions[i];
        }
    }

    private void ParentPlayers(bool parent)
    {
        foreach (ulong clientId in playersInside)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                continue;

            var netObj = client.PlayerObject;
            if (netObj == null) continue;

            if (parent)
                netObj.TrySetParent(transform);
            else
                netObj.TrySetParent((Transform)null);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        RefreshCounts();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        playersInside.Remove(clientId);
        RefreshCounts();
    }

    private void OnDrawGizmosSelected()
    {
        if (floors != null)
        {
            for (int i = 0; i < floors.Length; i++)
            {
                var f = floors[i];
                if (f.position == null) continue;

                Gizmos.color = i == currentFloorIndex ? Color.cyan : Color.yellow;
                Gizmos.DrawLine(transform.position, f.position.position);

                Vector3 mid = Vector3.Lerp(transform.position, f.position.position, 0.5f);
                Vector3 dir = (f.position.position - transform.position).normalized;

                Vector3 right = Vector3.Cross(dir, Vector3.up);
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.Cross(dir, Vector3.forward);
                right = right.normalized * 0.5f;

                Gizmos.DrawRay(mid - dir * 0.5f + right, dir - right);
                Gizmos.DrawRay(mid - dir * 0.5f - right, dir + right);
            }
        }

        if (doors != null)
        {
            Gizmos.color = Color.green * 0.8f;
            foreach (var d in doors)
            {
                if (d.door == null) continue;
                Vector3 from = d.door.position;
                Vector3 to = from + d.door.TransformDirection(d.closeDirection.normalized * doorOffset);
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(to, 0.08f);
            }
        }
    }

    private void Start()
    {
        if (elevatorTrigger == null)
            elevatorTrigger = GetComponent<BoxCollider>();

        if (elevatorTrigger != null)
            elevatorTrigger.isTrigger = true;
    }

    private void Reset()
    {
        elevatorTrigger = GetComponent<BoxCollider>();
        if (elevatorTrigger != null)
            elevatorTrigger.isTrigger = true;
    }
}
