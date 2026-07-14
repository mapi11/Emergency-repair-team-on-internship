using System.Collections.Generic;
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
    [SerializeField] private GameObject[] heldItemPrefabs;
    [SerializeField] private GameObject[] worldDropPrefabs;
    [SerializeField] private float heldItemScale = 0.8f;

    private readonly NetworkVariable<int> networkActiveSlot = new(-1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<byte> networkActiveItemType = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<byte> networkActiveHand = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> networkActiveWorldId = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject currentHeldVisual;
    private readonly Dictionary<int, ulong> serverSlotWorldIds = new();
    private GameObject cachedWorldVisualPrefab;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        inventory.OnActiveSlotChanged += OnLocalActiveSlotChanged;
    }

    public override void OnNetworkSpawn()
    {
        networkActiveSlot.OnValueChanged += OnActiveSlotChanged;
        networkActiveItemType.OnValueChanged += OnActiveItemTypeChanged;
        networkActiveHand.OnValueChanged += OnActiveHandChanged;
        networkActiveWorldId.OnValueChanged += OnActiveWorldIdChanged;

        UpdateHeldVisual(networkActiveSlot.Value, (InventoryItemType)(byte)networkActiveItemType.Value, networkActiveHand.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkActiveSlot.OnValueChanged -= OnActiveSlotChanged;
        networkActiveItemType.OnValueChanged -= OnActiveItemTypeChanged;
        networkActiveHand.OnValueChanged -= OnActiveHandChanged;
        networkActiveWorldId.OnValueChanged -= OnActiveWorldIdChanged;
    }

    public void ServerSetSlotWorldId(int slot, ulong worldId)
    {
        if (!IsServer) return;

        serverSlotWorldIds[slot] = worldId;

        if (networkActiveSlot.Value == slot)
            networkActiveWorldId.Value = worldId;
    }

    private void OnActiveWorldIdChanged(ulong oldId, ulong newId)
    {
        if (cachedWorldVisualPrefab != null)
        {
            Destroy(cachedWorldVisualPrefab);
            cachedWorldVisualPrefab = null;
        }

        if (newId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newId, out var netObj))
        {
            var mf = netObj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                cachedWorldVisualPrefab = new GameObject("NetHeldVisual");
                cachedWorldVisualPrefab.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = cachedWorldVisualPrefab.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                mr.enabled = false;
            }
        }

        UpdateHeldVisual(networkActiveSlot.Value, (InventoryItemType)(byte)networkActiveItemType.Value, networkActiveHand.Value);
    }

    private void OnLocalActiveSlotChanged(int slot)
    {
        if (IsSpawned && networkActiveSlot.Value == slot)
            return;

        byte handIndex = playerController != null
            ? (byte)(playerController.SelectedInteractionHand == PlayerController.InteractionHand.Right ? 0 : 1)
            : (byte)0;

        InventoryItemType itemType = slot >= 0 ? inventory.GetItemAtSlot(slot) : InventoryItemType.None;

        UpdateHeldVisual(slot, itemType, handIndex);

        if (IsSpawned)
        {
            UpdateActiveSlotServerRpc(slot, (byte)itemType, handIndex);

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
    }

    [ServerRpc]
    private void SyncWorldIdServerRpc(int slot)
    {
        ulong worldId = slot >= 0 && serverSlotWorldIds.TryGetValue(slot, out ulong id) ? id : 0;
        networkActiveWorldId.Value = worldId;
    }

    public void SyncFullState(byte handIndex = 0)
    {
        int slot = inventory.ActiveSlot;
        byte itemType = (byte)(slot >= 0 ? inventory.GetItemAtSlot(slot) : InventoryItemType.None);

        networkActiveSlot.Value = slot;
        networkActiveItemType.Value = itemType;
        networkActiveHand.Value = handIndex;

        if (IsServer)
        {
            ulong worldId = slot >= 0 && serverSlotWorldIds.TryGetValue(slot, out ulong id) ? id : 0;
            networkActiveWorldId.Value = worldId;
        }
    }

    public void DropActiveItem()
    {
        int slot = inventory.ActiveSlot;

        if (slot < 0)
            return;

        InventoryItemType itemType = inventory.GetItemAtSlot(slot);

        if (itemType == InventoryItemType.None)
            return;

        if (currentHeldVisual != null)
        {
            Destroy(currentHeldVisual);
            currentHeldVisual = null;
        }

        Vector3 pos = dropOrigin != null ? dropOrigin.position : transform.position + transform.forward * 0.5f;
        GameObject worldObject = inventory.GetSlotDropPrefab(slot);
        GameObject heldVisual = inventory.GetSlotHeldPrefab(slot);

        inventory.RemoveItem(slot);

        if (IsSpawned)
        {
            if (!IsOwner)
                return;

            if (worldObject != null)
            {
                PickableItem pickable = worldObject.GetComponent<PickableItem>();

                if (pickable != null)
                    pickable.DropServerRpc(pos, Quaternion.identity);
            }
            else
            {
                DropActiveItemServerRpc(slot, (byte)itemType, pos, Quaternion.identity);
            }

            RemoveSlotWorldIdServerRpc(slot);
        }
        else
        {
            GameObject dropObj = BuildDropItem(pos, Quaternion.identity, heldVisual, itemType);

            if (heldVisual != null)
                Destroy(heldVisual);
        }
    }

    [ServerRpc]
    private void DropActiveItemServerRpc(int slot, byte itemType, Vector3 position, Quaternion rotation)
    {
        inventory.RemoveItem(slot);
        SyncFullState(networkActiveHand.Value);

        int idx = (int)itemType - 1;

        if (idx >= 0 && idx < worldDropPrefabs.Length && worldDropPrefabs[idx] != null)
            Instantiate(worldDropPrefabs[idx], position, rotation);
    }

    private static GameObject BuildDropItem(Vector3 position, Quaternion rotation, GameObject heldVisual, InventoryItemType itemType)
    {
        GameObject obj = new GameObject($"Dropped_{itemType}");
        obj.transform.SetPositionAndRotation(position, rotation);

        if (heldVisual != null)
        {
            GameObject vis = Instantiate(heldVisual, obj.transform);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;
            var renderer = vis.GetComponentInChildren<MeshRenderer>();
            if (renderer != null) renderer.enabled = true;
        }

        obj.AddComponent<BoxCollider>();
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = true;

        PickableItem pickable = obj.AddComponent<PickableItem>();
        pickable.Setup(itemType, null);

        return obj;
    }

    [ServerRpc]
    private void RemoveSlotWorldIdServerRpc(int slot)
    {
        if (serverSlotWorldIds.Remove(slot) && networkActiveSlot.Value == slot)
            networkActiveWorldId.Value = 0;
    }

    [ServerRpc]
    private void UpdateActiveSlotServerRpc(int slot, byte itemType, byte handIndex)
    {
        networkActiveSlot.Value = slot;
        networkActiveItemType.Value = itemType;
        networkActiveHand.Value = handIndex;
    }

    private void OnActiveSlotChanged(int oldValue, int newValue)
    {
        inventory.SetActiveSlotFromNetwork(newValue);
        UpdateHeldVisual(newValue, (InventoryItemType)(byte)networkActiveItemType.Value, networkActiveHand.Value);
    }

    private void OnActiveItemTypeChanged(byte oldValue, byte newValue)
    {
        int slot = networkActiveSlot.Value;

        if (slot >= 0)
            inventory.SetSlotFromNetwork(slot, (InventoryItemType)newValue);

        UpdateHeldVisual(slot, (InventoryItemType)newValue, networkActiveHand.Value);
    }

    private void OnActiveHandChanged(byte oldValue, byte newValue)
    {
        UpdateHeldVisual(networkActiveSlot.Value, (InventoryItemType)(byte)networkActiveItemType.Value, newValue);
    }

    private void UpdateHeldVisual(int slot, InventoryItemType itemType, byte handIndex)
    {
        if (currentHeldVisual != null)
        {
            Destroy(currentHeldVisual);
            currentHeldVisual = null;
        }

        if (slot < 0 || itemType == InventoryItemType.None)
            return;

        Transform pivot = handIndex == 0 ? rightHoldPivot : leftHoldPivot;

        if (pivot == null)
            return;

        GameObject prefab = inventory != null ? inventory.GetSlotHeldPrefab(slot) : null;

        if (prefab == null)
            prefab = cachedWorldVisualPrefab;

        if (prefab == null)
        {
            int prefabIndex = (int)itemType - 1;

            if (prefabIndex >= 0 && prefabIndex < heldItemPrefabs.Length)
                prefab = heldItemPrefabs[prefabIndex];
        }

        if (prefab == null)
            return;

        currentHeldVisual = Instantiate(prefab, pivot.position, pivot.rotation, pivot);
        currentHeldVisual.transform.localScale = Vector3.one * heldItemScale;
        var renderer = currentHeldVisual.GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderer.enabled = true;
    }
}
