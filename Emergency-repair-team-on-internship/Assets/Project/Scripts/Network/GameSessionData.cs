using UnityEngine;

public static class GameSessionData
{
    public const string GameVersion = "Test build 0.12";

    public static string JoinCode = "";
    public static string ConnectionType = "";
    public static string LastStatus = "";
    public static int SelectedHandIndex = 0; // 0 = Right, 1 = Left
    public static int SelectedColorIndex = 0;

    public static readonly string[] ColorNames =
    {
        "Blue", "Red", "Green", "Yellow", "Purple", "Orange", "Cyan", "Pink"
    };

    public static readonly Color32[] ColorValues =
    {
        new Color32(90, 160, 255, 255),
        new Color32(255, 90, 90, 255),
        new Color32(90, 255, 140, 255),
        new Color32(255, 220, 90, 255),
        new Color32(210, 120, 255, 255),
        new Color32(255, 150, 80, 255),
        new Color32(90, 255, 240, 255),
        new Color32(255, 120, 190, 255),
    };
}
