using UnityEngine;
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

    private InventorySlot[] slots;

    private void Awake()
    {
        if (inventory == null)
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsClient)
            {
                var localObj = Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localObj != null)
                    inventory = localObj.GetComponent<Inventory>();
            }

            if (inventory == null)
                inventory = FindFirstObjectByType<Inventory>();
        }
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

    private void Start()
    {
        Inventory localInv = FindLocalPlayerInventory();
        if (localInv != null)
            inventory = localInv;

        if (inventory == null || slotsParent == null)
            return;

        CreateSlots();
        inventory.OnSlotChanged += OnSlotChanged;
        inventory.OnActiveSlotChanged += OnActiveSlotChanged;
        inventory.OnSlotIconChanged += OnSlotIconChanged;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            OnSlotChanged(i, inventory.GetItemAtSlot(i));
            OnSlotIconChanged(i, inventory.GetSlotIcon(i));
        }

        OnActiveSlotChanged(inventory.ActiveSlot);
    }

    private void OnDestroy()
    {
        if (inventory == null)
            return;

        inventory.OnSlotChanged -= OnSlotChanged;
        inventory.OnActiveSlotChanged -= OnActiveSlotChanged;
        inventory.OnSlotIconChanged -= OnSlotIconChanged;
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
            slot.ItemNameTxt.text = string.IsNullOrEmpty(itemName)
                ? (slotIndex + 1).ToString()
                : itemName;
        }
    }

    private void OnSlotIconChanged(int slotIndex, Sprite icon)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null || slots[slotIndex].ObjectImg == null)
            return;

        slots[slotIndex].ObjectImg.sprite = icon;
        slots[slotIndex].ObjectImg.enabled = icon != null;
    }

    private void OnActiveSlotChanged(int slotIndex)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].ObjectImg == null)
                continue;

            if (i == slotIndex)
            {
                slots[i].ObjectImg.color = activeSlotColor;
            }
            else if (i < inventory.MaxSlots && inventory.GetItemAtSlot(i) == null)
            {
                slots[i].ObjectImg.color = emptySlotColor;
            }
            else
            {
                slots[i].ObjectImg.color = inactiveSlotColor;
            }
        }
    }
}
