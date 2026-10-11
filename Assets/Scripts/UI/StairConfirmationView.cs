using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// 階段確認ダイアログのView
/// プレイヤーが階段マスに乗ったときに「移動しますか？」と確認する
/// 他のUIと同様にシーン内に配置し、平行移動で表示/非表示を行う
/// </summary>
public class StairConfirmationView : MonoBehaviour
{
    [SerializeField] private Transform stairConfirmationPanel = null!;
    [SerializeField] private TextMeshPro destinationText = null!;
    [SerializeField] private Transform[] itemPositions = null!;  // 0: Yes, 1: No
    [SerializeField] private Transform cursorObj = null!;
    [SerializeField] private float slideDuration = 0.3f;

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;
    private bool isVisible = false;

    public bool IsAnimating => isAnimating;
    public bool IsVisible => isVisible;

    void Start()
    {
        if (stairConfirmationPanel != null)
        {
            // 表示位置を現在の位置として保存
            visiblePosition = new Vector3(0, 0, stairConfirmationPanel.localPosition.z);
            // 非表示位置は画面下に隠す
            hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
            // 初期状態は画面外に配置
            stairConfirmationPanel.localPosition = hiddenPosition;
        }
    }

    public void SetDestinationName(string name)
    {
        if (destinationText != null)
        {
            destinationText.text = name;
        }
    }

    public void UpdateCursorPosition(int index)
    {
        if (index >= 0 && index < itemPositions.Length)
        {
            cursorObj.position = itemPositions[index].position;
        }
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        isVisible = true;
        stairConfirmationPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        stairConfirmationPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
        isVisible = false;
    }

    public async UniTask BlinkCursorAsync()
    {
        if (cursorObj == null) return;
        cursorObj.gameObject.SetActive(false);
        await UniTask.Delay(150);
        cursorObj.gameObject.SetActive(true);
    }
}
