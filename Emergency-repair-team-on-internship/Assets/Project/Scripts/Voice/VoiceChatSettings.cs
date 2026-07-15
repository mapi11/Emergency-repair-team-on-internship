using UnityEngine;

public static class VoiceChatSettings
{
    private const string SelectedMicrophoneKey = "Voice_SelectedMicrophone";

    public static string SelectedMicrophoneName { get; private set; } = "";

    public static void Load()
    {
        SelectedMicrophoneName = PlayerPrefs.GetString(SelectedMicrophoneKey, "");

        if (!IsMicrophoneAvailable(SelectedMicrophoneName))
        {
            SelectedMicrophoneName = GetDefaultMicrophoneName();
            Save();
        }
    }

    public static void SetSelectedMicrophone(string microphoneName)
    {
        SelectedMicrophoneName = microphoneName;
        Save();
    }

    public static string GetSelectedOrDefaultMicrophoneName()
    {
        Load();

        if (IsMicrophoneAvailable(SelectedMicrophoneName))
        {
            return SelectedMicrophoneName;
        }

        return GetDefaultMicrophoneName();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(SelectedMicrophoneKey, SelectedMicrophoneName);
        PlayerPrefs.Save();
    }

    private static bool IsMicrophoneAvailable(string microphoneName)
    {
        if (string.IsNullOrWhiteSpace(microphoneName))
        {
            return false;
        }

        string[] devices = Microphone.devices;

        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i] == microphoneName)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDefaultMicrophoneName()
    {
        string[] devices = Microphone.devices;

        if (devices == null || devices.Length == 0)
        {
            return "";
        }

        return devices[0];
    }
}