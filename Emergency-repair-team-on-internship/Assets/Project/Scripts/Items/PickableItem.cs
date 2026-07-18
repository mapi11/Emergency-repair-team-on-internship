using Unity.Netcode;
using UnityEngine;

public class PickableItem : Interactable
{
    public static PickableItem Find(ulong networkObjectId)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                networkObjectId, out var netObj))
            return null;

        return netObj.GetComponent<PickableItem>();
    }

    [Header("Pickup")]
    [SerializeField] private string itemName = "Wrench";
    [SerializeField] private Sprite inventoryIcon;

    [Header("Held Visual")]
    [SerializeField] private GameObject heldVisualPrefab;

    [Header("Role")]
    [SerializeField] private RoleItem roleItem;

    public string ItemName => itemName;
    public Sprite InventoryIcon => inventoryIcon;
    public GameObject HeldVisualPrefab => heldVisualPrefab;

    public void Setup(string name, Sprite icon)
    {
        itemName = name;
        inventoryIcon = icon;
    }

    private void Awake()
    {
        if (roleItem == null)
            roleItem = GetComponent<RoleItem>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer && !string.IsNullOrEmpty(gameObject.scene.name))
            MissionManager.RegisterDespawnedSceneObject(GetSceneKey());
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player))
            return false;

        if (player == null)
            return false;

        if (roleItem == null)
            roleItem = GetComponent<RoleItem>();

        if (roleItem == null || !roleItem.IsRoleItem)
            return true;

        Inventory inventory = player.GetComponent<Inventory>();

        if (inventory == null)
            return false;

        return inventory.CanAddRoleItem(roleItem.Role, roleItem.Category);
    }

    public override void OnHandBegin(PlayerController player)
    {
        Inventory inv = player.GetComponent<Inventory>();

        if (inv == null)
            return;

        if (roleItem == null)
            roleItem = GetComponent<RoleItem>();

        bool isRoleItem = roleItem != null && roleItem.IsRoleItem;
        PlayerRole role = isRoleItem ? roleItem.Role : PlayerRole.None;
        RoleItemCategory category = isRoleItem ? roleItem.Category : RoleItemCategory.None;

        if (isRoleItem && !inv.CanAddRoleItem(role, category))
        {
            Debug.Log("Cannot pick this role item. Player already has primary role item.");
            return;
        }

        GameObject heldVisual;

        if (heldVisualPrefab != null)
        {
            heldVisual = heldVisualPrefab;
        }
        else
        {
            heldVisual = BuildVisualFromSelf();
            if (heldVisual != null)
                DontDestroyOnLoad(heldVisual);
        }

        if (heldVisual == null)
            return;

        int slot = inv.AddItem(
            itemName,
            inventoryIcon,
            heldVisual,
            IsSpawned ? gameObject : null,
            isRoleItem,
            role,
            category
        );

        if (slot < 0)
        {
            if (heldVisualPrefab == null)
                Destroy(heldVisual);
            return;
        }

        if (isRoleItem)
        {
            var roleComponent = player.GetComponent<NetworkPlayerRole>();

            if (roleComponent != null)
                roleComponent.RequestSetRole(role);
        }

        SetCanInteract(false);

        if (IsSpawned)
        {
            var playerNetObj = player.GetComponent<NetworkObject>();

            if (playerNetObj != null)
                PickupServerRpc(slot, playerNetObj);
            else
                NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickupServerRpc(int slot, NetworkObjectReference playerRef)
    {
        if (playerRef.TryGet(out NetworkObject playerNetObj))
        {
            var sync = playerNetObj.GetComponent<NetworkInventorySync>();

            if (sync != null)
                sync.ServerSetSlotWorldId(slot, NetworkObjectId);
        }

        MissionManager.RegisterDespawnedSceneObject(GetSceneKey());
        NetworkObject.Despawn(true);
    }

    public string GetSceneKey()
    {
        var pos = transform.position;
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}_{1}_{2:F1}_{3:F1}_{4:F1}",
            gameObject.scene.name, gameObject.name,
            pos.x, pos.y, pos.z
        );
    }

    public override void OnHandHold(PlayerController player, float deltaTime) { }

    public override void OnHandEnd(PlayerController player) { }

    private GameObject BuildVisualFromSelf()
    {
        var meshFilters = GetComponentsInChildren<MeshFilter>();

        if (meshFilters.Length == 0)
            return null;

        GameObject visual = new GameObject($"{gameObject.name}_HeldVisual");

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null)
                continue;

            GameObject child = new GameObject(mf.name);
            child.transform.SetParent(visual.transform, false);
            child.transform.localPosition = transform.InverseTransformPoint(mf.transform.position);
            child.transform.localRotation = Quaternion.Inverse(transform.rotation) * mf.transform.rotation;

            Vector3 parentScale = transform.lossyScale;
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
}
