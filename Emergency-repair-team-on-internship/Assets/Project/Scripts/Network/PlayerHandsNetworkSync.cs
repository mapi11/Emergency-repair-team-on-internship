using Unity.Netcode;
using UnityEngine;

public class PlayerHandsNetworkSync : NetworkBehaviour
{
    [Header("Hands")]
    [SerializeField] private Hand leftHand;
    [SerializeField] private Hand rightHand;

    [Header("Settings")]
    [SerializeField] private float sendInterval = 0.03f;

    private readonly NetworkVariable<Vector3> leftHandPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<Quaternion> leftHandRotation = new(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<Vector3> rightHandPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<Quaternion> rightHandRotation = new(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float sendTimer;

    private void Awake()
    {
        if (leftHand == null || rightHand == null)
        {
            Hand[] hands = GetComponentsInChildren<Hand>(true);

            if (hands.Length > 0 && leftHand == null)
            {
                leftHand = hands[0];
            }

            if (hands.Length > 1 && rightHand == null)
            {
                rightHand = hands[1];
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (leftHand != null)
            {
                leftHand.SetNetworkPoseMode(true);
            }

            if (rightHand != null)
            {
                rightHand.SetNetworkPoseMode(true);
            }
        }
        else
        {
            if (leftHand != null)
            {
                leftHand.SetNetworkPoseMode(false);
            }

            if (rightHand != null)
            {
                rightHand.SetNetworkPoseMode(false);
            }
        }
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
        {
            SendOwnerHands();
        }
        else
        {
            ApplyRemoteHands();
        }
    }

    private void SendOwnerHands()
    {
        sendTimer += Time.deltaTime;

        if (sendTimer < sendInterval)
            return;

        sendTimer = 0f;

        if (leftHand != null)
        {
            leftHandPosition.Value = leftHand.transform.position;
            leftHandRotation.Value = leftHand.transform.rotation;
        }

        if (rightHand != null)
        {
            rightHandPosition.Value = rightHand.transform.position;
            rightHandRotation.Value = rightHand.transform.rotation;
        }
    }

    private void ApplyRemoteHands()
    {
        if (leftHand != null)
        {
            leftHand.ApplyNetworkPose(
                leftHandPosition.Value,
                leftHandRotation.Value
            );
        }

        if (rightHand != null)
        {
            rightHand.ApplyNetworkPose(
                rightHandPosition.Value,
                rightHandRotation.Value
            );
        }
    }
}