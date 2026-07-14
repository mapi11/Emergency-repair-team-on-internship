using Unity.Netcode;
using UnityEngine;

public class PickableItem : Interactable
{
    [Header("Pickup")]
    [SerializeField] private InventoryItemType itemType = InventoryItemType.Wrench;
    [SerializeField] private Sprite inventoryIcon;

    [Header("Held Visual")]
    [SerializeField] private GameObject heldVisualPrefab;

    private MeshFilter meshFilter;

    private void Awake()
    {
        meshFilter = GetComponentInChildren<MeshFilter>();
    }

    public void Setup(InventoryItemType type, Sprite icon)
    {
        itemType = type;
        inventoryIcon = icon;
    }

    private GameObject BuildVisualFromSelf()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            GameObject visual = new GameObject($"{gameObject.name}_HeldVisual");
            MeshFilter mf = visual.AddComponent<MeshFilter>();
            mf.sharedMesh = meshFilter.sharedMesh;
            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = meshFilter.GetComponent<MeshRenderer>()?.sharedMaterial;
            mr.enabled = false;
            return visual;
        }
        return null;
    }

    public override void OnHandBegin(PlayerController player)
    {
        Inventory inv = player.GetComponent<Inventory>();
        if (inv == null) return;

        GameObject heldVisual = heldVisualPrefab;
        if (heldVisual == null)
            heldVisual = BuildVisualFromSelf();
        if (heldVisual == null)
            return;

        int slot = inv.AddItem(itemType, inventoryIcon, heldVisual, IsSpawned ? gameObject : null);
        if (slot < 0)
        {
            if (!IsSpawned)
                Destroy(heldVisual);
            return;
        }

        if (IsSpawned)
        {
            var playerNetObj = player.GetComponent<NetworkObject>();
            if (playerNetObj != null)
                HideServerRpc(slot, playerNetObj);
            else
                NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnHandHold(PlayerController player, float deltaTime) { }
    public override void OnHandEnd(PlayerController player) { }

    [ServerRpc(RequireOwnership = false)]
    private void HideServerRpc(int slot, NetworkObjectReference playerRef)
    {
        if (playerRef.TryGet(out NetworkObject playerNetObj))
        {
            var sync = playerNetObj.GetComponent<NetworkInventorySync>();
            if (sync != null)
                sync.ServerSetSlotWorldId(slot, NetworkObjectId);
        }

        HideClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropServerRpc(Vector3 position, Quaternion rotation)
    {
        DropClientRpc(position, rotation);
    }

    [ClientRpc]
    private void HideClientRpc()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = false;
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = false;
    }

    [ClientRpc]
    private void DropClientRpc(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = true;
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = true;
    }
}
