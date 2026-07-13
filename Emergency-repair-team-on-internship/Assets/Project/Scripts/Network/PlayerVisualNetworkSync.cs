using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerVisualNetworkSync : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerController playerController;

    [Header("Send Settings")]
    [SerializeField] private float sendInterval = 0.02f;
    [SerializeField] private float minPitchDifferenceToSend = 0.15f;

    [Header("Remote Smooth")]
    [SerializeField] private float remotePitchSmooth = 18f;

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
    private float lastSentPitch;
    private bool lastSentCrouching;

    private float smoothedRemotePitch;
    private bool hasRemotePitch;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }
    }

    public override void OnNetworkSpawn()
    {
        lastSentPitch = networkPitch.Value;
        lastSentCrouching = networkIsCrouching.Value;

        smoothedRemotePitch = networkPitch.Value;
        hasRemotePitch = true;
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

        bool currentCrouching = playerController.IsCrouching;
        float currentPitch = playerController.Pitch;

        bool crouchChanged = currentCrouching != lastSentCrouching;
        bool pitchChanged = Mathf.Abs(Mathf.DeltaAngle(lastSentPitch, currentPitch)) >= minPitchDifferenceToSend;

        if (!crouchChanged && !pitchChanged)
            return;

        networkIsCrouching.Value = currentCrouching;
        networkPitch.Value = currentPitch;

        lastSentCrouching = currentCrouching;
        lastSentPitch = currentPitch;
    }

    private void ApplyRemoteVisualState()
    {
        if (playerController == null)
            return;

        float targetPitch = networkPitch.Value;

        if (!hasRemotePitch)
        {
            smoothedRemotePitch = targetPitch;
            hasRemotePitch = true;
        }

        smoothedRemotePitch = Mathf.LerpAngle(
            smoothedRemotePitch,
            targetPitch,
            Time.deltaTime * remotePitchSmooth
        );

        playerController.ApplyRemoteVisualState(
            networkIsCrouching.Value,
            smoothedRemotePitch
        );
    }
}