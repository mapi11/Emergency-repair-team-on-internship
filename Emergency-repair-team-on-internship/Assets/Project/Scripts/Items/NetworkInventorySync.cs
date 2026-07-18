using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkInventorySync : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform leftHoldPivot;
    [SerializeField] private Transform rightHoldPivot;
    [SerializeField] private Transform dropOrigin;

    [Header("Item Prefabs")]
    [SerializeField] private ItemEntry[] items;
    [SerializeField] private float heldItemScale = 0.8f;

    [System.Serializable]
    private class ItemEntry
    {
        public string Name;
        public GameObject WorldDropPrefab;
    }

    [Header("Throw")]
    [SerializeField] private Transform leftEjectPoint;
    [SerializeField] private Transform rightEjectPoint;
    [SerializeField] private float minThrowForce = 3f;
    [SerializeField] private float maxThrowForce = 15f;
    [SerializeField] private float throwBlockCheckRadius = 0.15f;
    [SerializeField] private LayerMask throwBlockMask = ~0;

    private readonly NetworkVariable<int> networkActiveSlot = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString32Bytes> networkActiveItemName = new(
        new FixedString32Bytes(""),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<byte> networkActiveHand = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<ulong> networkActiveWorldId = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameObject currentHeldVisual;
    private readonly Dictionary<int, ulong> serverSlotWorldIds = new();

    private static readonly Dictionary<ulong, TrackedPlayer> allTrackedItems = new();
    private static bool disconnectHookRegistered;

    private class TrackedPlayer
    {
        public List<string> Items = new();
        public Vector3 LastPosition;
        public NetworkInventorySync SyncInstance;
    }

    public static void ServerTrackItem(ulong clientId, string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
            return;

        if (!allTrackedItems.TryGetValue(clientId, out var entry))
        {
            entry = new TrackedPlayer();
            allTrackedItems[clientId] = entry;
        }

        if (!entry.Items.Contains(itemName))
            entry.Items.Add(itemName);
    }

    public static void ServerUntrackItem(ulong clientId, string itemName)
    {
        if (allTrackedItems.TryGetValue(clientId, out var entry))
        {
            entry.Items.Remove(itemName);
        }
    }

    private static void OnDisconnectStatic(ulong clientId)
    {
        if (!allTrackedItems.TryGetValue(clientId, out var entry))
            return;

        allTrackedItems.Remove(clientId);

        if (entry.Items.Count == 0)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        var sync = entry.SyncInstance;

        foreach (string itemName in entry.Items)
        {
            if (string.IsNullOrEmpty(itemName))
                continue;

            int idx = sync != null ? sync.GetPrefabIndex(itemName) : -1;
            Quaternion rotation = Random.rotationUniform;
            Vector3 spawnPos = entry.LastPosition + Vector3.up * 0.3f;

            GameObject dropObj;

            if (idx >= 0 && sync != null && idx < sync.items.Length && sync.items[idx].WorldDropPrefab != null)
            {
                dropObj = Object.Instantiate(sync.items[idx].WorldDropPrefab, spawnPos, rotation);
            }
            else
            {
                dropObj = BuildDropItem(spawnPos, rotation, null, itemName);
            }

            NetworkObject netObj = dropObj.GetComponent<NetworkObject>();

            if (netObj == null)
                netObj = dropObj.AddComponent<NetworkObject>();

            if (!netObj.IsSpawned)
                netObj.Spawn(true);
        }
    }
    private GameObject cachedWorldVisualPrefab;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (inventory != null)
            inventory.OnActiveSlotChanged += OnLocalActiveSlotChanged;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (allTrackedItems.TryGetValue(OwnerClientId, out var entry) && entry.Items.Count > 0)
            entry.LastPosition = transform.position;
    }

    private void OnValidate()
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
                continue;

            if (items[i].WorldDropPrefab == null)
                continue;

            if (string.IsNullOrEmpty(items[i].Name))
            {
                var pickable = items[i].WorldDropPrefab.GetComponent<PickableItem>();
                items[i].Name = pickable != null ? pickable.ItemName : items[i].WorldDropPrefab.name;
            }
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnActiveSlotChanged -= OnLocalActiveSlotChanged;
    }

    public override void OnNetworkSpawn()
    {
        networkActiveSlot.OnValueChanged += OnActiveSlotChanged;
        networkActiveItemName.OnValueChanged += OnActiveItemNameChanged;
        networkActiveHand.OnValueChanged += OnActiveHandChanged;
        networkActiveWorldId.OnValueChanged += OnActiveWorldIdChanged;

        UpdateHeldVisual(
            networkActiveSlot.Value,
            networkActiveItemName.Value.ToString(),
            networkActiveHand.Value
        );

        if (cachedWorldVisualPrefab == null &&
            networkActiveWorldId.Value != 0 &&
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(networkActiveWorldId.Value))
        {
            Invoke(nameof(RetryBuildCachedVisual), 0.1f);
        }

        if (IsServer)
        {
            if (!disconnectHookRegistered)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectStatic;
                disconnectHookRegistered = true;
            }

            if (allTrackedItems.TryGetValue(OwnerClientId, out var entry))
            {
                entry.SyncInstance = this;
                entry.LastPosition = transform.position;
            }
        }
    }

    private void RetryBuildCachedVisual()
    {
        ulong worldId = networkActiveWorldId.Value;

        if (worldId == 0)
            return;

        if (cachedWorldVisualPrefab != null)
            return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(worldId, out var netObj))
            return;

        cachedWorldVisualPrefab = BuildHeldVisualFromNetworkObject(netObj);

        if (cachedWorldVisualPrefab == null)
            return;

        UpdateHeldVisual(
            networkActiveSlot.Value,
            networkActiveItemName.Value.ToString(),
            networkActiveHand.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        networkActiveSlot.OnValueChanged -= OnActiveSlotChanged;
        networkActiveItemName.OnValueChanged -= OnActiveItemNameChanged;
        networkActiveHand.OnValueChanged -= OnActiveHandChanged;
        networkActiveWorldId.OnValueChanged -= OnActiveWorldIdChanged;
    }

    public void ServerSetSlotWorldId(int slot, ulong worldId)
    {
        if (!IsServer)
            return;

        serverSlotWorldIds[slot] = worldId;

        if (networkActiveSlot.Value == slot)
            networkActiveWorldId.Value = worldId;
    }

    public void ServerTrackItem(string itemName)
    {
        if (!IsServer || string.IsNullOrEmpty(itemName))
            return;

        ServerTrackItem(OwnerClientId, itemName);

        if (allTrackedItems.TryGetValue(OwnerClientId, out var entry))
        {
            entry.SyncInstance = this;
            entry.LastPosition = transform.position;
        }
    }

    public void ServerUntrackItem(string itemName)
    {
        if (!IsServer)
            return;

        ServerUntrackItem(OwnerClientId, itemName);
    }

    private void OnActiveWorldIdChanged(ulong oldId, ulong newId)
    {
        if (cachedWorldVisualPrefab != null)
        {
            Destroy(cachedWorldVisualPrefab);
            cachedWorldVisualPrefab = null;
        }

        if (newId != 0 &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newId, out var netObj))
        {
            cachedWorldVisualPrefab = BuildHeldVisualFromNetworkObject(netObj);
        }

        UpdateHeldVisual(
            networkActiveSlot.Value,
            networkActiveItemName.Value.ToString(),
            networkActiveHand.Value
        );
    }

    private GameObject BuildHeldVisualFromNetworkObject(NetworkObject netObj)
    {
        var meshFilters = netObj.GetComponentsInChildren<MeshFilter>();

        if (meshFilters.Length == 0)
            return null;

        GameObject visual = new GameObject("NetHeldVisual");

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null)
                continue;

            GameObject child = new GameObject(mf.name);
            child.transform.SetParent(visual.transform, false);
            child.transform.localPosition = netObj.transform.InverseTransformPoint(mf.transform.position);
            child.transform.localRotation = Quaternion.Inverse(netObj.transform.rotation) * mf.transform.rotation;

            Vector3 parentScale = netObj.transform.lossyScale;
            Vector3 childScale = mf.transform.lossyScale;

            child.transform.localScale = new Vector3(
                parentScale.x > 0.0001f ? childScale.x / parentScale.x : 1f,
                parentScale.y > 0.0001f ? childScale.y / parentScale.y : 1f,
                parentScale.z > 0.0001f ? childScale.z / parentScale.z : 1f
            );

            child.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;

            var sourceRenderer = mf.GetComponent<MeshRenderer>();
            var targetRenderer = child.AddComponent<MeshRenderer>();

            if (sourceRenderer != null)
                targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

            targetRenderer.enabled = false;
        }

        return visual;
    }

    private void OnLocalActiveSlotChanged(int slot)
    {
        if (inventory == null)
            return;

        if (IsSpawned && networkActiveSlot.Value == slot)
            return;

        byte handIndex = playerController != null
            ? (byte)(playerController.SelectedInteractionHand == PlayerController.InteractionHand.Right ? 0 : 1)
            : (byte)0;

        string itemName = slot >= 0 ? inventory.GetItemAtSlot(slot) : null;

        UpdateHeldVisual(slot, itemName, handIndex);

        if (!IsSpawned)
            return;

        UpdateActiveSlotServerRpc(slot, new FixedString32Bytes(itemName ?? ""), handIndex);

        if (IsServer)
        {
            ulong worldId = slot >= 0 && serverSlotWorldIds.TryGetValue(slot, out ulong id) ? id : 0;
            networkActiveWorldId.Value = worldId;
        }
        else
        {
            SyncWorldIdServerRpc(slot);
        }
    }

    [ServerRpc]
    private void SyncWorldIdServerRpc(int slot)
    {
        ulong worldId = slot >= 0 && serverSlotWorldIds.TryGetValue(slot, out ulong id) ? id : 0;
        networkActiveWorldId.Value = worldId;
    }

    public void SyncFullState(byte handIndex = 0)
    {
        if (inventory == null)
            return;

        int slot = inventory.ActiveSlot;
        string itemName = slot >= 0 ? inventory.GetItemAtSlot(slot) : null;

        networkActiveSlot.Value = slot;
        networkActiveItemName.Value = new FixedString32Bytes(itemName ?? "");
        networkActiveHand.Value = handIndex;

        if (IsServer)
        {
            ulong worldId = slot >= 0 && serverSlotWorldIds.TryGetValue(slot, out ulong id) ? id : 0;
            networkActiveWorldId.Value = worldId;
        }
    }

    private Vector3 GetLaunchPosition()
    {
        bool useLeft = playerController != null &&
                       playerController.SelectedInteractionHand == PlayerController.InteractionHand.Left;

        if (useLeft && leftEjectPoint != null)
            return leftEjectPoint.position;

        if (!useLeft && rightEjectPoint != null)
            return rightEjectPoint.position;

        if (dropOrigin != null)
            return dropOrigin.position;

        if (useLeft && leftHoldPivot != null)
            return leftHoldPivot.position;

        if (rightHoldPivot != null)
            return rightHoldPivot.position;

        return transform.position + transform.forward * 0.5f;
    }

    private Vector3 ComputeThrowPosition(byte handIndex)
    {
        bool useLeft = handIndex == 1;

        if (useLeft && leftEjectPoint != null)
            return leftEjectPoint.position;

        if (!useLeft && rightEjectPoint != null)
            return rightEjectPoint.position;

        if (dropOrigin != null)
            return dropOrigin.position;

        if (useLeft && leftHoldPivot != null)
            return leftHoldPivot.position;

        if (rightHoldPivot != null)
            return rightHoldPivot.position;

        return transform.position + transform.forward * 0.5f;
    }

    private bool IsPositionBlocked(Vector3 position)
    {
        Vector3 bodyCenter = transform.position + Vector3.up * 0.5f;
        float dist = Vector3.Distance(bodyCenter, position);

        if (dist < 0.01f)
            return false;

        Collider[] hits = Physics.OverlapCapsule(
            bodyCenter,
            position,
            throwBlockCheckRadius,
            throwBlockMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (!hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    public void LaunchActiveItem(float charge, Vector3 direction)
    {
        if (inventory == null)
            return;

        int slot = inventory.ActiveSlot;

        if (slot < 0)
            return;

        if (inventory.IsSlotLocked(slot))
        {
            Debug.Log($"Slot {slot} is locked. Cannot drop item.");
            return;
        }

        string itemName = inventory.GetItemAtSlot(slot);

        if (string.IsNullOrEmpty(itemName))
            return;

        Vector3 pos = GetLaunchPosition();

        if (IsPositionBlocked(pos))
            return;

        if (currentHeldVisual != null)
        {
            Destroy(currentHeldVisual);
            currentHeldVisual = null;
        }

        Vector3 velocity = direction * Mathf.Lerp(minThrowForce, maxThrowForce, charge);

        GameObject heldVisual = inventory.GetSlotHeldPrefab(slot);

        bool canRemove = inventory.CanRemoveItem(slot);

        if (!canRemove)
            return;

        bool wasRoleItem = inventory.ActiveSlotIsRoleItem;
        PlayerRole droppedRole = inventory.ActiveSlotRole;

        inventory.RemoveItem(slot);

        if (wasRoleItem && droppedRole != PlayerRole.None)
        {
            var roleComponent = GetComponent<NetworkPlayerRole>();

            if (roleComponent != null)
                roleComponent.RequestSetRole(PlayerRole.None);
        }

        if (IsSpawned)
        {
            if (!IsOwner)
                return;

            byte handIndex = playerController != null
                ? (byte)(playerController.SelectedInteractionHand == PlayerController.InteractionHand.Right ? 0 : 1)
                : (byte)0;

            LaunchServerRpc(slot, new FixedString32Bytes(itemName), handIndex, velocity);
            RemoveSlotWorldIdServerRpc(slot);

            if (heldVisual != null)
                Destroy(heldVisual);
        }
        else
        {
            Quaternion rot = Quaternion.LookRotation(direction);
            GameObject dropObj = BuildDropItem(pos, rot, heldVisual, itemName);
            Rigidbody rb = dropObj.GetComponent<Rigidbody>();

            if (rb != null)
                rb.linearVelocity = velocity;

            if (heldVisual != null)
                Destroy(heldVisual);
        }
    }

    public bool CanLaunchActiveItem()
    {
        if (inventory == null)
            return false;

        int slot = inventory.ActiveSlot;

        if (slot < 0)
            return false;

        if (inventory.IsSlotLocked(slot))
            return false;

        string itemName = inventory.GetItemAtSlot(slot);

        if (string.IsNullOrEmpty(itemName))
            return false;

        return true;
    }

    [ServerRpc]
    private void LaunchServerRpc(
        int slot,
        FixedString32Bytes itemName,
        byte handIndex,
        Vector3 velocity
    )
    {
        if (inventory != null)
            inventory.RemoveItem(slot);

        ServerUntrackItem(itemName.ToString());

        Vector3 position = ComputeThrowPosition(handIndex);
        Quaternion rotation = Quaternion.LookRotation(velocity);

        string name = itemName.ToString();
        int idx = GetPrefabIndex(name);

        GameObject obj;

        if (idx >= 0 && idx < items.Length && items[idx].WorldDropPrefab != null)
        {
            obj = Instantiate(items[idx].WorldDropPrefab, position, rotation);
        }
        else
        {
            obj = new GameObject($"Dropped_{name}");
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.AddComponent<BoxCollider>();
            obj.AddComponent<Rigidbody>().useGravity = true;
            var pickable = obj.AddComponent<PickableItem>();
            pickable.Setup(name, null);
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.linearVelocity = velocity;

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
            netObj = obj.AddComponent<NetworkObject>();

        if (!netObj.IsSpawned)
            netObj.Spawn(true);

        ThrowItemSpawnClientRpc(netObj, position, velocity);
    }

    [ClientRpc]
    private void ThrowItemSpawnClientRpc(NetworkObjectReference itemRef, Vector3 position, Vector3 velocity)
    {
        if (itemRef.TryGet(out NetworkObject itemNetObj))
        {
            itemNetObj.transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));

            Rigidbody rb = itemNetObj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = velocity;
        }
    }

    private int GetPrefabIndex(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || items == null)
            return -1;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].Name == itemName)
                return i;
        }

        return -1;
    }

    private static GameObject BuildDropItem(
        Vector3 position,
        Quaternion rotation,
        GameObject heldVisual,
        string itemName
    )
    {
        GameObject obj = new GameObject($"Dropped_{itemName}");
        obj.transform.SetPositionAndRotation(position, rotation);

        if (heldVisual != null)
        {
            GameObject vis = Instantiate(heldVisual, obj.transform);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;

            var renderers = vis.GetComponentsInChildren<MeshRenderer>();

            foreach (var r in renderers)
                r.enabled = true;
        }

        obj.AddComponent<BoxCollider>();

        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = true;

        PickableItem pickable = obj.AddComponent<PickableItem>();
        pickable.Setup(itemName, null);

        return obj;
    }

    [ServerRpc]
    private void RemoveSlotWorldIdServerRpc(int slot)
    {
        if (serverSlotWorldIds.Remove(slot) && networkActiveSlot.Value == slot)
            networkActiveWorldId.Value = 0;
    }

    [ServerRpc]
    private void UpdateActiveSlotServerRpc(
        int slot,
        FixedString32Bytes itemName,
        byte handIndex
    )
    {
        networkActiveSlot.Value = slot;
        networkActiveItemName.Value = itemName;
        networkActiveHand.Value = handIndex;
    }

    private void OnActiveSlotChanged(int oldValue, int newValue)
    {
        if (inventory != null)
            inventory.SetActiveSlotFromNetwork(newValue);

        UpdateHeldVisual(
            newValue,
            networkActiveItemName.Value.ToString(),
            networkActiveHand.Value
        );
    }

    private void OnActiveItemNameChanged(
        FixedString32Bytes oldValue,
        FixedString32Bytes newValue
    )
    {
        int slot = networkActiveSlot.Value;
        string itemName = newValue.ToString();

        if (inventory != null && slot >= 0)
            inventory.SetSlotFromNetwork(slot, itemName);

        UpdateHeldVisual(slot, itemName, networkActiveHand.Value);
    }

    private void OnActiveHandChanged(byte oldValue, byte newValue)
    {
        UpdateHeldVisual(
            networkActiveSlot.Value,
            networkActiveItemName.Value.ToString(),
            newValue
        );
    }

    private void UpdateHeldVisual(int slot, string itemName, byte handIndex)
    {
        if (currentHeldVisual != null)
        {
            Destroy(currentHeldVisual);
            currentHeldVisual = null;
        }

        if (slot < 0 || string.IsNullOrEmpty(itemName))
            return;

        Transform pivot = handIndex == 0 ? rightHoldPivot : leftHoldPivot;

        if (pivot == null)
            return;

        GameObject prefab = inventory != null
            ? inventory.GetSlotHeldPrefab(slot)
            : null;

        if (prefab == null)
            prefab = cachedWorldVisualPrefab;

        if (prefab == null)
        {
            int prefabIndex = GetPrefabIndex(itemName);

            if (prefabIndex >= 0 && prefabIndex < items.Length)
                prefab = items[prefabIndex].WorldDropPrefab;
        }

        if (prefab == null)
            return;

        currentHeldVisual = Instantiate(prefab, pivot);
        currentHeldVisual.transform.localPosition = Vector3.zero;
        currentHeldVisual.transform.localRotation = Quaternion.identity;
        currentHeldVisual.transform.localScale = Vector3.one * heldItemScale;

        var renderers = currentHeldVisual.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
            r.enabled = true;

        var rigidbodies = currentHeldVisual.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
            Destroy(rb);

        var colliders = currentHeldVisual.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
            Destroy(c);
    }
}