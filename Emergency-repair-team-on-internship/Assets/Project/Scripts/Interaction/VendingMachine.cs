using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class VendingMachine : NetworkBehaviour
{
    [Header("Pricing")]
    [SerializeField] private int cost = 50;
    [SerializeField] private int maxUses = 3;
    [SerializeField] private bool lendOut;

    [Header("Item")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform visualItem;

    [Header("UI")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text remainingText;

    [Header("Button")]
    [SerializeField] private ButtonPress buyButton;

    private readonly NetworkVariable<int> networkRemainingUses = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool CanBuy
    {
        get
        {
            if (networkRemainingUses.Value <= 0)
                return false;

            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.IsSpawned)
                return false;

            if (!lendOut && CurrencyManager.Instance.Currency < cost)
                return false;

            return true;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            networkRemainingUses.Value = maxUses;

        networkRemainingUses.OnValueChanged += OnRemainingChanged;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;

        if (buyButton != null)
        {
            buyButton.onPressed.AddListener(Buy);
            buyButton.SetCanInteract(CanBuy);
        }

        SpawnVisualItem();
        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        networkRemainingUses.OnValueChanged -= OnRemainingChanged;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;

        if (buyButton != null)
            buyButton.onPressed.RemoveListener(Buy);
    }

    private void Buy()
    {
        if (!IsServer) return;

        if (networkRemainingUses.Value <= 0) return;

        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.IsSpawned) return;

        int current = CurrencyManager.Instance.Currency;
        if (!lendOut && current < cost) return;

        if (lendOut)
            CurrencyManager.Instance.RequestForceSpendCurrency(cost);
        else
            CurrencyManager.Instance.RequestSpendCurrency(cost);

        networkRemainingUses.Value--;

        SpawnItem();
    }

    private void SpawnVisualItem()
    {
        if (itemPrefab == null || visualItem == null) return;

        GameObject display = Instantiate(itemPrefab, visualItem.position, visualItem.rotation, visualItem);

        foreach (var rb in display.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;

        foreach (var col in display.GetComponentsInChildren<Collider>())
            Destroy(col);

        foreach (var rb in display.GetComponentsInChildren<Rigidbody>())
            Destroy(rb);

        var netObj = display.GetComponent<NetworkObject>();
        if (netObj != null)
            Destroy(netObj);

        var behaviours = display.GetComponentsInChildren<NetworkBehaviour>();
        for (int i = behaviours.Length - 1; i >= 0; i--)
            Destroy(behaviours[i]);

        display.transform.localPosition = Vector3.zero;
        display.transform.localRotation = Quaternion.identity;
    }

    private void SpawnItem()
    {
        if (itemPrefab == null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject item = Instantiate(itemPrefab, pos, rot);
        NetworkObject netObj = item.GetComponent<NetworkObject>();

        if (netObj != null)
            netObj.Spawn(true);
    }

    private void OnRemainingChanged(int oldValue, int newValue)
    {
        UpdateUI();
        UpdateButtonState();
    }

    private void OnCurrencyChanged(int oldValue, int newValue)
    {
        UpdateUI();
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (buyButton == null) return;
        buyButton.SetCanInteract(CanBuy);
    }

    private void UpdateUI()
    {
        if (costText != null)
            costText.text = $"${cost}";

        if (remainingText != null)
            remainingText.text = networkRemainingUses.Value.ToString();
    }
}
