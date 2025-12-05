using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;

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
    [SerializeField] private TextMeshPro gameSpeedText = null!;
    [SerializeField] private TextMeshPro maskedScreenModeText = null!;
    [SerializeField] private TextMeshPro maskedResolutionText = null!;
    [SerializeField] private TextMeshPro maskedGameSpeedText = null!;

    [Header("Horizontal UI")]
    [SerializeField] private HorizontalUI bgmHorizontalUI = null!;
    [SerializeField] private HorizontalUI seHorizontalUI = null!;
    [SerializeField] private HorizontalUI screenModeHorizontalUI = null!;
    [SerializeField] private HorizontalUI resolutionHorizontalUI = null!;
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
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
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
        var displayText  = mode == OptionModel.ScreenModeType.Windowed ? "WINDOWED" : "FULLSCREEN";
        screenModeText.text = displayText ;
        maskedScreenModeText.text = displayText ;

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

    public void UpdateGameSpeedDisplay(OptionModel.GameSpeedType speed, int direction = 0)
    {
        var displayText  = speed == OptionModel.GameSpeedType.Normal ? "NORMAL" : "FAST";
        gameSpeedText.text = displayText ;
        maskedGameSpeedText.text = displayText ;

        if (direction > 0) gameSpeedHorizontalUI.OnRightInput();
        else if (direction < 0) gameSpeedHorizontalUI.OnLeftInput();
    }
}