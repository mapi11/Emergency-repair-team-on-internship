using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MultiButtonColorTest : NetworkBehaviour
{
    [Header("Buttons")]
    [SerializeField] private ButtonPress[] buttons;

    [Header("Cube Visual")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color twoButtonsColor = Color.yellow;
    [SerializeField] private Color threeButtonsColor = Color.green;

    [Header("Material")]
    [SerializeField] private string urpColorProperty = "_BaseColor";
    [SerializeField] private string standardColorProperty = "_Color";

    [Header("Debug")]
    [SerializeField] private int pressedButtonsDebug;
    [SerializeField] private int colorStateDebug;

    private Material targetMaterial;

    // 0 = default
    // 1 = yellow, when 2 buttons pressed
    // 2 = green, when 3+ buttons pressed
    private readonly NetworkVariable<int> networkColorState = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            targetMaterial = targetRenderer.material;
        }
    }

    public override void OnNetworkSpawn()
    {
        networkColorState.OnValueChanged += OnColorStateChanged;

        ApplyColorState(networkColorState.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkColorState.OnValueChanged -= OnColorStateChanged;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (!IsServer)
            return;

        UpdateColorStateOnServer();
    }

    private void UpdateColorStateOnServer()
    {
        int pressedCount = CountPressedButtons();

        pressedButtonsDebug = pressedCount;

        int newState = 0;

        if (pressedCount >= 3)
        {
            newState = 2;
        }
        else if (pressedCount >= 2)
        {
            newState = 1;
        }
        else
        {
            newState = 0;
        }

        colorStateDebug = newState;

        if (networkColorState.Value == newState)
            return;

        networkColorState.Value = newState;

        Debug.Log($"🧩 MultiButtonColorTest: pressed buttons = {pressedCount}, color state = {newState}");
    }

    private int CountPressedButtons()
    {
        int count = 0;

        if (buttons == null)
            return count;

        for (int i = 0; i < buttons.Length; i++)
        {
            ButtonPress button = buttons[i];

            if (button == null)
                continue;

            if (button.IsPressed)
            {
                count++;
            }
        }

        return count;
    }

    private void OnColorStateChanged(int oldValue, int newValue)
    {
        ApplyColorState(newValue);
    }

    private void ApplyColorState(int state)
    {
        Color targetColor = defaultColor;

        if (state == 1)
        {
            targetColor = twoButtonsColor;
        }
        else if (state == 2)
        {
            targetColor = threeButtonsColor;
        }

        ApplyColor(targetColor);
    }

    private void ApplyColor(Color color)
    {
        if (targetMaterial == null)
            return;

        if (targetMaterial.HasProperty(urpColorProperty))
        {
            targetMaterial.SetColor(urpColorProperty, color);
        }

        if (targetMaterial.HasProperty(standardColorProperty))
        {
            targetMaterial.SetColor(standardColorProperty, color);
        }
    }
}