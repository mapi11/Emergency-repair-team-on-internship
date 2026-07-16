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

    public GameObject CachedWorldVisualPrefab
    {
        get
        {
            if (cachedWorldVisualPrefab == null)
                BuildCachedWorldVisual();

            return cachedWorldVisualPrefab;
        }
    }

    [Header("Pickup")]
    [SerializeField] private string itemName = "Wrench";
    [SerializeField] private Sprite inventoryIcon;

    [Header("Held Visual")]
    [SerializeField] private GameObject heldVisualPrefab;

    [Header("Role")]
    [SerializeField] private RoleItem roleItem;

    private readonly NetworkVariable<bool> networkHidden = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Vector3> networkPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Vector3> networkRotation = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Vector3> networkVelocity = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Vector3> networkAngularVelocity = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameObject cachedWorldVisualPrefab;
    private float lastSyncTime;

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

    private void Update()
    {
        if (!IsSpawned || !IsServer || networkHidden.Value)
            return;

        if (Time.unscaledTime - lastSyncTime < 0.2f)
            return;

        lastSyncTime = Time.unscaledTime;
        networkPosition.Value = transform.position;
        networkRotation.Value = transform.rotation.eulerAngles;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            networkVelocity.Value = rb.linearVelocity;
            networkAngularVelocity.Value = rb.angularVelocity;
        }
    }

    public override void OnNetworkSpawn()
    {
        networkHidden.OnValueChanged += OnHiddenChanged;
        ApplyHiddenState(networkHidden.Value);

        if (!networkHidden.Value)
            ApplyNetworkPosition();
    }

    public override void OnNetworkDespawn()
    {
        networkHidden.OnValueChanged -= OnHiddenChanged;
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

        bool canAddRoleItem = inventory.CanAddRoleItem(
            roleItem.Role,
            roleItem.Category
        );

        return canAddRoleItem;
    }

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

        GameObject heldVisual = heldVisualPrefab;

        if (heldVisual == null)
            heldVisual = BuildVisualFromSelf();

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

        networkHidden.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropServerRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        networkHidden.Value = false;
        networkPosition.Value = position;
        networkRotation.Value = rotation.eulerAngles;
        networkVelocity.Value = velocity;
        networkAngularVelocity.Value = Vector3.zero;
        DropClientRpc(position, rotation, velocity);
    }

    private void OnHiddenChanged(bool oldValue, bool newValue)
    {
        ApplyHiddenState(newValue);
    }

    private void ApplyNetworkPosition()
    {
        Vector3 pos = networkPosition.Value;
        Quaternion rot = networkRotation.Value.sqrMagnitude > 0.001f
            ? Quaternion.Euler(networkRotation.Value)
            : transform.rotation;

        if (pos.sqrMagnitude > 0.001f)
            transform.SetPositionAndRotation(pos, rot);

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = networkVelocity.Value;
            rb.angularVelocity = networkAngularVelocity.Value;
        }
    }

    private void ApplyHiddenState(bool hidden)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);

        foreach (var r in renderers)
            r.enabled = !hidden;

        var colliders = GetComponentsInChildren<Collider>(true);

        foreach (var c in colliders)
            c.enabled = !hidden;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = hidden;
            rb.useGravity = !hidden;

            if (hidden)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    public void TryRebuildCachedVisual()
    {
        if (networkHidden.Value)
            return;

        BuildCachedWorldVisual();
    }

    private void BuildCachedWorldVisual()
    {
        if (!IsSpawned)
            return;

        ulong worldId = NetworkObjectId;
        var netObj = this;

        if (cachedWorldVisualPrefab != null)
        {
            Destroy(cachedWorldVisualPrefab);
            cachedWorldVisualPrefab = null;
        }

        var mfs = netObj.GetComponentsInChildren<MeshFilter>();

        if (mfs.Length == 0)
            return;

        cachedWorldVisualPrefab = new GameObject("Pickable_CachedVisual");

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null)
                continue;

            GameObject child = new GameObject(mf.name);
            child.transform.SetParent(cachedWorldVisualPrefab.transform, false);
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
    }
}