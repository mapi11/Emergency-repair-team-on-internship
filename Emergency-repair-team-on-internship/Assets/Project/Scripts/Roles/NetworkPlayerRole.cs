using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkPlayerRole : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform roleIconsContainer;
    [SerializeField] private GameObject roleIconPrefab;
    [SerializeField] private Sprite[] roleSprites;

    private readonly NetworkVariable<byte> networkRoleMask = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public byte NetworkRoleMask => networkRoleMask.Value;

    private static byte RoleToBit(PlayerRole role) => (byte)(1 << ((int)role - 1));

    public PlayerRole CurrentRole
    {
        get
        {
            byte mask = networkRoleMask.Value;
            if (mask == 0) return PlayerRole.None;
            int max = roleSprites != null ? roleSprites.Length : 4;
            for (int i = 1; i <= max; i++)
                if ((mask & (1 << (i - 1))) != 0)
                    return (PlayerRole)i;
            return PlayerRole.None;
        }
    }

    public event System.Action<byte, byte> OnRoleMaskChangedEvent;

    public override void OnNetworkSpawn()
    {
        networkRoleMask.OnValueChanged += OnRoleMaskChanged;
        RebuildIcons(networkRoleMask.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkRoleMask.OnValueChanged -= OnRoleMaskChanged;
    }

    public void RequestAddRole(PlayerRole role)
    {
        if (!IsSpawned || role == PlayerRole.None) return;
        byte bit = RoleToBit(role);
        if (IsServer)
            networkRoleMask.Value |= bit;
        else
            AddRoleServerRpc(bit);
    }

    public void RequestRemoveRole(PlayerRole role)
    {
        if (!IsSpawned || role == PlayerRole.None) return;
        byte bit = RoleToBit(role);
        if (IsServer)
            networkRoleMask.Value &= (byte)~bit;
        else
            RemoveRoleServerRpc(bit);
    }

    public bool HasRole(PlayerRole role)
    {
        return (networkRoleMask.Value & RoleToBit(role)) != 0;
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddRoleServerRpc(byte bit, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        networkRoleMask.Value |= bit;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RemoveRoleServerRpc(byte bit, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        networkRoleMask.Value &= (byte)~bit;
    }

    private void OnRoleMaskChanged(byte oldMask, byte newMask)
    {
        RebuildIcons(newMask);
        OnRoleMaskChangedEvent?.Invoke(oldMask, newMask);
    }

    private void RebuildIcons(byte mask)
    {
        if (roleIconsContainer == null) return;

        for (int i = roleIconsContainer.childCount - 1; i >= 0; i--)
        {
            var child = roleIconsContainer.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }

        if (mask == 0 || roleIconPrefab == null)
        {
            roleIconsContainer.gameObject.SetActive(false);
            return;
        }

        roleIconsContainer.gameObject.SetActive(true);

        int max = roleSprites != null ? roleSprites.Length : 4;
        for (int i = 1; i <= max; i++)
        {
            if ((mask & (1 << (i - 1))) != 0)
            {
                var iconGO = Instantiate(roleIconPrefab, roleIconsContainer);
                iconGO.name = $"RoleIcon_{(PlayerRole)i}";
                var img = iconGO.GetComponent<Image>();
                if (img != null && roleSprites != null && i - 1 < roleSprites.Length)
                    img.sprite = roleSprites[i - 1];
            }
        }
    }
}
