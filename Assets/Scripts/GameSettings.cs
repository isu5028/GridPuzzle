using UnityEngine;

public static class GameSettings
{
    public static int screenMode = 0;
    public static Vector2Int Resolution = new Vector2Int(960, 540);
    public static int GameSpeed = 1;
    public static int Language = 1; // 0: English, 1: Japanese
    public static int BgmVolume = 50;
    public static int SeVolume = 50;
    private static bool isLanguageApplyCallbackRegistered = false;

    public static void ApplySettings()
    {
#if !UNITY_WEBGL
        // 解像度とモードの適用 (0: Window, 1: Fullscreen)
        FullScreenMode mode = screenMode == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        Screen.SetResolution(Resolution.x, Resolution.y, mode);
#endif

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
        else if (!isLanguageApplyCallbackRegistered)
        {
            isLanguageApplyCallbackRegistered = true;
            op.Completed += OnLocalizationInitialized;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(BgmVolume / 100f);
            AudioManager.Instance.SetSEVolume(SeVolume / 100f);
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

    private static void OnLocalizationInitialized(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<UnityEngine.Localization.Settings.LocalizationSettings> _)
    {
        isLanguageApplyCallbackRegistered = false;
        SetLanguage(Language);
    }
}