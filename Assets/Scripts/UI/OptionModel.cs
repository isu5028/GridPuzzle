using UnityEngine;
using System;
using System.Collections.Generic;


public class OptionModel
{
    // 設定項目の定義
    public enum ItemType { Save, Load, GameSpeed, BgmVolume, SeVolume, ScreenMode, Resolution, Language, ReturnToTitle, Close}

    public ItemType CurrentItem { get; private set; } = ItemType.Save;

    // 音量 (0〜100の整数で管理する例)
    public int BgmLevel { get; private set; } = 50;
    public int SeLevel { get; private set; } = 50;

    public int MaxLevel = 100;
    public int MinLevel = 0;

    public enum ScreenModeType { Window, Fullscreen}
    public ScreenModeType ScreenMode { get; private set; } = ScreenModeType.Window;

    public List<Vector2Int> AvailableResolutions { get; private set; } = new List<Vector2Int>()
    {
        new Vector2Int(960, 540),
        new Vector2Int(1440, 810),
        new Vector2Int(1920, 1080)
    };
    public int CurrentResolutionIndex { get; private set; } = 0;
    public Vector2Int CurrentResolution => AvailableResolutions[CurrentResolutionIndex];


    public enum GameSpeedType { Normal, Fast}
    public GameSpeedType GameSpeed { get; private set; } = GameSpeedType.Normal;

    public enum LanguageType { English, Japanese }
    public LanguageType Language { get; private set; } = LanguageType.English;

    public OptionModel()
    {
        // GameSettingsから初期値を反映
        ScreenMode = (ScreenModeType)GameSettings.screenMode;

        int resIndex = AvailableResolutions.FindIndex(r => r.x == GameSettings.Resolution.x && r.y == GameSettings.Resolution.y);
        CurrentResolutionIndex = resIndex >= 0 ? resIndex : 0;

        GameSpeed = (GameSpeedType)GameSettings.GameSpeed;
        Language = (LanguageType)GameSettings.Language;

        BgmLevel = GameSettings.BgmVolume;
        SeLevel = GameSettings.SeVolume;
    }

    // 選択項目の移動
    public void MoveSelection(int direction) // +1 or -1
    {
        int current = (int)CurrentItem;
        int count = System.Enum.GetNames(typeof(ItemType)).Length;

        // ループさせる計算
        do
        {
            current = (current + direction + count) % count;
        }
#if UNITY_WEBGL
        while ((ItemType)current == ItemType.ScreenMode || (ItemType)current == ItemType.Resolution);
#else
        while (false);
#endif
        CurrentItem = (ItemType)current;
    }

    // 音量の変更
    public void ChangeBgm(int amount)
    {
        BgmLevel = Mathf.Clamp(BgmLevel + amount, MinLevel, MaxLevel);
    }

    public void ChangeSe(int amount)
    {
        SeLevel = Mathf.Clamp(SeLevel + amount, MinLevel, MaxLevel);
    }

    public void ChangeScreenMode(int direction)
    {
        ScreenMode = Utility.GetNextEnum(ScreenMode, direction);
    }

    public void ChangeGameSpeed(int direction)
    {
        GameSpeed = Utility.GetNextEnum(GameSpeed, direction);
    }

    public void ChangeResolution(int direction)
    {
        int count = AvailableResolutions.Count;
        CurrentResolutionIndex = (CurrentResolutionIndex + direction + count) % count;
    }

    public void ChangeLanguage(int direction)
    {
        Language = Utility.GetNextEnum(Language, direction);
    }
}