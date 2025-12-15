using UnityEngine;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Components;

public class TitleOptionView : MonoBehaviour
{
    [SerializeField] private Transform[] itemPositions = null!;
    [SerializeField] private Transform cursorObj = null!;
    [SerializeField] private Transform container = null!;
    [SerializeField] private TextMeshPro gameSpeedText;
    [SerializeField] private TextMeshPro maskedGameSpeedText;
    [SerializeField] private TextMeshPro bgmText;
    [SerializeField] private TextMeshPro maskedBgmText;
    [SerializeField] private TextMeshPro seText;
    [SerializeField] private TextMeshPro maskedSeText;
    [SerializeField] private TextMeshPro screenModeText;
    [SerializeField] private TextMeshPro maskedScreenModeText;
    [SerializeField] private TextMeshPro resolutionText;
    [SerializeField] private TextMeshPro maskedResolutionText;
    [SerializeField] private TextMeshPro languageText;
    [SerializeField] private TextMeshPro maskedLanguageText;

    [Header("Volume Bars")]
    [SerializeField] private Transform bgmBar = null!;
    [SerializeField] private Transform seBar = null!;
    [SerializeField] private Transform maskedBgmBar = null!;
    [SerializeField] private Transform maskedSeBar = null!;

    [Header("Horizontal UI")]
    [SerializeField] private HorizontalUI gameSpeedHorizontalUI = null!;
    [SerializeField] private HorizontalUI bgmHorizontalUI = null!;
    [SerializeField] private HorizontalUI seHorizontalUI = null!;
    [SerializeField] private HorizontalUI screenModeHorizontalUI = null!;
    [SerializeField] private HorizontalUI resolutionHorizontalUI = null!;
    [SerializeField] private HorizontalUI languageHorizontalUI = null!;

    [SerializeField] private float slideDuration = 0.3f;
    private Vector3 visiblePos;
    private Vector3 hiddenPos;
    private bool isInitialized = false;

    private void Initialize()
    {
        if (isInitialized) return;
        if (container != null)
        {
             visiblePos = new Vector3(0, 0, 0);
             hiddenPos = visiblePos + new Vector3(0, -Screen.height, 0);
        }
        isInitialized = true;
    }

    public void UpdateView(TitleOptionModel model, int direction = 0)
    {
        UpdateGameSpeedDisplay(model.GameSpeed, model.CurrentItem == TitleOptionModel.ItemType.GameSpeed ? direction : 0);
        UpdateBgmDisplay(model.BgmLevel, model.CurrentItem == TitleOptionModel.ItemType.BgmVolume ? direction : 0);
        UpdateSeDisplay(model.SeLevel, model.CurrentItem == TitleOptionModel.ItemType.SeVolume ? direction : 0);
        UpdateScreenModeDisplay(model.ScreenMode, model.CurrentItem == TitleOptionModel.ItemType.ScreenMode ? direction : 0);
        UpdateResolutionDisplay(model.CurrentResolution, model.CurrentItem == TitleOptionModel.ItemType.Resolution ? direction : 0);
        UpdateLanguageDisplay(model.Language, model.CurrentItem == TitleOptionModel.ItemType.Language ? direction : 0);

        UpdateCursor((int)model.CurrentItem);
    }

    public void UpdateGameSpeedDisplay(TitleOptionModel.GameSpeedType speed, int direction = 0)
    {
        string key = speed == TitleOptionModel.GameSpeedType.Normal ? "OPTION_GAME_SPEED_NORMAL" : "OPTION_GAME_SPEED_FAST";
        SetLocalizedText(gameSpeedText, "StringTable", key);
        SetLocalizedText(maskedGameSpeedText, "StringTable", key);

        if (direction > 0) gameSpeedHorizontalUI?.OnRightInput();
        else if (direction < 0) gameSpeedHorizontalUI?.OnLeftInput();
    }

    public void UpdateBgmDisplay(int value, int direction = 0)
    {
        string text = value.ToString();
        if (bgmText) bgmText.text = text;
        if (maskedBgmText) maskedBgmText.text = text;

        if (bgmBar) bgmBar.localScale = new Vector3(value / 100f, 1f, 1f);
        if (maskedBgmBar) maskedBgmBar.localScale = new Vector3(value / 100f, 1f, 1f);

        if (direction > 0) bgmHorizontalUI?.OnRightInput();
        else if (direction < 0) bgmHorizontalUI?.OnLeftInput();
    }

    public void UpdateSeDisplay(int value, int direction = 0)
    {
        string text = value.ToString();
        if (seText) seText.text = text;
        if (maskedSeText) maskedSeText.text = text;

        if (seBar) seBar.localScale = new Vector3(value / 100f, 1f, 1f);
        if (maskedSeBar) maskedSeBar.localScale = new Vector3(value / 100f, 1f, 1f);

        if (direction > 0) seHorizontalUI?.OnRightInput();
        else if (direction < 0) seHorizontalUI?.OnLeftInput();
    }

    public void UpdateScreenModeDisplay(TitleOptionModel.ScreenModeType mode, int direction = 0)
    {
        string key = mode == TitleOptionModel.ScreenModeType.Window ? "OPTION_SCREEN_MODE_WINDOW" : "OPTION_SCREEN_MODE_FULL_SCREEN";
        SetLocalizedText(screenModeText, "StringTable", key);
        SetLocalizedText(maskedScreenModeText, "StringTable", key);

        if (direction > 0) screenModeHorizontalUI?.OnRightInput();
        else if (direction < 0) screenModeHorizontalUI?.OnLeftInput();
    }

    public void UpdateResolutionDisplay(Vector2Int res, int direction = 0)
    {
        string text = $"{res.x} x {res.y}";
        if (resolutionText) resolutionText.text = text;
        if (maskedResolutionText) maskedResolutionText.text = text;

        if (direction > 0) resolutionHorizontalUI?.OnRightInput();
        else if (direction < 0) resolutionHorizontalUI?.OnLeftInput();
    }

    public void UpdateLanguageDisplay(TitleOptionModel.LanguageType lang, int direction = 0)
    {
        string key = lang == TitleOptionModel.LanguageType.Japanese ? "OPTION_LANGUAGE_JAPANESE" : "OPTION_LANGUAGE_ENGLISH";
        SetLocalizedText(languageText, "StringTable", key);
        SetLocalizedText(maskedLanguageText, "StringTable", key);

        if (direction > 0) languageHorizontalUI?.OnRightInput();
        else if (direction < 0) languageHorizontalUI?.OnLeftInput();
    }

    public void UpdateCursor(int index)
    {
        if (index >= 0 && index < itemPositions.Length && cursorObj != null)
        {
            cursorObj.position = itemPositions[index].position;
        }
    }

    public async UniTask ShowAsync()
    {
        Initialize();
        if (container)
        {
            container.localPosition = hiddenPos;
            container.DOLocalMove(visiblePos, slideDuration).SetEase(Ease.OutCubic);
        }
        await UniTask.Delay(System.TimeSpan.FromSeconds(slideDuration));
    }

    public async UniTask HideAsync()
    {
        Initialize();
        if (container)
        {
            container.DOLocalMove(hiddenPos, slideDuration).SetEase(Ease.InCubic);
        }
        await UniTask.Delay(System.TimeSpan.FromSeconds(slideDuration));
    }

    private void SetLocalizedText(TextMeshPro textComp, string table, string key)
    {
        if (textComp.TryGetComponent<LocalizeStringEvent>(out var locEvent))
        {
            locEvent.StringReference.SetReference(table, key);
        }
        else
        {
            var text = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
            textComp.text = text;
        }
    }
}
