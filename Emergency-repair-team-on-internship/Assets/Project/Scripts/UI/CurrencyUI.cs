using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private Button addButton;
    [SerializeField] private Button subtractButton;
    [SerializeField] private int testAmount = 100;

    private CurrencyManager currencyManager;

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            gameObject.SetActive(false);
            return;
        }

        FindCurrencyManager();

        if (addButton != null)
            addButton.onClick.AddListener(OnAddClicked);

        if (subtractButton != null)
            subtractButton.onClick.AddListener(OnSubtractClicked);
    }

    private void OnDestroy()
    {
        if (currencyManager != null)
            currencyManager.OnCurrencyChanged -= OnCurrencyChanged;
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
