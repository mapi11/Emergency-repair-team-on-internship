using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerColor : NetworkBehaviour
{
    [Header("Renderers To Paint")]
    [Tooltip("Сюда назначь Body, Hands, можно Head. Глаза лучше не красить.")]
    [SerializeField] private Renderer[] colorRenderers;

    [Header("Material")]
    [SerializeField] private string urpColorProperty = "_BaseColor";
    [SerializeField] private string standardColorProperty = "_Color";

    [Header("Debug")]
    [SerializeField] private Color32 currentColor = Color.white;

    private readonly NetworkVariable<int> networkPackedColor = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public Color32 CurrentColor => currentColor;

    private void Awake()
    {
        if (colorRenderers == null || colorRenderers.Length == 0)
        {
            colorRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    public override void OnNetworkSpawn()
    {
        networkPackedColor.OnValueChanged += OnColorChanged;

        if (IsOwner)
        {
            LocalPlayerSettings.Load();

            int packedColor = LocalPlayerSettings.PackColor(LocalPlayerSettings.PlayerColor);

            if (IsServer)
            {
                SetColorOnServer(packedColor, OwnerClientId);
            }
            else
            {
                RequestSetColorServerRpc(packedColor);
            }
        }

        if (networkPackedColor.Value != 0)
        {
            ApplyPackedColor(networkPackedColor.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkPackedColor.OnValueChanged -= OnColorChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetColorServerRpc(int requestedPackedColor, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        SetColorOnServer(requestedPackedColor, senderClientId);
    }

    private void SetColorOnServer(int requestedPackedColor, ulong senderClientId)
    {
        if (senderClientId != OwnerClientId)
        {
            Debug.LogWarning($"⚠ Client {senderClientId} tried to set color for player owned by {OwnerClientId}");
            return;
        }

        Color32 requestedColor = LocalPlayerSettings.UnpackColor(requestedPackedColor);
        requestedColor.a = 255;

        int validatedColor = LocalPlayerSettings.PackColor(requestedColor);

        networkPackedColor.Value = validatedColor;

        Debug.Log($"🎨 Player {OwnerClientId} color set to R{requestedColor.r} G{requestedColor.g} B{requestedColor.b}");
    }

    private void OnColorChanged(int oldValue, int newValue)
    {
        ApplyPackedColor(newValue);
    }

    private void ApplyPackedColor(int packedColor)
    {
        if (packedColor == 0)
            return;

        currentColor = LocalPlayerSettings.UnpackColor(packedColor);
        ApplyColor(currentColor);
    }

    private void ApplyColor(Color32 color)
    {
        if (colorRenderers == null)
            return;

        for (int i = 0; i < colorRenderers.Length; i++)
        {
            Renderer renderer = colorRenderers[i];

            if (renderer == null)
                continue;

            Material material = renderer.material;

            if (material == null)
                continue;

            if (material.HasProperty(urpColorProperty))
            {
                material.SetColor(urpColorProperty, color);
            }

            if (material.HasProperty(standardColorProperty))
            {
                material.SetColor(standardColorProperty, color);
            }
        }
    }
}