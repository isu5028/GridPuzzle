using UnityEngine;
using UnityEngine.InputSystem;

using Cysharp.Threading.Tasks;
using DG.Tweening;
using UniRx;
using System;

public class Player : Unit
{
    private InputAction inputMove = null!;
    private InputAction inputAttack = null!;
    private InputAction inputStay = null!;
    private InputAction inputUndo = null!;
    private InputAction inputRedo = null!;
    private InputAction inputReset = null!;

    private bool isProcessingAction = false;

    private bool isInputBlocked = false;

    protected override void Start()
    {
        Initialize();
        base.Start();
        inputMove = InputSystem.actions.FindAction("Move");
        inputAttack = InputSystem.actions.FindAction("Attack");
        inputStay = InputSystem.actions.FindAction("Stay");
        inputUndo = InputSystem.actions.FindAction("Undo");
        inputRedo = InputSystem.actions.FindAction("Redo");
        inputReset = InputSystem.actions.FindAction("Reset");
    }

    public void WarpToNewStage(Vector2Int newPos, Direction newDir)
    {
        currentPos = newPos;
        currentDirection = newDir;
        unitAnimator.SetDirection(newDir);

        // 座標更新
        transform.position = gridManager.GridToWorldPosition(newPos);

        // アクション中のフラグをリセット（階段移動などでOnActionCompleteがスキップされた場合用）
        isProcessingAction = false;
        isMoving = false;

        BlockInputTemporarily().Forget();
    }

    private async UniTaskVoid BlockInputTemporarily()
    {
        isInputBlocked = true;
        // 0.2秒待つ（フェード演出などがあるならその時間分待つ）
        await UniTask.Delay(100);
        isInputBlocked = false;
    }

    private void Initialize()
    {
        // 入力が可能な状態かどうかを判定するストリーム
        var canInputStream = Observable.EveryUpdate()
            .Where(_ =>
                turnManager.IsPlayerTurn &&
                !isInputBlocked &&
                !isProcessingAction &&
                !turnManager.IsOptionOpen &&
                !gridManager.IsTransitioning &&
                !gridManager.IsEventPlaying &&
                !gridManager.IsStairDialogOpen &&  // 階段ダイアログ中は操作不可
                !isMoving &&
                !turnManager.IsMapOpen
            )
            .Share();

        // --- Undo処理 ---
        // 死亡していてもUndoは可能にする（isDeadチェックを通さない）

        var undoTriggered = canInputStream.Where(_ => inputUndo.triggered);

        // 単発
        undoTriggered
            .Subscribe(_ => turnManager.UndoTurn().Forget())
            .AddTo(this);

        // 長押しリピート
        undoTriggered
            .SelectMany(_ => Observable.Timer(TimeSpan.FromSeconds(0.4f), TimeSpan.FromSeconds(0.15f))
                .TakeUntil(Observable.EveryUpdate().Where(__ => !inputUndo.IsPressed()))
            )
            .ObserveOnMainThread()
            .Subscribe(_ =>
            {
                // リピート中も最低限のガード条件だけ再確認（ターン状態など）
                if(turnManager.IsPlayerTurn && !isProcessingAction && !gridManager.IsTransitioning && !commandManager.IsUndoing)
                {
                    turnManager.UndoTurn().Forget();
                }
            })
            .AddTo(this);

        // --- Redo処理 ---
        // 死亡していてもRedoは可能にする

        var redoTriggered = canInputStream.Where(_ => inputRedo.triggered);

        // 単発
        redoTriggered
            .Subscribe(_ => turnManager.RedoTurn().Forget())
            .AddTo(this);

        // 長押しリピート
        redoTriggered
            .SelectMany(_ => Observable.Timer(TimeSpan.FromSeconds(0.4f), TimeSpan.FromSeconds(0.15f))
                .TakeUntil(Observable.EveryUpdate().Where(__ => !inputRedo.IsPressed()))
            )
            .ObserveOnMainThread()
            .Subscribe(_ =>
            {
                if(turnManager.IsPlayerTurn && !isProcessingAction && !gridManager.IsTransitioning && !commandManager.IsRedoing)
                {
                    turnManager.RedoTurn().Forget();
                }
            })
            .AddTo(this);

        // --- Reset ---
        canInputStream
            .Where(_ => inputReset.triggered)
            .Subscribe(_ => turnManager.ResetTurn())
            .AddTo(this);

        // --- Move (死亡時は操作不可) ---
        canInputStream
            .Where(_ => !isDead)
            .Select(_ => inputMove.ReadValue<Vector2>())
            .Where(v => v != Vector2.zero)
            .Subscribe(inputValue => HandleMoveInput(inputValue))
            .AddTo(this);

        // --- Attack (死亡時は操作不可) ---
        canInputStream
             .Where(_ => !isDead && inputAttack.triggered)
             .Subscribe(_ =>
             {
                 turnManager.EnsureTurnStartSnapshot();
                 isProcessingAction = true;
                 TryAttack();
             })
             .AddTo(this);
    }

    private void HandleMoveInput(Vector2 inputValue)
    {
        turnManager.EnsureTurnStartSnapshot();

        Direction newDirection = Utility.GetDirectionFromStickInput(inputValue);
        currentDirection = newDirection;

        if(inputStay.IsPressed())
        {
            unitAnimator.SetDirection(newDirection);
            return;
        }

        Vector2Int directionVector = Utility.GetVectorFromDirection(newDirection);
        Vector2Int targetPos = currentPos + directionVector;

        // グリッド外に出るかチェック
        if (!gridManager.IsPositionInBounds(targetPos))
        {
            return;
        }

        isProcessingAction = true;
        TryMoveAsync(currentDirection).Forget();
    }

    private async UniTaskVoid TryMoveAsync(Direction direction)
    {
        bool didMove = await TryMove(direction);

        if (!didMove)
        {
            // 移動しなかった（攻撃や移動失敗）場合はフラグをリセットして終了
            isProcessingAction = false;
            return;
        }

        // GameSpeed == 0 (Normal) のときは移動完了を待つ
        // 高速モードではプレイヤーの移動完了を待たずに敵のターンへ進む
        if (GameSettings.GameSpeed == 0)
        {
            await UniTask.WaitUntil(() => !isMoving);
        }

        // 階段チェック（移動完了後に階段の上にいるか確認）
        bool dialogShown = gridManager.CheckAndShowStairDialog();

        if (dialogShown)
        {
            // ダイアログが表示された場合は、ユーザーの選択待ちになるので
            // ここでターン終了せず、ダイアログのコールバックに任せる
            return;
        }

        OnActionComplete();
    }

    protected override async void OnActionComplete()
    {
        turnManager.OnPlayerActionComplete().Forget();

        // 敵のターンが終わるまで入力をブロック
        await UniTask.WaitUntil(() => turnManager.IsPlayerTurn);

        isProcessingAction = false;
        isMoving = false;
    }

    /// <summary>
    /// 外部（GridManagerなど）からアクション完了を強制する
    /// </summary>
    public void ForceActionComplete()
    {
        OnActionComplete();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }
}