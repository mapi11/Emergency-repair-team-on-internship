using UnityEngine;

public static class GameSettingsApplier
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        float volume = PlayerPrefs.GetFloat("Volume", 1f);
        AudioListener.volume = volume;

        int quality = PlayerPrefs.GetInt("QualityLevel", -1);

        if (quality >= 0 && quality < QualitySettings.names.Length)
            QualitySettings.SetQualityLevel(quality, true);
    }
}
