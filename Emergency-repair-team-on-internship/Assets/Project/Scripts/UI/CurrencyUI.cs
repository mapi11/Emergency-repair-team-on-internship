using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private Button addButton;
    [SerializeField] private Button subtractButton;
    [SerializeField] private GameObject container;
    [SerializeField] private int testAmount = 100;

    public static CurrencyUI Instance { get; private set; }

    private CurrencyManager currencyManager;

    private void Awake()
    {
        Instance = this;
        container?.SetActive(false);
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            container?.SetActive(false);
            return;
        }

        if (CurrencyManager.IsReady)
            Initialize();
    }

    private void OnDestroy()
    {
        if (currencyManager != null)
            currencyManager.OnCurrencyChanged -= OnCurrencyChanged;

        if (Instance == this)
            Instance = null;
    }

    public void Initialize()
    {
        if (container != null)
            container.SetActive(true);

        if (addButton != null)
            addButton.onClick.AddListener(OnAddClicked);

        if (subtractButton != null)
            subtractButton.onClick.AddListener(OnSubtractClicked);

        if (currencyManager != null)
            return;

        FindCurrencyManager();
    }

    private void FindCurrencyManager()
    {
        currencyManager = CurrencyManager.Instance;

        if (currencyManager != null)
        {
            currencyManager.OnCurrencyChanged += OnCurrencyChanged;
            UpdateDisplay(currencyManager.Currency);
        }
    }

    private void OnCurrencyChanged(int oldValue, int newValue)
    {
        UpdateDisplay(newValue);
    }

    private void UpdateDisplay(int amount)
    {
        if (currencyText != null)
            currencyText.text = $"$ {amount}";
    }

    private void OnAddClicked()
    {
        if (currencyManager != null)
            currencyManager.RequestAddCurrency(testAmount);
    }

    private void OnSubtractClicked()
    {
        if (currencyManager != null)
            currencyManager.RequestSpendCurrency(testAmount);
    }
}
