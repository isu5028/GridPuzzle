using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.Localization.Components;
using System.Security;

public class OptionView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform cursorObj = null!; // カーソル画像
    [SerializeField] private Transform[] itemPositions = null!; // 各項目の配置位置(親オブジェクト等)
    [SerializeField] private TextMeshPro bgmValueText = null!;
    [SerializeField] private TextMeshPro seValueText = null!;
    [SerializeField] private TextMeshPro maskedBgmValueText = null!;
    [SerializeField] private TextMeshPro maskedSeValueText = null!;
    [SerializeField] private Transform bgmBar = null!;
    [SerializeField] private Transform seBar = null!;
    [SerializeField] private Transform maskedBgmBar = null!;
    [SerializeField] private Transform maskedSeBar = null!;
    [SerializeField] private TextMeshPro screenModeText = null!;
    [SerializeField] private TextMeshPro resolutionText = null!;
    [SerializeField] private TextMeshPro languageText = null!;
    [SerializeField] private TextMeshPro gameSpeedText = null!;
    [SerializeField] private TextMeshPro maskedScreenModeText = null!;
    [SerializeField] private TextMeshPro maskedResolutionText = null!;
    [SerializeField] private TextMeshPro maskedLanguageText = null!;
    [SerializeField] private TextMeshPro maskedGameSpeedText = null!;

    [Header("Horizontal UI")]
    [SerializeField] private HorizontalUI bgmHorizontalUI = null!;
    [SerializeField] private HorizontalUI seHorizontalUI = null!;
    [SerializeField] private HorizontalUI screenModeHorizontalUI = null!;
    [SerializeField] private HorizontalUI resolutionHorizontalUI = null!;
    [SerializeField] private HorizontalUI languageHorizontalUI = null!;
    [SerializeField] private HorizontalUI gameSpeedHorizontalUI = null!;

    [Header("Animation Settings")]
    [SerializeField] private Transform optionPanel = null!; // アニメーションさせるパネル
    [SerializeField] private float slideDuration = 0.3f;
    // Presenterに通知するためのイベント

    [HideInInspector] public InputAction InputMove = null!;
    [HideInInspector] public InputAction InputDecide = null!;
    [HideInInspector] public InputAction InputOption = null!;

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    void Awake()
    {
        InputMove = InputSystem.actions.FindAction("Move");
        InputDecide = InputSystem.actions.FindAction("Decide");
        InputOption = InputSystem.actions.FindAction("Option");

        if (optionPanel != null)
        {
            visiblePosition = new Vector3(0, 0, optionPanel.localPosition.z);
            hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
            optionPanel.localPosition = hiddenPosition;
        }

#if UNITY_WEBGL
        if (screenModeText) screenModeText.gameObject.SetActive(false);
        if (maskedScreenModeText) maskedScreenModeText.gameObject.SetActive(false);
        if (screenModeHorizontalUI) screenModeHorizontalUI.gameObject.SetActive(false);
        if (resolutionText) resolutionText.gameObject.SetActive(false);
        if (maskedResolutionText) maskedResolutionText.gameObject.SetActive(false);
        if (resolutionHorizontalUI) resolutionHorizontalUI.gameObject.SetActive(false);
#endif
    }

    public void InitialShow()
    {
        optionPanel.localPosition = hiddenPosition;
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;

        AudioManager.Instance.SetMasterHighPassFilter(400);

        isAnimating = true;
        optionPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;

        AudioManager.Instance.SetMasterHighPassFilter(10);

        isAnimating = true;
        optionPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideUpAsync()
    {
        if (isAnimating) return;

        AudioManager.Instance.SetMasterHighPassFilter(10);

        isAnimating = true;
        // 画面上部へ移動
        Vector3 topPosition = new Vector3(visiblePosition.x, visiblePosition.y + Screen.height, visiblePosition.z);
        optionPanel.DOLocalMove(topPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    // カーソル位置の更新
    public void UpdateCursorPosition(int index)
    {
        if (index >= 0 && index < itemPositions.Length)
        {
            // ターゲットの横に配置するなど、オフセットが必要ならここで足す
            cursorObj.position = itemPositions[index].position;
        }
    }

    public async UniTask BlinkCursorAsync()
    {
        cursorObj.gameObject.SetActive(false);
        await UniTask.Delay(150);
        cursorObj.gameObject.SetActive(true);
    }

    // 音量表示の更新
    public void UpdateBgmDisplay(int value, int direction = 0)
    {
        bgmValueText.text = value.ToString();
        maskedBgmValueText.text = value.ToString();
        bgmBar.localScale = new Vector3(value / 100f, 1f, 1f);
        maskedBgmBar.localScale = new Vector3(value / 100f, 1f, 1f);

        if (direction > 0) bgmHorizontalUI.OnRightInput();
        else if (direction < 0) bgmHorizontalUI.OnLeftInput();
    }

    public void UpdateSeDisplay(int value, int direction = 0)
    {
        seValueText.text = value.ToString();
        maskedSeValueText.text = value.ToString();
        seBar.localScale = new Vector3(value / 100f, 1f, 1f);
        maskedSeBar.localScale = new Vector3(value / 100f, 1f, 1f);

        if (direction > 0) seHorizontalUI.OnRightInput();
        else if (direction < 0) seHorizontalUI.OnLeftInput();
    }

    public void UpdateScreenModeDisplay(OptionModel.ScreenModeType mode, int direction = 0)
    {
        string key = mode == OptionModel.ScreenModeType.Window ? "OPTION_SCREEN_MODE_WINDOW" : "OPTION_SCREEN_MODE_FULL_SCREEN";
        SetLocalizedText(screenModeText, "StringTable", key);
        SetLocalizedText(maskedScreenModeText, "StringTable", key);

        if (direction > 0) screenModeHorizontalUI.OnRightInput();
        else if (direction < 0) screenModeHorizontalUI.OnLeftInput();
    }

    public void UpdateResolutionDisplay(Vector2Int resolution, int direction = 0)
    {
        resolutionText.text = $"{resolution.x} x {resolution.y}";
        maskedResolutionText.text = $"{resolution.x} x {resolution.y}";

        if (direction > 0) resolutionHorizontalUI.OnRightInput();
        else if (direction < 0) resolutionHorizontalUI.OnLeftInput();
    }

    public void UpdateLanguageDisplay(OptionModel.LanguageType language, int direction = 0)
    {
        string key = language == OptionModel.LanguageType.Japanese ? "OPTION_LANGUAGE_JAPANESE" : "OPTION_LANGUAGE_ENGLISH";
        SetLocalizedText(languageText, "StringTable", key);
        SetLocalizedText(maskedLanguageText, "StringTable", key);

        if (direction > 0) languageHorizontalUI.OnRightInput();
        else if (direction < 0) languageHorizontalUI.OnLeftInput();
    }

    public void UpdateGameSpeedDisplay(OptionModel.GameSpeedType speed, int direction = 0)
    {
        string key = speed == OptionModel.GameSpeedType.Slow ? "OPTION_GAME_SPEED_NORMAL" : "OPTION_GAME_SPEED_FAST";
        SetLocalizedText(gameSpeedText, "StringTable", key);
        SetLocalizedText(maskedGameSpeedText, "StringTable", key);

        if (direction > 0) gameSpeedHorizontalUI.OnRightInput();
        else if (direction < 0) gameSpeedHorizontalUI.OnLeftInput();
    }

    private void SetLocalizedText(TextMeshPro textComp, string table, string key)
    {
        if (textComp.TryGetComponent<LocalizeStringEvent>(out var locEvent))
        {
            locEvent.StringReference.SetReference(table, key);
        }
        else
        {
            SetTextAsync(textComp, table, key).Forget();
        }
    }

    private async UniTaskVoid SetTextAsync(TextMeshPro textComp, string table, string key)
    {
        var op = UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;
        if (!op.IsDone)
        {
            await op;
        }

        if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
        {
            var text = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
            if (textComp != null) textComp.text = text;
        }
    }
}