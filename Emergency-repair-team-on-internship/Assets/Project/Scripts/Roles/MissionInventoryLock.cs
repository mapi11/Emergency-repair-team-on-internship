using Unity.Netcode;
using UnityEngine;

public class MissionInventoryLock : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool lockRoleSlotsOnStart;

    private void Start()
    {
        if (lockRoleSlotsOnStart)
            LockLocalPlayerRoleSlots();
    }

    public void LockLocalPlayerRoleSlots()
    {
        Inventory inventory = FindLocalInventory();

        if (inventory == null)
            return;

        inventory.LockRoleSlots();
    }

    public void UnlockLocalPlayerRoleSlots()
    {
        Inventory inventory = FindLocalInventory();

        if (inventory == null)
            return;

        inventory.UnlockRoleSlots();
    }

    public void LockRoleSlotsForEveryone()
    {
        if (IsServer)
        {
            LockRoleSlotsClientRpc();
        }
        else
        {
            RequestLockRoleSlotsServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestLockRoleSlotsServerRpc()
    {
        LockRoleSlotsClientRpc();
    }

    [ClientRpc]
    private void LockRoleSlotsClientRpc()
    {
        LockLocalPlayerRoleSlots();
    }

    private Inventory FindLocalInventory()
    {
        NetworkManager net = NetworkManager.Singleton;

        if (net != null && net.IsClient)
        {
            NetworkObject localPlayer = net.LocalClient?.PlayerObject;

            if (localPlayer != null)
                return localPlayer.GetComponent<Inventory>();
        }

        return FindFirstObjectByType<Inventory>();
    }
}