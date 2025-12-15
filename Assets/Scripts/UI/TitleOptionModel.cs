using UnityEngine;
using System.Collections.Generic;

public class TitleOptionModel
{
    // Items specific to Title Option
    // ORDER: GAME SPEED, BGM, SE, SCREEN MODE, RESOLUTION, LANGUAGE, DELETE SAVEDATA, CLOSE
    public enum ItemType
    {
        GameSpeed,
        BgmVolume,
        SeVolume,
        ScreenMode,
        Resolution,
        Language,
        DeleteSaveData,
        Close
    }

    public ItemType CurrentItem { get; private set; } = ItemType.GameSpeed;

    // Reuse settings logic from OptionModel or duplicate/share?
    // Sharing logic is better, but for MVP speed, duplication with slight modification or composition.
    // Since OptionModel has hardcoded ItemType enum that includes InGame options, making a separate Model is cleaner for now.

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

    public TitleOptionModel()
    {
        // Initialize from GameSettings
        ScreenMode = (ScreenModeType)GameSettings.screenMode;
        int resIndex = AvailableResolutions.FindIndex(r => r.x == GameSettings.Resolution.x && r.y == GameSettings.Resolution.y);
        CurrentResolutionIndex = resIndex >= 0 ? resIndex : 0;
        GameSpeed = (GameSpeedType)GameSettings.GameSpeed;
        Language = (LanguageType)GameSettings.Language;
        BgmLevel = GameSettings.BgmVolume;
        SeLevel = GameSettings.SeVolume;
    }

    public void MoveSelection(int direction)
    {
        int current = (int)CurrentItem;
        int count = System.Enum.GetNames(typeof(ItemType)).Length;
        current = (current + direction + count) % count;
        CurrentItem = (ItemType)current;
    }

    // Logic methods similar to OptionModel
    public void ChangeBgm(int amount) => BgmLevel = Mathf.Clamp(BgmLevel + amount, MinLevel, MaxLevel);
    public void ChangeSe(int amount) => SeLevel = Mathf.Clamp(SeLevel + amount, MinLevel, MaxLevel);
    public void ChangeScreenMode(int direction) => ScreenMode = Utility.GetNextEnum(ScreenMode, direction);
    public void ChangeGameSpeed(int direction) => GameSpeed = Utility.GetNextEnum(GameSpeed, direction);
    public void ChangeResolution(int direction)
    {
        int count = AvailableResolutions.Count;
        CurrentResolutionIndex = (CurrentResolutionIndex + direction + count) % count;
    }
    public void ChangeLanguage(int direction) => Language = Utility.GetNextEnum(Language, direction);
}
