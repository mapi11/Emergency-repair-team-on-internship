using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerName : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform nameplateRoot;
    [SerializeField] private TMP_Text nameText;

    [Header("Visibility")]
    [SerializeField] private bool hideForLocalPlayer = true;
    [SerializeField] private float maxVisibleDistance = 30f;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool keepUpright = true;

    [Header("Debug")]
    [SerializeField] private string currentName;

    private readonly NetworkVariable<FixedString32Bytes> networkPlayerName = new(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Camera cachedCamera;

    public string CurrentName => networkPlayerName.Value.ToString();

    private void Awake()
    {
        if (nameplateRoot == null && nameText != null)
        {
            nameplateRoot = nameText.transform;
        }

        if (nameText != null)
        {
            nameText.text = "";
        }
    }

    public override void OnNetworkSpawn()
    {
        networkPlayerName.OnValueChanged += OnNameChanged;

        ApplyName(networkPlayerName.Value.ToString());

        if (IsOwner)
        {
            string requestedName = LocalPlayerSettings.PlayerName;

            if (string.IsNullOrWhiteSpace(requestedName))
            {
                requestedName = $"Player {OwnerClientId}";
            }

            requestedName = SanitizeName(requestedName);

            FixedString32Bytes fixedName = new FixedString32Bytes(requestedName);

            if (IsServer)
            {
                SetNameOnServer(fixedName, OwnerClientId);
            }
            else
            {
                RequestSetNameServerRpc(fixedName);
            }
        }

        UpdateVisibility();
    }

    public override void OnNetworkDespawn()
    {
        networkPlayerName.OnValueChanged -= OnNameChanged;
    }

    private void LateUpdate()
    {
        UpdateVisibility();

        if (faceCamera)
        {
            FaceCamera();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetNameServerRpc(FixedString32Bytes requestedName, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        SetNameOnServer(requestedName, senderClientId);
    }

    private void SetNameOnServer(FixedString32Bytes requestedName, ulong senderClientId)
    {
        if (senderClientId != OwnerClientId)
        {
            Debug.LogWarning($"⚠ Client {senderClientId} tried to set name for player owned by {OwnerClientId}");
            return;
        }

        string cleanedName = SanitizeName(requestedName.ToString());

        if (string.IsNullOrWhiteSpace(cleanedName))
        {
            cleanedName = $"Player {OwnerClientId}";
        }

        networkPlayerName.Value = new FixedString32Bytes(cleanedName);

        Debug.Log($"🏷 Player {OwnerClientId} name set to: {cleanedName}");
    }

    private void OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        ApplyName(newName.ToString());
    }

    private void ApplyName(string newName)
    {
        currentName = newName;

        if (nameText != null)
        {
            nameText.text = newName;
        }
    }

    private string SanitizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "Player";
        }

        string result = rawName.Trim();

        if (result.Length > 20)
        {
            result = result.Substring(0, 20);
        }

        return result;
    }

    private void UpdateVisibility()
    {
        if (nameplateRoot == null)
            return;

        if (hideForLocalPlayer && IsOwner)
        {
            nameplateRoot.gameObject.SetActive(false);
            return;
        }

        Camera cam = GetCamera();

        if (cam == null)
        {
            nameplateRoot.gameObject.SetActive(true);
            return;
        }

        float distance = Vector3.Distance(cam.transform.position, nameplateRoot.position);
        bool visible = distance <= maxVisibleDistance;

        nameplateRoot.gameObject.SetActive(visible);
    }

    private void FaceCamera()
    {
        if (nameplateRoot == null)
            return;

        Camera cam = GetCamera();

        if (cam == null)
            return;

        Vector3 direction = nameplateRoot.position - cam.transform.position;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (keepUpright)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.001f)
            return;

        nameplateRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Camera GetCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        cachedCamera = Camera.main;

        if (cachedCamera != null)
        {
            return cachedCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                cachedCamera = cameras[i];
                return cachedCamera;
            }
        }

        return null;
    }
}