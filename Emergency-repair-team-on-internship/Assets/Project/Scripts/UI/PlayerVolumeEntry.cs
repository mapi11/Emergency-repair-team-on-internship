using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerVolumeEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image playerColorImage;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;

    public ulong ClientId { get; private set; }

    public void Setup(ulong clientId, string playerName, Color32 color, float initialVolume)
    {
        ClientId = clientId;

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (playerColorImage != null)
            playerColorImage.color = color;

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = initialVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        UpdateVolumeText(initialVolume);
    }

    private void OnVolumeChanged(float value)
    {
        UpdateVolumeText(value);
        PausePanelUI.ApplyVolume(ClientId, value);
    }

    private void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }
}
