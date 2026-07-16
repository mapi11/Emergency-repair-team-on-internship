using UnityEngine;
using UnityEngine.InputSystem;

public class MissionInventoryLockTester : MonoBehaviour
{
    [SerializeField] private MissionInventoryLock missionInventoryLock;
    [SerializeField] private Key lockKey = Key.L;
    [SerializeField] private Key unlockKey = Key.U;

    private void Awake()
    {
        if (missionInventoryLock == null)
            missionInventoryLock = FindFirstObjectByType<MissionInventoryLock>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (missionInventoryLock == null)
            return;

        if (Keyboard.current[lockKey].wasPressedThisFrame)
        {
            missionInventoryLock.LockRoleSlotsForEveryone();
            Debug.Log("🔒 Test: locked role slots for everyone.");
        }

        if (Keyboard.current[unlockKey].wasPressedThisFrame)
        {
            missionInventoryLock.UnlockLocalPlayerRoleSlots();
            Debug.Log("🔓 Test: unlocked local role slots.");
        }
    }
}