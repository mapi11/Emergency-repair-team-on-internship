using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SettingsPanelUI : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;

    [Header("Mouse")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText;

    [Header("Graphics")]
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Microphone")]
    [SerializeField] private TMP_Dropdown micDropdown;

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    [Header("Animation")]
    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private PlayerController playerController;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        playerController = GetLocalPlayerController();

        InitVolume();
        InitSensitivity();
        InitQuality();
        InitMicDropdown();

        if (backButton != null)
            backButton.onClick.AddListener(() => PauseMenu.Instance.CloseSettings());
    }

    private void Start()
    {
        AnimateIn();
    }

    public void AnimateIn()
    {
        transform.localScale = Vector3.one * 0.8f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        transform.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f);

        if (canvasGroup != null)
            canvasGroup.DOFade(1f, animInDuration * 0.6f);
    }

    public void AnimateOut(Action onComplete)
    {
        transform.DOScale(0.8f, animOutDuration).SetEase(Ease.InBack);

        if (canvasGroup != null)
            canvasGroup.DOFade(0f, animOutDuration * 0.6f);

        DOVirtual.DelayedCall(animOutDuration, () =>
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        });
    }

    private void InitVolume()
    {
        if (volumeSlider == null)
            return;

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = AudioListener.volume;
        UpdateVolumeText(AudioListener.volume);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Volume", value);
        PlayerPrefs.Save();
        UpdateVolumeText(value);
    }

    private void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }

    private void InitSensitivity()
    {
        if (sensitivitySlider == null)
            return;

        sensitivitySlider.minValue = 0.01f;
        sensitivitySlider.maxValue = 0.3f;

        float saved = PlayerPrefs.GetFloat("MouseSensitivity", -1f);

        if (saved >= 0f)
        {
            sensitivitySlider.value = saved;
            ApplySensitivity(saved);
        }
        else if (playerController != null)
        {
            sensitivitySlider.value = playerController.mouseSensitivity;
        }

        UpdateSensitivityText(sensitivitySlider.value);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    private void OnSensitivityChanged(float value)
    {
        ApplySensitivity(value);
        UpdateSensitivityText(value);
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();
    }

    private void ApplySensitivity(float value)
    {
        if (playerController != null)
            playerController.mouseSensitivity = value;
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = (value * 100f).ToString("F0");
    }

    private void InitQuality()
    {
        if (qualityDropdown == null)
            return;

        qualityDropdown.ClearOptions();

        string[] names = QualitySettings.names;
        var options = new List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < names.Length; i++)
            options.Add(new TMP_Dropdown.OptionData(names[i]));

        qualityDropdown.AddOptions(options);

        int savedQuality = PlayerPrefs.GetInt("QualityLevel", -1);
        int qualityIndex = savedQuality >= 0 ? savedQuality : QualitySettings.GetQualityLevel();
        qualityIndex = Mathf.Clamp(qualityIndex, 0, names.Length - 1);

        qualityDropdown.SetValueWithoutNotify(qualityIndex);
        QualitySettings.SetQualityLevel(qualityIndex, true);
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
    }

    private void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt("QualityLevel", index);
        PlayerPrefs.Save();
    }

    private void InitMicDropdown()
    {
        if (micDropdown == null)
            return;

        micDropdown.ClearOptions();

        string[] devices = Microphone.devices;
        var options = new List<TMP_Dropdown.OptionData>();

        if (devices.Length == 0)
        {
            options.Add(new TMP_Dropdown.OptionData("No Mic"));
        }
        else
        {
            for (int i = 0; i < devices.Length; i++)
                options.Add(new TMP_Dropdown.OptionData(devices[i]));
        }

        micDropdown.AddOptions(options);

        string saved = VoiceChatSettings.GetSelectedOrDefaultMicrophoneName();
        int savedIndex = 0;

        if (!string.IsNullOrEmpty(saved))
        {
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i] == saved)
                {
                    savedIndex = i;
                    break;
                }
            }
        }

        micDropdown.SetValueWithoutNotify(Mathf.Clamp(savedIndex, 0, options.Count - 1));
        micDropdown.onValueChanged.AddListener(OnMicChanged);
    }

    private void OnMicChanged(int index)
    {
        string[] devices = Microphone.devices;

        if (index >= 0 && index < devices.Length)
        {
            VoiceChatSettings.SetSelectedMicrophone(devices[index]);

            if (ProximityVoiceManager.Instance != null)
                ProximityVoiceManager.Instance.RestartMicrophone();
        }
    }

    private static PlayerController GetLocalPlayerController()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localClient = NetworkManager.Singleton.LocalClient;

            if (localClient != null && localClient.PlayerObject != null)
            {
                var pc = localClient.PlayerObject.GetComponentInChildren<PlayerController>();

                if (pc != null)
                    return pc;
            }
        }

        var all = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].IsLocalPlayer)
                return all[i];
        }

        return null;
    }
}
