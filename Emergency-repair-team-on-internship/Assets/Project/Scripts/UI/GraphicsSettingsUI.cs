using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("Preset")]
    [SerializeField] private TMP_Dropdown presetDropdown;

    [Header("Texture Quality")]
    [SerializeField] private TMP_Dropdown textureQualityDropdown;

    [Header("Shadow")]
    [SerializeField] private TMP_Dropdown shadowQualityDropdown;
    [SerializeField] private Slider shadowDistanceSlider;
    [SerializeField] private TMP_Text shadowDistanceValueText;
    [SerializeField] private TMP_Dropdown shadowResolutionDropdown;

    [Header("Draw Distance")]
    [SerializeField] private Slider drawDistanceSlider;
    [SerializeField] private TMP_Text drawDistanceValueText;

    [Header("VSync")]
    [SerializeField] private Toggle vSyncToggle;

    [Header("Frame Rate")]
    [SerializeField] private Slider maxFpsSlider;
    [SerializeField] private TMP_Text maxFpsValueText;

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    [Header("Animation")]
    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private int savedTextureQuality;
    private int savedShadowQuality;
    private float savedShadowDistance;
    private int savedShadowResolution;
    private float savedDrawDistance;
    private bool savedVSync;
    private int savedMaxFps;
    private int savedPreset;
    private bool ignorePresetChange;

    private string Loc(string key) => LocalizationSettings.StringDatabase.GetLocalizedString("PlayerUI_Table", key);

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
        }

        transform.localScale = Vector3.one * 0.8f;

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        LoadSavedValues();

        ignorePresetChange = true;
        InitPreset();
        InitTextureQuality();
        InitShadowQuality();
        InitShadowDistance();
        InitShadowResolution();
        InitDrawDistance();
        InitVSync();
        InitMaxFps();
        ignorePresetChange = false;

        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    private void Start()
    {
        AnimateIn();
    }

    private void AnimateIn()
    {
        transform.DOScale(1f, animInDuration).SetEase(Ease.OutBack, 1.2f);

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(1f, animInDuration * 0.6f).OnComplete(() =>
            {
                if (canvasGroup != null)
                    canvasGroup.interactable = true;
            });
        }
    }

    private void AnimateOut(Action onComplete)
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

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        InitPreset();
        InitTextureQuality();
        InitShadowQuality();
        InitShadowResolution();

        if (maxFpsSlider != null)
            UpdateMaxFpsText(maxFpsSlider.value);
    }

    private void OnBack()
    {
        AnimateOut(null);
    }

    private void LoadSavedValues()
    {
        savedPreset = PlayerPrefs.GetInt("GraphicsPreset", -1);

        if (savedPreset < 0)
        {
            savedTextureQuality = 1;
            savedShadowQuality = 2;
            savedShadowDistance = 100f;
            savedShadowResolution = 2;
            savedDrawDistance = Camera.main != null ? Camera.main.farClipPlane : 500f;
            savedVSync = true;
            savedMaxFps = 144;
        }
        else
        {
            float defaultDrawDistance = Camera.main != null ? Camera.main.farClipPlane : 500f;

            savedTextureQuality = PlayerPrefs.GetInt("TextureQuality", 0);
            savedShadowQuality = PlayerPrefs.GetInt("ShadowQuality", 2);
            savedShadowDistance = PlayerPrefs.GetFloat("ShadowDistance", QualitySettings.shadowDistance > 0.01f ? QualitySettings.shadowDistance : 50f);
            savedShadowResolution = PlayerPrefs.GetInt("ShadowResolution", 2);
            savedDrawDistance = PlayerPrefs.GetFloat("DrawDistance", defaultDrawDistance);
            savedVSync = PlayerPrefs.GetInt("VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            savedMaxFps = PlayerPrefs.GetInt("MaxFps", Application.targetFrameRate >= 0 ? Application.targetFrameRate : 200);
        }
    }

    private void InitPreset()
    {
        if (presetDropdown == null) return;

        presetDropdown.onValueChanged.RemoveAllListeners();
        presetDropdown.ClearOptions();
        presetDropdown.AddOptions(new System.Collections.Generic.List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Ultra") });
        presetDropdown.SetValueWithoutNotify(savedPreset >= 0 ? savedPreset : 2);
        presetDropdown.onValueChanged.AddListener(OnPresetChanged);
    }

    private void ApplyPreset(int index)
    {
        ignorePresetChange = true;

        switch (index)
        {
            case 0: // Low
                SetTextureQuality(3);
                SetShadowQuality(0);
                SetShadowDistance(10f);
                SetShadowResolution(0);
                SetDrawDistance(200f);
                SetVSync(false);
                SetMaxFps(60);
                break;

            case 1: // Medium
                SetTextureQuality(2);
                SetShadowQuality(1);
                SetShadowDistance(40f);
                SetShadowResolution(1);
                SetDrawDistance(500f);
                SetVSync(true);
                SetMaxFps(60);
                break;

            case 2: // High
                SetTextureQuality(1);
                SetShadowQuality(2);
                SetShadowDistance(100f);
                SetShadowResolution(2);
                SetDrawDistance(1000f);
                SetVSync(true);
                SetMaxFps(144);
                break;

            case 3: // Ultra
                SetTextureQuality(0);
                SetShadowQuality(2);
                SetShadowDistance(150f);
                SetShadowResolution(3);
                SetDrawDistance(1500f);
                SetVSync(true);
                SetMaxFps(200);
                break;
        }

        savedPreset = index;
        PlayerPrefs.SetInt("GraphicsPreset", index);
        PlayerPrefs.Save();

        ignorePresetChange = false;
        presetDropdown.SetValueWithoutNotify(index);
    }

    private void OnPresetChanged(int index)
    {
        ApplyPreset(index);
    }

    private void InitTextureQuality()
    {
        if (textureQualityDropdown == null) return;

        textureQualityDropdown.onValueChanged.RemoveAllListeners();
        textureQualityDropdown.ClearOptions();
        textureQualityDropdown.AddOptions(new System.Collections.Generic.List<string> { Loc("Ultra"), Loc("High"), Loc("Medium"), Loc("Low") });
        textureQualityDropdown.SetValueWithoutNotify(Mathf.Clamp(savedTextureQuality, 0, 3));
        textureQualityDropdown.onValueChanged.AddListener(OnTextureQualityChanged);
    }

    private void OnTextureQualityChanged(int index)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetTextureQuality(index);
    }

    private void SetTextureQuality(int index)
    {
        // index 0=Ultra → mipmapLimit 0 (full res), index 3=Low → mipmapLimit 3 (most compressed)
        QualitySettings.globalTextureMipmapLimit = index;
        PlayerPrefs.SetInt("TextureQuality", index);
        PlayerPrefs.Save();

        if (textureQualityDropdown != null)
            textureQualityDropdown.SetValueWithoutNotify(index);
    }

    private void InitShadowQuality()
    {
        if (shadowQualityDropdown == null) return;

        shadowQualityDropdown.onValueChanged.RemoveAllListeners();
        shadowQualityDropdown.ClearOptions();
        shadowQualityDropdown.AddOptions(new System.Collections.Generic.List<string> { Loc("Disable"), Loc("Hard Only"), Loc("All") });
        shadowQualityDropdown.SetValueWithoutNotify(Mathf.Clamp(savedShadowQuality, 0, 2));
        shadowQualityDropdown.onValueChanged.AddListener(OnShadowQualityChanged);
    }

    private void OnShadowQualityChanged(int index)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetShadowQuality(index);
    }

    private void SetShadowQuality(int index)
    {
        QualitySettings.shadows = (UnityEngine.ShadowQuality)index;
        PlayerPrefs.SetInt("ShadowQuality", index);
        PlayerPrefs.Save();
    }

    private void InitShadowDistance()
    {
        if (shadowDistanceSlider == null) return;

        shadowDistanceSlider.minValue = 0f;
        shadowDistanceSlider.maxValue = 200f;
        shadowDistanceSlider.onValueChanged.AddListener(OnShadowDistanceChanged);
        SetShadowDistance(savedShadowDistance);
    }

    private void OnShadowDistanceChanged(float value)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetShadowDistance(value);
    }

    private void SetShadowDistance(float value)
    {
        QualitySettings.shadowDistance = value;
        PlayerPrefs.SetFloat("ShadowDistance", value);
        PlayerPrefs.Save();
        UpdateShadowDistanceText(value);

        if (shadowDistanceSlider != null)
            shadowDistanceSlider.SetValueWithoutNotify(value);
    }

    private void UpdateShadowDistanceText(float value)
    {
        if (shadowDistanceValueText != null)
            shadowDistanceValueText.text = value.ToString("F0");
    }

    private void InitShadowResolution()
    {
        if (shadowResolutionDropdown == null) return;

        shadowResolutionDropdown.onValueChanged.RemoveAllListeners();
        shadowResolutionDropdown.ClearOptions();
        shadowResolutionDropdown.AddOptions(new System.Collections.Generic.List<string> { Loc("Low"), Loc("Medium"), Loc("High"), Loc("Very High") });
        shadowResolutionDropdown.onValueChanged.AddListener(OnShadowResolutionChanged);
        SetShadowResolution(savedShadowResolution);
    }

    private void OnShadowResolutionChanged(int index)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetShadowResolution(index);
    }

    private void SetShadowResolution(int index)
    {
        int[] resolutions = { 256, 512, 1024, 2048 };
        int res = resolutions[Mathf.Clamp(index, 0, resolutions.Length - 1)];

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urp != null)
            urp.mainLightShadowmapResolution = res;

        QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)index;
        PlayerPrefs.SetInt("ShadowResolution", index);
        PlayerPrefs.Save();
    }

    private void InitDrawDistance()
    {
        if (drawDistanceSlider == null) return;

        drawDistanceSlider.minValue = 50f;
        drawDistanceSlider.maxValue = 2000f;
        drawDistanceSlider.onValueChanged.AddListener(OnDrawDistanceChanged);
        SetDrawDistance(savedDrawDistance);
    }

    private void OnDrawDistanceChanged(float value)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetDrawDistance(value);
    }

    private void SetDrawDistance(float value)
    {
        if (Camera.main != null)
            Camera.main.farClipPlane = value;

        PlayerPrefs.SetFloat("DrawDistance", value);
        PlayerPrefs.Save();
        UpdateDrawDistanceText(value);

        if (drawDistanceSlider != null)
            drawDistanceSlider.SetValueWithoutNotify(value);
    }

    private void UpdateDrawDistanceText(float value)
    {
        if (drawDistanceValueText != null)
            drawDistanceValueText.text = value.ToString("F0");
    }

    private void InitVSync()
    {
        if (vSyncToggle == null) return;

        vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        SetVSync(savedVSync);
    }

    private void OnVSyncChanged(bool enabled)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetVSync(enabled);
    }

    private void SetVSync(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt("VSync", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(enabled);
    }

    private void InitMaxFps()
    {
        if (maxFpsSlider == null) return;

        maxFpsSlider.minValue = 30;
        maxFpsSlider.maxValue = 200;
        maxFpsSlider.wholeNumbers = true;
        maxFpsSlider.onValueChanged.AddListener(OnMaxFpsChanged);
        SetMaxFps(savedMaxFps);
    }

    private void OnMaxFpsChanged(float value)
    {
        if (!ignorePresetChange)
            presetDropdown.SetValueWithoutNotify(-1);

        SetMaxFps(value);
    }

    private void SetMaxFps(float value)
    {
        int fps = Mathf.RoundToInt(value);
        Application.targetFrameRate = fps >= 200 ? -1 : fps;
        PlayerPrefs.SetInt("MaxFps", fps);
        PlayerPrefs.Save();
        UpdateMaxFpsText(fps);

        if (maxFpsSlider != null)
            maxFpsSlider.SetValueWithoutNotify(fps);
    }

    private void UpdateMaxFpsText(float value)
    {
        int fps = Mathf.RoundToInt(value);

        if (maxFpsValueText != null)
            maxFpsValueText.text = fps >= 200 ? Loc("Unlimited") : fps.ToString();
    }
}
