using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkPlayerRole : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private Image roleIcon;
    [SerializeField] private Sprite[] roleSprites;

    private readonly NetworkVariable<byte> networkRole = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public PlayerRole CurrentRole => (PlayerRole)networkRole.Value;

    private void Awake()
    {
        if (roleIcon != null)
            roleIcon.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        networkRole.OnValueChanged += OnRoleChanged;
        UpdateIcon(networkRole.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkRole.OnValueChanged -= OnRoleChanged;
    }

    public void RequestSetRole(PlayerRole role)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            SetRoleOnServer((byte)role, OwnerClientId);
        }
        else
        {
            RequestSetRoleServerRpc((byte)role);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetRoleServerRpc(byte role, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (senderClientId != OwnerClientId)
            return;

        SetRoleOnServer(role, senderClientId);
    }

    private void SetRoleOnServer(byte role, ulong senderClientId)
    {
        if (senderClientId != OwnerClientId)
            return;

        networkRole.Value = role;
    }

    private void OnRoleChanged(byte oldRole, byte newRole)
    {
        UpdateIcon(newRole);
    }

    private void UpdateIcon(byte role)
    {
        if (roleIcon == null)
            return;

        if (role == 0 || roleSprites == null || role - 1 >= roleSprites.Length)
        {
            roleIcon.enabled = false;
            return;
        }

        Sprite sprite = roleSprites[role - 1];

        if (sprite == null)
        {
            roleIcon.enabled = false;
            return;
        }

        roleIcon.sprite = sprite;
        roleIcon.enabled = true;
    }
}
