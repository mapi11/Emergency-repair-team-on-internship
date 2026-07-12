using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (audioListener == null && playerCamera != null)
        {
            audioListener = playerCamera.GetComponent<AudioListener>();
        }

        SetLocalState(false);
    }

    public override void OnNetworkSpawn()
    {
        bool local = IsOwner;

        SetLocalState(local);

        Debug.Log($"👤 NetworkPlayer spawned. Name = {name}, IsOwner = {IsOwner}, OwnerClientId = {OwnerClientId}");
    }

    private void SetLocalState(bool local)
    {
        if (playerController != null)
        {
            playerController.SetLocalPlayer(local);
            playerController.enabled = true;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = local;
        }

        if (audioListener != null)
        {
            audioListener.enabled = local;
        }
    }
}