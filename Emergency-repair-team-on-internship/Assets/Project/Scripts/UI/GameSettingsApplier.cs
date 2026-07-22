using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GameSettingsApplier
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        float volume = PlayerPrefs.GetFloat("Volume", 1f);
        AudioListener.volume = volume;

        int textureQuality = PlayerPrefs.GetInt("TextureQuality", -1);

        if (textureQuality >= 0)
            QualitySettings.globalTextureMipmapLimit = textureQuality;

        int shadowQuality = PlayerPrefs.GetInt("ShadowQuality", -1);

        if (shadowQuality >= 0)
            QualitySettings.shadows = (UnityEngine.ShadowQuality)shadowQuality;

        float shadowDistance = PlayerPrefs.GetFloat("ShadowDistance", -1f);

        if (shadowDistance >= 0f)
            QualitySettings.shadowDistance = shadowDistance;

        int shadowResolution = PlayerPrefs.GetInt("ShadowResolution", -1);

        if (shadowResolution >= 0)
        {
            QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)shadowResolution;

            int[] resolutions = { 256, 512, 1024, 2048 };
            int res = resolutions[Mathf.Clamp(shadowResolution, 0, resolutions.Length - 1)];
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (urp != null)
                urp.mainLightShadowmapResolution = res;
        }

        bool hasVSync = PlayerPrefs.HasKey("VSync");

        if (hasVSync)
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync") == 1 ? 1 : 0;

        int fps = PlayerPrefs.GetInt("MaxFps", -1);

        if (fps >= 30)
            Application.targetFrameRate = fps >= 200 ? -1 : fps;
    }
}
