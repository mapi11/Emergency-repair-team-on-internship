using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NetworkObject))]
public class NetworkButton : Interactable
{
    [Header("Button Visual")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private Renderer buttonRenderer;

    [SerializeField] private Vector3 normalLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 pressedLocalPosition = new Vector3(0f, -0.06f, 0f);

    [Header("Press")]
    [SerializeField] private bool resetOnRelease = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color pressedColor = Color.green;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    [Header("Debug")]
    [SerializeField] private bool visualPressed;

    private readonly NetworkVariable<bool> networkIsPressed = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Material buttonMaterial;

    private void Awake()
    {
        if (buttonVisual == null)
        {
            buttonVisual = transform;
        }

        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponentInChildren<Renderer>();
        }

        if (buttonRenderer != null)
        {
            buttonMaterial = buttonRenderer.material;
        }

        normalLocalPosition = buttonVisual.localPosition;
    }

    public override void OnNetworkSpawn()
    {
        networkIsPressed.OnValueChanged += OnPressedStateChanged;

        visualPressed = networkIsPressed.Value;
        ApplyVisualInstant();

        Debug.Log($"🔘 NetworkButton spawned: {name}, IsServer={IsServer}, IsClient={IsClient}, IsSpawned={IsSpawned}");
    }

    public override void OnNetworkDespawn()
    {
        networkIsPressed.OnValueChanged -= OnPressedStateChanged;
    }

    private void Update()
    {
        UpdateVisual();
    }

    public override void OnHandBegin(PlayerController player)
    {
        Debug.Log($"🔘 NetworkButton {name}: OnHandBegin from {player.name}");

        if (!IsSpawned)
        {
            Debug.LogWarning($"⚠ NetworkButton {name}: object is not spawned. Check NetworkObject / Scene Management.");
            visualPressed = true;
            return;
        }

        RequestPressServerRpc();
    }

    public override void OnHandEnd(PlayerController player)
    {
        if (!resetOnRelease)
            return;

        Debug.Log($"🔘 NetworkButton {name}: OnHandEnd from {player.name}");

        if (!IsSpawned)
        {
            visualPressed = false;
            return;
        }

        RequestReleaseServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPressServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log($"🔘 NetworkButton {name}: PRESS RPC received on server from ClientId={rpcParams.Receive.SenderClientId}");

        if (networkIsPressed.Value)
            return;

        networkIsPressed.Value = true;

        onPressed?.Invoke();

        Debug.Log($"✅ NetworkButton {name}: pressed on server");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReleaseServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log($"🔘 NetworkButton {name}: RELEASE RPC received on server from ClientId={rpcParams.Receive.SenderClientId}");

        if (!networkIsPressed.Value)
            return;

        networkIsPressed.Value = false;

        onReleased?.Invoke();

        Debug.Log($"✅ NetworkButton {name}: released on server");
    }

    private void OnPressedStateChanged(bool oldValue, bool newValue)
    {
        visualPressed = newValue;

        Debug.Log($"🔁 NetworkButton {name}: synced pressed state {oldValue} -> {newValue}");
    }

    private void UpdateVisual()
    {
        if (buttonVisual == null)
            return;

        Vector3 targetPos = visualPressed
            ? pressedLocalPosition
            : normalLocalPosition;

        buttonVisual.localPosition = Vector3.Lerp(
            buttonVisual.localPosition,
            targetPos,
            Time.deltaTime * 18f
        );

        if (buttonMaterial != null)
        {
            Color targetColor = visualPressed ? pressedColor : normalColor;

            buttonMaterial.color = Color.Lerp(
                buttonMaterial.color,
                targetColor,
                Time.deltaTime * 14f
            );

            buttonMaterial.EnableKeyword("_EMISSION");
            buttonMaterial.SetColor("_EmissionColor", targetColor * 1.2f);
        }
    }

    private void ApplyVisualInstant()
    {
        if (buttonVisual != null)
        {
            buttonVisual.localPosition = visualPressed
                ? pressedLocalPosition
                : normalLocalPosition;
        }

        if (buttonMaterial != null)
        {
            Color targetColor = visualPressed ? pressedColor : normalColor;

            buttonMaterial.color = targetColor;
            buttonMaterial.EnableKeyword("_EMISSION");
            buttonMaterial.SetColor("_EmissionColor", targetColor * 1.2f);
        }
    }
}