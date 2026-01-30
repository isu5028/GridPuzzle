using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using UniRx;

/// <summary>
/// 階段確認ダイアログのPresenter
/// プレイヤーの入力を処理し、Viewを更新する
/// </summary>
public class StairConfirmationPresenter : MonoBehaviour
{
    [SerializeField] private StairConfirmationView view = null!;
    [SerializeField] private GridManager gridManager = null!;

    private InputAction inputMove = null!;
    private InputAction inputAttack = null!;
    private InputAction inputCancel = null!;

    private int selectedIndex = 1;  // 0: はい, 1: いいえ（デフォルトは「いいえ」）
    private const int OPTION_YES = 0;
    private const int OPTION_NO = 1;

    private Action<bool> onConfirmCallback;
    private bool isProcessing = false;

    public bool IsOpen => view != null && view.IsVisible;

    void Start()
    {
        inputMove = InputSystem.actions.FindAction("Move");
        inputAttack = InputSystem.actions.FindAction("Attack");
        inputCancel = InputSystem.actions.FindAction("Cancel");

        if (gridManager == null)
        {
            gridManager = FindObjectOfType<GridManager>();
        }
    }

    void Update()
    {
        if (!IsOpen || view.IsAnimating || isProcessing) return;

        HandleInput();
    }

    private void HandleInput()
    {
        // 上下でカーソル移動（ループする）
        if (inputMove.triggered)
        {
            Vector2 input = inputMove.ReadValue<Vector2>();
            if (Mathf.Abs(input.y) > 0.5f)
            {
                int newIndex;
                if (input.y > 0)
                {
                    // 上を押した：インデックスを減らす（YES方向）、上端ならループして下端へ
                    newIndex = selectedIndex - 1;
                    if (newIndex < OPTION_YES) newIndex = OPTION_NO;  // ループ
                }
                else
                {
                    // 下を押した：インデックスを増やす（NO方向）、下端ならループして上端へ
                    newIndex = selectedIndex + 1;
                    if (newIndex > OPTION_NO) newIndex = OPTION_YES;  // ループ
                }

                if (newIndex != selectedIndex)
                {
                    selectedIndex = newIndex;
                    view.UpdateCursorPosition(selectedIndex);
                    AudioManager.Instance.PlaySE(AudioManager.SEType.cursor);
                }
            }
        }

        // 決定
        if (inputAttack.triggered)
        {
            OnConfirm().Forget();
        }

        // キャンセル
        if (inputCancel.triggered)
        {
            OnCancel().Forget();
        }
    }

    public async UniTask ShowAsync(CellType stairType, StageSO targetStage, string displayName, Action<bool> callback)
    {
        if (view == null || view.IsVisible) return;

        onConfirmCallback = callback;
        selectedIndex = OPTION_YES;  // デフォルトは「はい」

        // 移動先の名前を設定（上へ/下へ + フロア名）
        string directionText = stairType == CellType.StairUp ? "上へ" : "下へ";
        view.SetDestinationName($"{directionText} {displayName}");
        view.UpdateCursorPosition(selectedIndex);

        await view.ShowAsync();
    }

    private async UniTask OnConfirm()
    {
        isProcessing = true;

        // 決定時のカーソル点滅などの演出は待っても良いが、短くする
        // await view.BlinkCursorAsync();
        AudioManager.Instance.PlaySE(AudioManager.SEType.cursor);

        bool confirmed = selectedIndex == OPTION_YES;

        // Callbackを先に呼ぶ（ステージ遷移を開始させる）
        onConfirmCallback?.Invoke(confirmed);
        onConfirmCallback = null;

        // ダイアログは非同期で閉じる
        view.HideAsync().Forget();

        isProcessing = false;
    }

    private async UniTask OnCancel()
    {
        isProcessing = true;
        AudioManager.Instance.PlaySE(AudioManager.SEType.cursor);
        await view.HideAsync();

        onConfirmCallback?.Invoke(false);
        onConfirmCallback = null;
        isProcessing = false;
    }

    public async UniTask HideAsync()
    {
        if (view != null && view.IsVisible)
        {
            await view.HideAsync();
        }
        onConfirmCallback = null;
    }
}
