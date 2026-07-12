using Unity.Netcode;
using UnityEngine;

public abstract class Interactable : NetworkBehaviour
{
    private const ulong NoClient = ulong.MaxValue;

    [Header("Interaction")]
    [SerializeField] private Transform handTarget;
    [SerializeField] private bool canInteract = true;

    [Header("Multiplayer Lock")]
    [SerializeField] private bool lockWhileInteracting = true;

    [Header("Debug")]
    [SerializeField] private bool isBusyDebug;
    [SerializeField] private ulong currentUserClientIdDebug = NoClient;

    private readonly NetworkVariable<ulong> currentUserClientId = new(
        NoClient,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public Transform HandTarget => handTarget != null ? handTarget : transform;

    public bool IsBusy => currentUserClientId.Value != NoClient;
    public ulong CurrentUserClientId => currentUserClientId.Value;

    public bool IsUsedByLocalClient
    {
        get
        {
            if (!IsSpawned) return false;
            if (NetworkManager.Singleton == null) return false;

            return currentUserClientId.Value == NetworkManager.Singleton.LocalClientId;
        }
    }

    public override void OnNetworkSpawn()
    {
        currentUserClientId.OnValueChanged += OnUserChanged;

        UpdateDebugState();

        Debug.Log($"🧲 Interactable spawned: {name}, IsSpawned={IsSpawned}");
    }

    public override void OnNetworkDespawn()
    {
        currentUserClientId.OnValueChanged -= OnUserChanged;
    }

    public virtual bool CanInteract(PlayerController player)
    {
        if (!canInteract)
            return false;

        if (!lockWhileInteracting)
            return true;

        if (!IsSpawned)
            return true;

        if (!IsBusy)
            return true;

        return IsUsedByLocalClient;
    }

    public virtual void OnHandBegin(PlayerController player)
    {
        if (!canInteract)
            return;

        if (!lockWhileInteracting)
        {
            OnLocalInteractionBegin(player);
            return;
        }

        if (!IsSpawned)
        {
            OnLocalInteractionBegin(player);
            return;
        }

        RequestBeginInteractionServerRpc();
    }

    public virtual void OnHandHold(PlayerController player, float deltaTime)
    {
    }

    public virtual void OnHandEnd(PlayerController player)
    {
        if (!lockWhileInteracting)
        {
            OnLocalInteractionEnd(player);
            return;
        }

        if (!IsSpawned)
        {
            OnLocalInteractionEnd(player);
            return;
        }

        RequestEndInteractionServerRpc();
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBeginInteractionServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!canInteract)
            return;

        if (currentUserClientId.Value != NoClient &&
            currentUserClientId.Value != senderClientId)
        {
            Debug.Log($"⛔ Interactable {name}: Client {senderClientId} tried to use, but already used by {currentUserClientId.Value}");
            return;
        }

        currentUserClientId.Value = senderClientId;

        OnServerInteractionBegin(senderClientId);

        Debug.Log($"🧲 Interactable {name}: locked by Client {senderClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestEndInteractionServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (currentUserClientId.Value != senderClientId)
        {
            Debug.Log($"⚠ Interactable {name}: Client {senderClientId} tried to release, but owner is {currentUserClientId.Value}");
            return;
        }

        OnServerInteractionEnd(senderClientId);

        currentUserClientId.Value = NoClient;

        Debug.Log($"🧲 Interactable {name}: released by Client {senderClientId}");
    }

    private void OnUserChanged(ulong oldValue, ulong newValue)
    {
        UpdateDebugState();

        if (newValue == NoClient)
        {
            OnInteractionUnlocked(oldValue);
        }
        else
        {
            OnInteractionLocked(newValue);
        }
    }

    private void UpdateDebugState()
    {
        isBusyDebug = IsBusy;
        currentUserClientIdDebug = currentUserClientId.Value;
    }

    protected virtual void OnServerInteractionBegin(ulong clientId)
    {
    }

    protected virtual void OnServerInteractionEnd(ulong clientId)
    {
    }

    protected virtual void OnInteractionLocked(ulong clientId)
    {
    }

    protected virtual void OnInteractionUnlocked(ulong previousClientId)
    {
    }

    protected virtual void OnLocalInteractionBegin(PlayerController player)
    {
    }

    protected virtual void OnLocalInteractionEnd(PlayerController player)
    {
    }
}