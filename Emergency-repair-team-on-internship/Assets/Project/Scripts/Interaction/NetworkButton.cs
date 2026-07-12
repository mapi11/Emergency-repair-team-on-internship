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
        base.OnNetworkSpawn();

        networkIsPressed.OnValueChanged += OnPressedStateChanged;

        visualPressed = networkIsPressed.Value;
        ApplyVisualInstant();

        Debug.Log($"🔘 NetworkButton spawned: {name}");
    }

    public override void OnNetworkDespawn()
    {
        networkIsPressed.OnValueChanged -= OnPressedStateChanged;

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        UpdateVisual();
    }

    protected override void OnServerInteractionBegin(ulong clientId)
    {
        if (networkIsPressed.Value)
            return;

        networkIsPressed.Value = true;

        onPressed?.Invoke();

        Debug.Log($"✅ NetworkButton {name}: pressed by Client {clientId}");
    }

    protected override void OnServerInteractionEnd(ulong clientId)
    {
        if (!resetOnRelease)
            return;

        if (!networkIsPressed.Value)
            return;

        networkIsPressed.Value = false;

        onReleased?.Invoke();

        Debug.Log($"✅ NetworkButton {name}: released by Client {clientId}");
    }

    protected override void OnLocalInteractionBegin(PlayerController player)
    {
        visualPressed = true;
        onPressed?.Invoke();
    }

    protected override void OnLocalInteractionEnd(PlayerController player)
    {
        if (!resetOnRelease)
            return;

        visualPressed = false;
        onReleased?.Invoke();
    }

    private void OnPressedStateChanged(bool oldValue, bool newValue)
    {
        visualPressed = newValue;

        Debug.Log($"🔁 NetworkButton {name}: pressed state {oldValue} -> {newValue}");
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