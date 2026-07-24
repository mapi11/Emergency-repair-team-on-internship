using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ButtonLongPressed : ButtonPress
{
    [Header("Hold Settings")]
    [SerializeField] private float holdDuration = 0.1f;

    private float holdTimer;
    private bool holdCompleted;
    private PlayerController currentPlayer;

    protected override IEnumerator AutoRelease(PlayerController player)
    {
        yield break;
    }

    public override void OnHandBegin(PlayerController player)
    {
        if (!CanInteract(player))
        {
            if (cancelOnCanInteractFail && player != null)
                player.ReleaseCurrentInteractable();
            return;
        }

        holdTimer = 0f;
        holdCompleted = false;
        currentPlayer = player;

        visualPressed = false;
    }

    public override void OnHandHold(PlayerController player, float deltaTime)
    {
        if (holdCompleted)
            return;

        holdTimer += deltaTime;

        if (holdTimer >= holdDuration)
        {
            holdCompleted = true;
            currentPlayer = null;

            if (IsServer)
            {
                networkIsPressed.Value = true;
                onPressed?.Invoke();
            }
            else
            {
                SetPressedServerRpc();
            }

            visualPressed = true;
        }
    }

    public override void OnHandEnd(PlayerController player)
    {
        if (!holdCompleted)
        {
            holdTimer = 0f;
            currentPlayer = null;
            return;
        }

        holdCompleted = false;
        holdTimer = 0f;

        if (resetOnRelease)
        {
            if (IsServer)
            {
                networkIsPressed.Value = false;
                onReleased?.Invoke();
            }
            else
            {
                ResetPressedServerRpc();
            }

            visualPressed = false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPressedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (networkIsPressed.Value)
            return;

        networkIsPressed.Value = true;
        onPressed?.Invoke();

        Debug.Log($"✅ ButtonLongPressed {name}: pressed by Client {rpcParams.Receive.SenderClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetPressedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!resetOnRelease)
            return;

        if (!networkIsPressed.Value)
            return;

        networkIsPressed.Value = false;
        onReleased?.Invoke();

        Debug.Log($"✅ ButtonLongPressed {name}: released by Client {rpcParams.Receive.SenderClientId}");
    }
}
