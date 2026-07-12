using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerVisualNetworkSync : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerController playerController;

    [Header("Sync Settings")]
    [SerializeField] private float sendInterval = 0.05f;

    private readonly NetworkVariable<bool> networkIsCrouching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<float> networkPitch = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float sendTimer;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
        {
            SendOwnerVisualState();
        }
        else
        {
            ApplyRemoteVisualState();
        }
    }

    private void SendOwnerVisualState()
    {
        if (playerController == null)
            return;

        sendTimer += Time.deltaTime;

        if (sendTimer < sendInterval)
            return;

        sendTimer = 0f;

        networkIsCrouching.Value = playerController.IsCrouching;
        networkPitch.Value = playerController.Pitch;
    }

    private void ApplyRemoteVisualState()
    {
        if (playerController == null)
            return;

        playerController.ApplyRemoteVisualState(
            networkIsCrouching.Value,
            networkPitch.Value
        );
    }
}