using UnityEngine;

public static class LocalPlayerSettings
{
    private const string DefaultProfileId = "Default";

    public static string ProfileId { get; private set; } = DefaultProfileId;
    public static string PlayerName { get; private set; } = "Player";
    public static Color32 PlayerColor { get; private set; } = new Color32(255, 255, 255, 255);

    private static readonly Color32[] ColorPalette =
    {
        new Color32(255, 90, 90, 255),    // red
        new Color32(90, 160, 255, 255),   // blue
        new Color32(90, 255, 140, 255),   // green
        new Color32(255, 220, 90, 255),   // yellow
        new Color32(210, 120, 255, 255),  // purple
        new Color32(255, 150, 80, 255),   // orange
        new Color32(90, 255, 240, 255),   // cyan
        new Color32(255, 120, 190, 255),  // pink
    };

    private static string PlayerNameKey => $"PlayerName_{ProfileId}";
    private static string PlayerColorKey => $"PlayerColor_{ProfileId}";

    public static void Load()
    {
        Load(ProfileId);
    }

    public static void Load(string profileId)
    {
        SetProfileId(profileId);

        PlayerName = PlayerPrefs.GetString(PlayerNameKey, $"Player_{ProfileId}");

        if (PlayerPrefs.HasKey(PlayerColorKey))
        {
            PlayerColor = UnpackColor(PlayerPrefs.GetInt(PlayerColorKey));
        }
        else
        {
            GenerateAndSaveRandomColor();
        }
    }

    public static void SetProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            profileId = DefaultProfileId;
        }

        profileId = profileId.Trim();

        if (profileId.Length > 20)
        {
            profileId = profileId.Substring(0, 20);
        }

        ProfileId = profileId;
    }

    public static void SetPlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            value = $"Player_{ProfileId}";
        }

        value = value.Trim();

        if (value.Length > 20)
        {
            value = value.Substring(0, 20);
        }

        PlayerName = value;

        PlayerPrefs.SetString(PlayerNameKey, PlayerName);
        PlayerPrefs.Save();
    }

    public static void GenerateAndSaveRandomColor()
    {
        int index = Random.Range(0, ColorPalette.Length);
        SetPlayerColor(ColorPalette[index]);
        GameSessionData.SelectedColorIndex = index;
    }

    public static Color32 GetPaletteColor(int index)
    {
        return ColorPalette[Mathf.Clamp(index, 0, ColorPalette.Length - 1)];
    }

    public static int PaletteSize => ColorPalette.Length;

    public static void SetPlayerColor(Color32 color)
    {
        color.a = 255;

        PlayerColor = color;

        PlayerPrefs.SetInt(PlayerColorKey, PackColor(PlayerColor));
        PlayerPrefs.Save();
    }

    public static int PackColor(Color32 color)
    {
        return
            color.r << 24 |
            color.g << 16 |
            color.b << 8 |
            color.a;
    }

    public static Color32 UnpackColor(int packed)
    {
        byte r = (byte)((packed >> 24) & 0xFF);
        byte g = (byte)((packed >> 16) & 0xFF);
        byte b = (byte)((packed >> 8) & 0xFF);
        byte a = (byte)(packed & 0xFF);

        return new Color32(r, g, b, a);
    }
}