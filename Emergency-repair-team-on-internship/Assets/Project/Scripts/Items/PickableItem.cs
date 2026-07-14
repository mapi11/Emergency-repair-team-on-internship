using Unity.Netcode;
using UnityEngine;

public class PickableItem : Interactable
{
    [Header("Pickup")]
    [SerializeField] private string itemName = "Wrench";
    [SerializeField] private Sprite inventoryIcon;

    [Header("Held Visual")]
    [SerializeField] private GameObject heldVisualPrefab;

    private MeshFilter meshFilter;

    private void Awake()
    {
        meshFilter = GetComponentInChildren<MeshFilter>();
    }

    public void Setup(string name, Sprite icon)
    {
        itemName = name;
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

        int slot = inv.AddItem(itemName, inventoryIcon, heldVisual, IsSpawned ? gameObject : null);
        if (slot < 0)
        {
            Destroy(heldVisual);
            return;
        }

        SetCanInteract(false);

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
    public void DropServerRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        DropClientRpc(position, rotation, velocity);
    }

    [ClientRpc]
    private void HideClientRpc()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = false;
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    [ClientRpc]
    private void DropClientRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        SetCanInteract(true);

        transform.SetPositionAndRotation(position, rotation);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = velocity;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = true;
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = true;
    }
}
