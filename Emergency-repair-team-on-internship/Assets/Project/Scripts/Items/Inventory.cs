using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private int maxSlots = 3;

    private string[] slots;
    private Sprite[] slotIcons;
    private GameObject[] slotHeldPrefabs;
    private GameObject[] slotDropPrefabs;
    private int activeSlot = -1;

    public int MaxSlots => maxSlots;
    public int ActiveSlot => activeSlot;
    public string ActiveItemType => activeSlot >= 0 && activeSlot < slots.Length ? slots[activeSlot] : null;

    public event System.Action<int, string> OnSlotChanged;
    public event System.Action<int> OnActiveSlotChanged;
    public event System.Action<int, Sprite> OnSlotIconChanged;

    private void Awake()
    {
        slots = new string[maxSlots];
        slotIcons = new Sprite[maxSlots];
        slotHeldPrefabs = new GameObject[maxSlots];
        slotDropPrefabs = new GameObject[maxSlots];
        activeSlot = -1;
    }

    public int AddItem(string itemName, Sprite icon = null, GameObject heldPrefab = null, GameObject dropPrefab = null)
    {
        if (string.IsNullOrEmpty(itemName))
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = itemName;
                slotIcons[i] = icon;
                slotHeldPrefabs[i] = heldPrefab;
                slotDropPrefabs[i] = dropPrefab;
                OnSlotChanged?.Invoke(i, itemName);
                OnSlotIconChanged?.Invoke(i, icon);

                if (activeSlot < 0)
                    SetActiveSlot(i);

                return i;
            }
        }

        return -1;
    }

    public void SetSlotIcon(int slot, Sprite icon)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slotIcons[slot] = icon;
        OnSlotIconChanged?.Invoke(slot, icon);
    }

    public Sprite GetSlotIcon(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return null;

        return slotIcons[slot];
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

    public void RemoveItem(int slot)
    {
        if (slot < 0 || slot >= slots.Length)
            return;

        slots[slot] = null;
        slotIcons[slot] = null;
        slotHeldPrefabs[slot] = null;
        slotDropPrefabs[slot] = null;
        OnSlotChanged?.Invoke(slot, null);
        OnSlotIconChanged?.Invoke(slot, null);

        if (activeSlot == slot)
        {
            int newSlot = FindNextFilledSlot();

            if (newSlot >= 0)
                SetActiveSlot(newSlot);
            else
                ClearActiveSlot();
        }
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

        for (int i = 0; i < slots.Length; i++)
        {
            newSlots[i] = slots[i];
            newIcons[i] = slotIcons[i];
            newHeldPrefabs[i] = slotHeldPrefabs[i];
            newDropPrefabs[i] = slotDropPrefabs[i];
        }

        slots = newSlots;
        slotIcons = newIcons;
        slotHeldPrefabs = newHeldPrefabs;
        slotDropPrefabs = newDropPrefabs;
        maxSlots = newMax;
    }
}
