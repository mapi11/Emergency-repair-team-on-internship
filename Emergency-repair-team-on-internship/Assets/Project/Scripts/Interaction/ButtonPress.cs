using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NetworkObject))]
public class ButtonPress : Interactable
{
    [Header("Button Visual")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private Renderer buttonRenderer;

    [SerializeField] private Vector3 normalLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 pressedLocalPosition = new Vector3(0f, -0.06f, 0f);

    [Header("Press")]
    [SerializeField] private bool resetOnRelease = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = Color.green;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    public bool IsPressed => networkIsPressed.Value;

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
        cancelOnCanInteractFail = false;

        if (handTarget == null)
        {
            var go = new GameObject("HandTarget");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            handTarget = go.transform;
        }

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

        Debug.Log($"🔘 Button spawned: {name}");
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

    public override void OnHandBegin(PlayerController player)
    {
        base.OnHandBegin(player);
        StartCoroutine(AutoRelease(player));
    }

    protected virtual IEnumerator AutoRelease(PlayerController player)
    {
        yield return new WaitForSeconds(0.5f);

        if (player != null)
            player.ReleaseCurrentInteractable();
    }

    protected override void OnServerInteractionBegin(ulong clientId)
    {
        if (networkIsPressed.Value)
            return;

        networkIsPressed.Value = true;

        onPressed?.Invoke();

        Debug.Log($"✅ Button {name}: pressed by Client {clientId}");
    }

    protected override void OnServerInteractionEnd(ulong clientId)
    {
        if (!resetOnRelease)
            return;

        if (!networkIsPressed.Value)
            return;

        networkIsPressed.Value = false;

        onReleased?.Invoke();

        Debug.Log($"✅ Button {name}: released by Client {clientId}");
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

        Debug.Log($"🔁 Button {name}: pressed state {oldValue} -> {newValue}");
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
