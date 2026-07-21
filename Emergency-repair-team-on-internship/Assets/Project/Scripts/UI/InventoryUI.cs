using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private InventorySlot slotPrefab;

    [Header("Colors")]
    [SerializeField] private Color activeSlotColor = Color.white;
    [SerializeField] private Color inactiveSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
    [SerializeField] private Color lockedSlotColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    private InventorySlot[] slots;

    private void Awake()
    {
        if (inventory == null)
        {
            Inventory localInv = FindLocalPlayerInventory();

            if (localInv != null)
                inventory = localInv;
            else
                inventory = FindFirstObjectByType<Inventory>();
        }
    }

    private void Start()
    {
        var netObj = GetComponentInParent<NetworkObject>();
        if (netObj != null && !netObj.IsOwner)
        {
            gameObject.SetActive(false);
            return;
        }

        Inventory localInv = FindLocalPlayerInventory();
        if (localInv != null)
            inventory = localInv;
    }

    public void Initialize(Inventory newInventory)
    {
        if (newInventory == null)
            return;

        if (inventory != null && inventory != newInventory)
        {
            inventory.OnSlotChanged -= OnSlotChanged;
            inventory.OnActiveSlotChanged -= OnActiveSlotChanged;
            inventory.OnSlotIconChanged -= OnSlotIconChanged;
            inventory.OnSlotLockChanged -= OnSlotLockChanged;
        }

        inventory = newInventory;

        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            slots = null;
        }

        if (slotsParent == null)
            return;

        CreateSlots();

        inventory.OnSlotChanged += OnSlotChanged;
        inventory.OnActiveSlotChanged += OnActiveSlotChanged;
        inventory.OnSlotIconChanged += OnSlotIconChanged;
        inventory.OnSlotLockChanged += OnSlotLockChanged;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            OnSlotChanged(i, inventory.GetItemAtSlot(i));
            OnSlotIconChanged(i, inventory.GetSlotIcon(i));
            OnSlotLockChanged(i, inventory.IsSlotLocked(i));
        }

        OnActiveSlotChanged(inventory.ActiveSlot);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (inventory == null)
            return;

        inventory.OnSlotChanged -= OnSlotChanged;
        inventory.OnActiveSlotChanged -= OnActiveSlotChanged;
        inventory.OnSlotIconChanged -= OnSlotIconChanged;
        inventory.OnSlotLockChanged -= OnSlotLockChanged;
    }

    private Inventory FindLocalPlayerInventory()
    {
        var net = Unity.Netcode.NetworkManager.Singleton;

        if (net != null && net.IsClient)
        {
            var localObj = net.LocalClient?.PlayerObject;

            if (localObj != null)
                return localObj.GetComponent<Inventory>();
        }

        return null;
    }

    private void CreateSlots()
    {
        slots = new InventorySlot[inventory.MaxSlots];

        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot;

            if (slotPrefab != null)
            {
                slot = Instantiate(slotPrefab, slotsParent);
            }
            else
            {
                GameObject obj = new GameObject($"Slot_{i}", typeof(RectTransform));
                obj.AddComponent<Image>();
                slot = obj.AddComponent<InventorySlot>();
                slot.transform.SetParent(slotsParent, false);
            }

            slot.gameObject.name = $"Slot_{i}";

            if (slot.ItemNameTxt != null)
                slot.ItemNameTxt.text = (i + 1).ToString();

            if (slot.LockImg != null)
                slot.LockImg.enabled = false;

            slots[i] = slot;
        }
    }

    private void OnSlotChanged(int slotIndex, string itemName)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        InventorySlot slot = slots[slotIndex];

        if (slot == null)
            return;

        if (slot.ItemNameTxt != null)
        {
            string displayName;

            if (string.IsNullOrEmpty(itemName))
            {
                displayName = (slotIndex + 1).ToString();
            }
            else if (PickableItem.LocalizationKeys.TryGetValue(itemName, out string key))
            {
                displayName = LocalizationSettings.StringDatabase.GetLocalizedString("Items_Table", key);
            }
            else
            {
                displayName = itemName;
            }

            slot.ItemNameTxt.text = displayName;
        }

        RefreshSlotColor(slotIndex);
    }

    private void OnSlotIconChanged(int slotIndex, Sprite icon)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        InventorySlot slot = slots[slotIndex];

        if (slot == null || slot.ObjectImg == null)
            return;

        slot.ObjectImg.sprite = icon;
        slot.ObjectImg.enabled = icon != null;

        RefreshSlotColor(slotIndex);
    }

    private void OnSlotLockChanged(int slotIndex, bool locked)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        InventorySlot slot = slots[slotIndex];

        if (slot == null)
            return;

        if (slot.LockImg != null)
            slot.LockImg.enabled = locked;

        RefreshSlotColor(slotIndex);
    }

    private void OnActiveSlotChanged(int slotIndex)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            RefreshSlotColor(i);
        }
    }

    private void RefreshSlotColor(int slotIndex)
    {
        if (inventory == null)
            return;

        if (slots == null)
            return;

        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        InventorySlot slot = slots[slotIndex];

        if (slot == null || slot.ObjectImg == null)
            return;

        if (slotIndex == inventory.ActiveSlot)
        {
            slot.ObjectImg.color = activeSlotColor;
            return;
        }

        if (inventory.IsSlotLocked(slotIndex))
        {
            slot.ObjectImg.color = lockedSlotColor;
            return;
        }

        if (inventory.GetItemAtSlot(slotIndex) == null)
        {
            slot.ObjectImg.color = emptySlotColor;
            return;
        }

        slot.ObjectImg.color = inactiveSlotColor;
    }
}