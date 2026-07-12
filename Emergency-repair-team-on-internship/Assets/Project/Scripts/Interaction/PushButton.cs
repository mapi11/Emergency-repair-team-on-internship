using UnityEngine;
using UnityEngine.Events;

public class PushButton : Interactable
{
    [Header("Button Visual")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private Renderer buttonRenderer;

    [SerializeField] private Vector3 normalLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 pressedLocalPosition = new Vector3(0f, -0.06f, 0f);

    [Header("Press")]
    [SerializeField] private float pressTime = 0.12f;
    [SerializeField] private bool resetOnRelease = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color pressedColor = Color.green;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    [Header("Debug")]
    [SerializeField] private bool isHeldByHand;
    [SerializeField] private bool isPressed;
    [SerializeField] private float holdProgress;

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
        UpdateVisualInstant();
    }

    private void Update()
    {
        UpdateVisual();
    }

    public override void OnHandBegin(PlayerController player)
    {
        isHeldByHand = true;
        holdProgress = 0f;
    }

    public override void OnHandHold(PlayerController player, float deltaTime)
    {
        isHeldByHand = true;

        if (!isPressed)
        {
            holdProgress += deltaTime / Mathf.Max(pressTime, 0.001f);

            if (holdProgress >= 1f)
            {
                Press();
            }
        }
    }

    public override void OnHandEnd(PlayerController player)
    {
        isHeldByHand = false;
        holdProgress = 0f;

        if (resetOnRelease && isPressed)
        {
            Release();
        }
    }

    private void Press()
    {
        if (isPressed) return;

        isPressed = true;
        holdProgress = 1f;

        onPressed?.Invoke();

        Debug.Log($"🔘 PushButton {name}: pressed");
    }

    private void Release()
    {
        if (!isPressed) return;

        isPressed = false;
        holdProgress = 0f;

        onReleased?.Invoke();

        Debug.Log($"🔘 PushButton {name}: released");
    }

    private void UpdateVisual()
    {
        if (buttonVisual == null) return;

        float visualT = isPressed ? 1f : holdProgress;

        Vector3 targetPos = Vector3.Lerp(
            normalLocalPosition,
            pressedLocalPosition,
            visualT
        );

        buttonVisual.localPosition = Vector3.Lerp(
            buttonVisual.localPosition,
            targetPos,
            Time.deltaTime * 18f
        );

        if (buttonMaterial != null)
        {
            Color targetColor = isPressed ? pressedColor : normalColor;

            buttonMaterial.color = Color.Lerp(
                buttonMaterial.color,
                targetColor,
                Time.deltaTime * 14f
            );

            buttonMaterial.EnableKeyword("_EMISSION");
            buttonMaterial.SetColor("_EmissionColor", targetColor * 1.2f);
        }
    }

    private void UpdateVisualInstant()
    {
        if (buttonVisual != null)
        {
            buttonVisual.localPosition = isPressed ? pressedLocalPosition : normalLocalPosition;
        }

        if (buttonMaterial != null)
        {
            Color color = isPressed ? pressedColor : normalColor;
            buttonMaterial.color = color;
            buttonMaterial.EnableKeyword("_EMISSION");
            buttonMaterial.SetColor("_EmissionColor", color * 1.2f);
        }
    }
}