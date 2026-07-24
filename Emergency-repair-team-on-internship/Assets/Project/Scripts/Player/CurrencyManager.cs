using Unity.Netcode;
using UnityEngine;

public class CurrencyManager : NetworkBehaviour
{
    private static CurrencyManager instance;

    private readonly NetworkVariable<int> networkCurrency = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int Currency => networkCurrency.Value;
    public event System.Action<int, int> OnCurrencyChanged;

    public static CurrencyManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<CurrencyManager>();
            return instance;
        }
        private set => instance = value;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public static bool IsReady { get; private set; }

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);

        networkCurrency.OnValueChanged += OnCurrencyValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkCurrency.OnValueChanged -= OnCurrencyValueChanged;

        if (instance == this)
            instance = null;
    }

    public void GrantStartingCurrency(int playerCount)
    {
        if (!IsServer) return;

        int amount = playerCount switch
        {
            1 => 500,
            2 => 750,
            _ => 750
        };

        networkCurrency.Value = amount;
        IsReady = true;
        EnableCurrencyUIClientRpc();
    }

    public void EnableCurrencyUIForClient(ulong clientId)
    {
        if (!IsServer) return;
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        EnableCurrencyUIClientRpc(rpcParams);
    }

    [ClientRpc]
    private void EnableCurrencyUIClientRpc(ClientRpcParams clientRpcParams = default)
    {
        IsReady = true;
        if (CurrencyUI.Instance != null)
            CurrencyUI.Instance.Initialize();
    }

    private void OnCurrencyValueChanged(int oldValue, int newValue)
    {
        OnCurrencyChanged?.Invoke(oldValue, newValue);
    }

    public void RequestAddCurrency(int amount)
    {
        if (!IsSpawned) return;
        if (IsServer)
            networkCurrency.Value += amount;
        else
            AddCurrencyServerRpc(amount);
    }

    public void RequestSpendCurrency(int amount)
    {
        if (!IsSpawned) return;
        if (IsServer)
            networkCurrency.Value = Mathf.Max(0, networkCurrency.Value - amount);
        else
            SpendCurrencyServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddCurrencyServerRpc(int amount)
    {
        networkCurrency.Value += amount;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpendCurrencyServerRpc(int amount)
    {
        networkCurrency.Value = Mathf.Max(0, networkCurrency.Value - amount);
    }

    public void RequestForceSpendCurrency(int amount)
    {
        if (!IsSpawned) return;
        if (IsServer)
            networkCurrency.Value -= amount;
        else
            ForceSpendCurrencyServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ForceSpendCurrencyServerRpc(int amount)
    {
        networkCurrency.Value -= amount;
    }
}
