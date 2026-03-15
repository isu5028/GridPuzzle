using UnityEngine;
using UnityEngine.InputSystem;

using Cysharp.Threading.Tasks;
using DG.Tweening;
using UniRx;
using System;

public class Player : Unit
{
    [Header("Effects")]
    [SerializeField] private GameObject heartEffectPrefab = null!;

    private InputAction inputMove = null!;
    private InputAction inputAttack = null!;
    private InputAction inputStay = null!;
    private InputAction inputUndo = null!;
    private InputAction inputRedo = null!;
    private InputAction inputReset = null!;

    private bool isProcessingAction = false;

    private bool isInputBlocked = false;
    private StageSO lastShopPriceShakeStage;
    private Vector2Int lastShopPriceShakePos;
    private bool hasLastShopPriceShakePos;

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
        Debug.Log($"[Player.Initialize] Subscribing inputs for InstanceID: {this.GetInstanceID()}, gameObject: {gameObject.name}");
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
                 // isMovingがtrueの場合、同フレームにMoveサブスクライバが既にTryAttackを
                 // 呼び出している（Share()による同時発火）ので二重実行を防ぐ
                 if (isMoving) return;

                 // Shop Item上ではSPACE入力をショップ操作に割り当てる
                 if (TryHandleShopInteractionByAttack()) return;

                 turnManager.LastPlayerActionDirection = currentDirection;
                 turnManager.LastPlayerAction = PlayerActionType.Attack;
                 turnManager.EnsureTurnStartSnapshot();
                 isProcessingAction = true;
                 TryAttack();
             })
             .AddTo(this);

        // --- Shop Prompt ---
        Observable.EveryUpdate()
            .Subscribe(_ => UpdateShopPrompt())
            .AddTo(this);
    }

    private void HandleMoveInput(Vector2 inputValue)
    {
        turnManager.EnsureTurnStartSnapshot();

        Direction newDirection = Utility.GetDirectionFromStickInput(inputValue);
        turnManager.LastPlayerActionDirection = newDirection;
        turnManager.LastPlayerAction = PlayerActionType.Move;
        currentDirection = newDirection;

        if(inputStay.IsPressed())
        {
            unitAnimator.SetDirection(newDirection);
            return;
        }

        Vector2Int directionVector = Utility.GetVectorFromDirection(newDirection);
        Vector2Int targetPos = currentPos + directionVector;

        // グリッド外に出るかチェック（ただし破壊された境界壁＝通行可能なら許可）
        if (!gridManager.IsPositionInBounds(targetPos))
        {
            if (gridManager.IsWalkableAt(gridManager.CurrentStageSO, targetPos))
            {
                // 境界外へ移動可能な場合（壁が壊れている場合）
                // 通常の移動アニメーション（TryMoveAsync）を行わず、即座にステージ遷移を開始し、
                // ステージスライドに合わせてプレイヤーを移動させる（シームレスな移動）
                isProcessingAction = true;
                bool started = gridManager.CheckAndPerformBoundaryTransition(this, targetPos);
                if (!started) isProcessingAction = false;
                return;
            }
            else
            {
                // 通行不可（通常の壁）
                return;
            }
        }

        isProcessingAction = true;
        TryMoveAsync(currentDirection).Forget();
    }

    private async UniTaskVoid TryMoveAsync(Direction direction)
    {
        bool didMove = await TryMove(direction);

        if (!didMove)
        {
            if (!isMoving)
            {
                // 壁衝突・移動失敗の場合のみ即時リセット
                // isMoving=trueの場合はTryMoveから攻撃が発動しているため
                // isProcessingActionをfalseにせず、AttackAsync→OnActionCompleteに任せる
                isProcessingAction = false;
            }
            return;
        }

        // GameSpeed == 0 (Normal) のときは移動完了を待つ
        // 高速モードではプレイヤーの移動完了を待たずに敵のターンへ進む
        if (GameSettings.GameSpeed == 0)
        {
            await UniTask.WaitUntil(() => !isMoving);
        }

        // 境界遷移チェック（移動完了後に外周座標にいるか確認）
        bool transitionStarted = gridManager.CheckAndPerformBoundaryTransition(this);
        if (transitionStarted) return;

        // 階段チェック（移動完了後に階段の上にいるか確認）
        bool dialogShown = gridManager.CheckAndShowStairDialog();

        if (dialogShown)
        {
            // 階段マスに乗った → TakeStairAsyncが自動でステージ移動＋OnPlayerActionCompleteを処理するため
            // ここではターン終了せずに返る
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

    private bool TryHandleShopInteractionByAttack()
    {
        if (gridManager == null) return false;

        bool consumed = gridManager.TryInteractCurrentStageShopItem(currentPos, out bool purchasedNow, out bool readMode, out _, out _);
        if (!consumed) return false;

        if (purchasedNow)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySE(AudioManager.SEType.buy);
            }
        }
        else if (readMode)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySE(AudioManager.SEType.tap);
            }
        }
        else
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySE(AudioManager.SEType.cancel);
            }
        }

        UpdateShopPrompt();

        if (purchasedNow)
        {
            gridManager.TriggerShopPromptShake();
        }

        return true;
    }

    private void UpdateShopPrompt()
    {
        if (gridManager == null) return;

        StageSO currentStage = gridManager.CurrentStageSO;
        bool enteredNewCellOrStage = !hasLastShopPriceShakePos
            || lastShopPriceShakeStage != currentStage
            || lastShopPriceShakePos != currentPos;

        lastShopPriceShakeStage = currentStage;
        lastShopPriceShakePos = currentPos;
        hasLastShopPriceShakePos = true;

        if (isDead ||
            gridManager.IsTransitioning ||
            gridManager.IsEventPlaying ||
            gridManager.IsStairDialogOpen ||
            (turnManager != null && (turnManager.IsMapOpen || turnManager.IsOptionOpen)))
        {
            gridManager.HideShopPrompt();
            return;
        }

        if (gridManager.TryGetCurrentShopPromptText(currentPos, out string promptText))
        {
            gridManager.ShowShopPromptAt(currentPos, promptText);

            if (enteredNewCellOrStage)
            {
                gridManager.TriggerCurrentShopItemPriceShake(currentPos);
            }
        }
        else
        {
            gridManager.HideShopPrompt();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected virtual void OnDestroy()
    {
        // GameObject破棄時にUniRxのストリーム監視を全て解除し、二重発動を防ぐ
        Debug.Log($"[Player.OnDestroy] Cleaning up InstanceID: {this.GetInstanceID()}");

        if (gridManager != null)
        {
            gridManager.HideShopPrompt();
        }
    }

    public void PlayHealEffect()
    {
        Debug.Log("PlayHealEffect");
        AudioManager.Instance.PlaySE(AudioManager.SEType.heal);
        if (heartEffectPrefab != null)
        {
            if (hpTransform != null)
            {
                Instantiate(heartEffectPrefab, hpTransform.position, Quaternion.identity, hpTransform);
            }
            else
            {
                Instantiate(heartEffectPrefab, transform.position, Quaternion.identity, transform);
            }
        }
    }
}