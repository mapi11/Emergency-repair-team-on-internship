using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int maxSlots = 3;

    private string[] slots;
    private Sprite[] slotIcons;
    private GameObject[] slotHeldPrefabs;
    private GameObject[] slotDropPrefabs;

    private bool[] slotLocked;
    private bool[] slotRoleItems;
    private PlayerRole[] slotRoles;
    private RoleItemCategory[] slotRoleItemCategories;

    private int activeSlot = -1;

    public int MaxSlots => maxSlots;
    public int ActiveSlot => activeSlot;

    public string ActiveItemType =>
        activeSlot >= 0 && activeSlot < slots.Length
            ? slots[activeSlot]
            : null;

    public bool ActiveSlotIsLocked =>
        activeSlot >= 0 && activeSlot < slots.Length && slotLocked[activeSlot];

    public bool ActiveSlotIsRoleItem =>
        activeSlot >= 0 && activeSlot < slots.Length && slotRoleItems[activeSlot];

    public PlayerRole ActiveSlotRole =>
        activeSlot >= 0 && activeSlot < slots.Length
            ? slotRoles[activeSlot]
            : PlayerRole.None;

    public RoleItemCategory ActiveSlotRoleItemCategory =>
        activeSlot >= 0 && activeSlot < slots.Length
            ? slotRoleItemCategories[activeSlot]
            : RoleItemCategory.None;

    public event System.Action<int, string> OnSlotChanged;
    public event System.Action<int> OnActiveSlotChanged;
    public event System.Action<int, Sprite> OnSlotIconChanged;
    public event System.Action<int, bool> OnSlotLockChanged;
    public event System.Action<int, bool, PlayerRole> OnSlotRoleChanged;

    private void Awake()
    {
        slots = new string[maxSlots];
        slotIcons = new Sprite[maxSlots];
        slotHeldPrefabs = new GameObject[maxSlots];
        slotDropPrefabs = new GameObject[maxSlots];

        slotLocked = new bool[maxSlots];
        slotRoleItems = new bool[maxSlots];
        slotRoles = new PlayerRole[maxSlots];
        slotRoleItemCategories = new RoleItemCategory[maxSlots];

        activeSlot = -1;
    }

    public int AddItem(
        string itemName,
        Sprite icon = null,
        GameObject heldPrefab = null,
        GameObject dropPrefab = null,
        bool isRoleItem = false,
        PlayerRole role = PlayerRole.None,
        RoleItemCategory roleItemCategory = RoleItemCategory.None
    )
    {
        if (string.IsNullOrEmpty(itemName))
            return -1;

        if (isRoleItem && !CanAddRoleItem(role, roleItemCategory))
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = itemName;
                slotIcons[i] = icon;
                slotHeldPrefabs[i] = heldPrefab;
                slotDropPrefabs[i] = dropPrefab;

                slotLocked[i] = false;
                slotRoleItems[i] = isRoleItem;
                slotRoles[i] = isRoleItem ? role : PlayerRole.None;
                slotRoleItemCategories[i] = isRoleItem ? roleItemCategory : RoleItemCategory.None;

                OnSlotChanged?.Invoke(i, itemName);
                OnSlotIconChanged?.Invoke(i, icon);
                OnSlotLockChanged?.Invoke(i, slotLocked[i]);
                OnSlotRoleChanged?.Invoke(i, slotRoleItems[i], slotRoles[i]);

                if (activeSlot < 0)
                    SetActiveSlot(i);

                return i;
            }
        }

        return -1;
    }

    public bool CanAddRoleItem(PlayerRole role, RoleItemCategory category)
    {
        if (role == PlayerRole.None || category == RoleItemCategory.None)
            return true;

        if (category == RoleItemCategory.PrimaryRole && HasPrimaryRoleItem())
            return false;

        return true;
    }

    public void RemoveItem(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        if (slotLocked[slot])
        {
            Debug.Log($"Slot {slot} is locked. Item cannot be removed.");
            return;
        }

        ForceRemoveItem(slot);
    }

    public void ForceRemoveItem(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slots[slot] = null;
        slotIcons[slot] = null;
        slotHeldPrefabs[slot] = null;
        slotDropPrefabs[slot] = null;

        slotLocked[slot] = false;
        slotRoleItems[slot] = false;
        slotRoles[slot] = PlayerRole.None;
        slotRoleItemCategories[slot] = RoleItemCategory.None;

        OnSlotChanged?.Invoke(slot, null);
        OnSlotIconChanged?.Invoke(slot, null);
        OnSlotLockChanged?.Invoke(slot, false);
        OnSlotRoleChanged?.Invoke(slot, false, PlayerRole.None);

        if (activeSlot == slot)
        {
            int newSlot = FindNextFilledSlot();

            if (newSlot >= 0)
                SetActiveSlot(newSlot);
            else
                ClearActiveSlot();
        }
    }

    public bool CanRemoveItem(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return false;

        if (slots[slot] == null)
            return false;

        return !slotLocked[slot];
    }

    public bool SwitchToSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return false;

        if (activeSlot == slot)
        {
            ClearActiveSlot();
            return true;
        }

        if (slots[slot] == null)
        {
            ClearActiveSlot();
            return true;
        }

        SetActiveSlot(slot);
        return true;
    }

    private void SetActiveSlot(int slot)
    {
        activeSlot = slot;
        OnActiveSlotChanged?.Invoke(activeSlot);
    }

    public void ClearActiveSlot()
    {
        activeSlot = -1;
        OnActiveSlotChanged?.Invoke(-1);
    }

    public string GetItemAtSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        return slots[slot];
    }

    public Sprite GetSlotIcon(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        return slotIcons[slot];
    }

    public void SetSlotIcon(int slot, Sprite icon)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slotIcons[slot] = icon;
        OnSlotIconChanged?.Invoke(slot, icon);
    }

    public GameObject GetSlotHeldPrefab(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        return slotHeldPrefabs[slot];
    }

    public GameObject GetSlotDropPrefab(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        return slotDropPrefabs[slot];
    }

    public bool IsSlotLocked(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return false;

        return slotLocked[slot];
    }

    public void SetSlotLocked(int slot, bool locked)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slotLocked[slot] = locked;
        OnSlotLockChanged?.Invoke(slot, locked);
    }

    public bool IsRoleItemSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return false;

        return slotRoleItems[slot];
    }

    public PlayerRole GetSlotRole(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return PlayerRole.None;

        return slotRoles[slot];
    }

    public RoleItemCategory GetSlotRoleItemCategory(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return RoleItemCategory.None;

        return slotRoleItemCategories[slot];
    }

    public int FindFirstRoleSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slotRoleItems[i])
                return i;
        }

        return -1;
    }

    public int FindFirstPrimaryRoleSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null &&
                slotRoleItems[i] &&
                slotRoleItemCategories[i] == RoleItemCategory.PrimaryRole)
            {
                return i;
            }
        }

        return -1;
    }

    public bool HasRoleItem()
    {
        return FindFirstRoleSlot() >= 0;
    }

    public bool HasPrimaryRoleItem()
    {
        return FindFirstPrimaryRoleSlot() >= 0;
    }

    public PlayerRole GetCurrentRole()
    {
        int roleSlot = FindFirstPrimaryRoleSlot();

        if (roleSlot < 0)
            return PlayerRole.None;

        return slotRoles[roleSlot];
    }

    public void LockRoleSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null &&
                slotRoleItems[i] &&
                slotRoleItemCategories[i] == RoleItemCategory.PrimaryRole)
            {
                SetSlotLocked(i, true);
            }
        }
    }

    public void UnlockRoleSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slotRoleItems[i] &&
                slotRoleItemCategories[i] == RoleItemCategory.PrimaryRole)
            {
                SetSlotLocked(i, false);
            }
        }
    }

    public void SetSlotFromNetwork(int slot, string itemName)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slots[slot] = itemName;
        OnSlotChanged?.Invoke(slot, itemName);
    }

    public void SetActiveSlotFromNetwork(int slot)
    {
        activeSlot = slot;
        OnActiveSlotChanged?.Invoke(slot);
    }

    public void SetSlotRoleFromNetwork(int slot, bool isRoleItem, PlayerRole role)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slotRoleItems[slot] = isRoleItem;
        slotRoles[slot] = isRoleItem ? role : PlayerRole.None;

        if (!isRoleItem)
            slotRoleItemCategories[slot] = RoleItemCategory.None;

        OnSlotRoleChanged?.Invoke(slot, slotRoleItems[slot], slotRoles[slot]);
    }

    public void SetSlotRoleFromNetwork(
        int slot,
        bool isRoleItem,
        PlayerRole role,
        RoleItemCategory category
    )
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slotRoleItems[slot] = isRoleItem;
        slotRoles[slot] = isRoleItem ? role : PlayerRole.None;
        slotRoleItemCategories[slot] = isRoleItem ? category : RoleItemCategory.None;

        OnSlotRoleChanged?.Invoke(slot, slotRoleItems[slot], slotRoles[slot]);
    }

    public void SetSlotLockFromNetwork(int slot, bool locked)
    {
        SetSlotLocked(slot, locked);
    }

    private int FindNextFilledSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                return i;
        }

        return -1;
    }

    public void ExpandTo(int newMax)
    {
        if (newMax <= slots.Length)
            return;

        var newSlots = new string[newMax];
        var newIcons = new Sprite[newMax];
        var newHeldPrefabs = new GameObject[newMax];
        var newDropPrefabs = new GameObject[newMax];

        var newLocked = new bool[newMax];
        var newRoleItems = new bool[newMax];
        var newRoles = new PlayerRole[newMax];
        var newRoleItemCategories = new RoleItemCategory[newMax];

        for (int i = 0; i < slots.Length; i++)
        {
            newSlots[i] = slots[i];
            newIcons[i] = slotIcons[i];
            newHeldPrefabs[i] = slotHeldPrefabs[i];
            newDropPrefabs[i] = slotDropPrefabs[i];

            newLocked[i] = slotLocked[i];
            newRoleItems[i] = slotRoleItems[i];
            newRoles[i] = slotRoles[i];
            newRoleItemCategories[i] = slotRoleItemCategories[i];
        }

        slots = newSlots;
        slotIcons = newIcons;
        slotHeldPrefabs = newHeldPrefabs;
        slotDropPrefabs = newDropPrefabs;

        slotLocked = newLocked;
        slotRoleItems = newRoleItems;
        slotRoles = newRoles;
        slotRoleItemCategories = newRoleItemCategories;

        maxSlots = newMax;
    }

    public void ClearAll()
    {
        for (int i = 0; i < slots.Length; i++)
            ForceRemoveItem(i);
    }

    public void RearrangeRoleSlots()
    {
        int primarySlot = -1;
        int subSlot = -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (!slotRoleItems[i]) continue;

            if (slotRoleItemCategories[i] == RoleItemCategory.PrimaryRole)
                primarySlot = i;
            else if (slotRoleItemCategories[i] == RoleItemCategory.SubRole)
                subSlot = i;
        }

        if (primarySlot >= 0 && primarySlot != 0)
            SwapSlots(primarySlot, 0);

        if (subSlot >= 0 && subSlot != 1)
        {
            if (slots[1] != null && slotRoleItems[1] && slotRoleItemCategories[1] == RoleItemCategory.PrimaryRole)
            {
                int temp = subSlot;
                if (temp == 0) temp = subSlot;
                SwapSlots(subSlot, 1);
            }
            else
            {
                SwapSlots(subSlot, 1);
            }
        }
    }

    private void SwapSlots(int a, int b)
    {
        if (a < 0 || a >= slots.Length || b < 0 || b >= slots.Length || a == b) return;

        (slots[a], slots[b]) = (slots[b], slots[a]);
        (slotIcons[a], slotIcons[b]) = (slotIcons[b], slotIcons[a]);
        (slotHeldPrefabs[a], slotHeldPrefabs[b]) = (slotHeldPrefabs[b], slotHeldPrefabs[a]);
        (slotDropPrefabs[a], slotDropPrefabs[b]) = (slotDropPrefabs[b], slotDropPrefabs[a]);
        (slotLocked[a], slotLocked[b]) = (slotLocked[b], slotLocked[a]);
        (slotRoleItems[a], slotRoleItems[b]) = (slotRoleItems[b], slotRoleItems[a]);
        (slotRoles[a], slotRoles[b]) = (slotRoles[b], slotRoles[a]);
        (slotRoleItemCategories[a], slotRoleItemCategories[b]) = (slotRoleItemCategories[b], slotRoleItemCategories[a]);

        OnSlotChanged?.Invoke(a, slots[a]);
        OnSlotIconChanged?.Invoke(a, slotIcons[a]);
        OnSlotLockChanged?.Invoke(a, slotLocked[a]);
        OnSlotRoleChanged?.Invoke(a, slotRoleItems[a], slotRoles[a]);

        OnSlotChanged?.Invoke(b, slots[b]);
        OnSlotIconChanged?.Invoke(b, slotIcons[b]);
        OnSlotLockChanged?.Invoke(b, slotLocked[b]);
        OnSlotRoleChanged?.Invoke(b, slotRoleItems[b], slotRoles[b]);
    }
}