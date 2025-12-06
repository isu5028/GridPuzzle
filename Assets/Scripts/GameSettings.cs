using UnityEngine;

public static class GameSettings
{
    public static int screenMode = 0;
    public static Vector2Int Resolution = new Vector2Int(960, 540);
    public static int GameSpeed = 1;
    public static int Language = 0; // 0: English, 1: Japanese

    public static void ApplySettings()
    {
        // 解像度とモードの適用 (0: Window, 1: Fullscreen)
        FullScreenMode mode = screenMode == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        Screen.SetResolution(Resolution.x, Resolution.y, mode);

        // PixelPerfectCameraの設定更新
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.ApplyResolutionSettings(Resolution);
        }

        // 言語の適用 (Localization)
        var op = UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;
        if (op.IsDone)
        {
            SetLanguage(Language);
        }
        else
        {
            op.Completed += _ => SetLanguage(Language);
        }
    }

    private static void SetLanguage(int langIndex)
    {
        var locales = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales;
        if (locales != null && langIndex < locales.Count)
        {
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = locales[langIndex];
        }
    }
}