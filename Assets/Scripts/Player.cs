using UnityEngine;
using UnityEngine.InputSystem;

using Cysharp.Threading.Tasks;
using DG.Tweening;

public class Player : Unit
{
    private InputAction inputMove = null!;
    private InputAction inputAttack = null!;
    private InputAction inputStay = null!;
    private InputAction inputUndo = null!;
    private InputAction inputReset = null!;

    private bool isProcessingAction = false;

    // Undo長押し用の変数
    private float undoHoldTime = 0f;
    private float undoInitialDelay = 0.5f; // 最初のUndo後の待機時間
    private float undoRepeatDelay = 0.25f; // 連続Undo時の間隔
    private float undoNextTriggerTime = 0f;

    private bool isInputBlocked = false;

    protected override void Awake()
    {
        base.Awake();
        inputMove = InputSystem.actions.FindAction("Move");
        inputAttack = InputSystem.actions.FindAction("Attack");
        inputStay = InputSystem.actions.FindAction("Stay");
        inputUndo = InputSystem.actions.FindAction("Undo");
        inputReset = InputSystem.actions.FindAction("Reset");
    }

    public void WarpToNewStage(Vector2Int newPos, Direction newDir)
    {
        currentPos = newPos;
        currentDirection = newDir;
        unitAnimator.SetDirection(newDir);

        // 座標更新
        transform.position = gridManager.GridToWorldPosition(newPos);
        BlockInputTemporarily().Forget();
    }

    private async UniTaskVoid BlockInputTemporarily()
    {
        isInputBlocked = true;
        // 0.2秒待つ（フェード演出などがあるならその時間分待つ）
        await UniTask.Delay(100);
        isInputBlocked = false;
    }

    private void Update()
    {
        if (!turnManager.IsPlayerTurn) return;
        if (isInputBlocked) return;
        if (isProcessingAction) return;
        if (turnManager.IsOptionOpen) return;
        if (gridManager.IsTransitioning) return;
        if (isMoving) return;
        if (turnManager.IsMapOpen) return;

        if (inputUndo.IsPressed())
        {
            if (inputUndo.triggered)
            {
                // 最初の押下
                turnManager.UndoTurn().Forget();
                undoHoldTime = Time.time;
                undoNextTriggerTime = Time.time + undoInitialDelay;
            }
            else if (Time.time >= undoNextTriggerTime)
            {
                // 長押し中の連続Undo
                turnManager.UndoTurn().Forget();
                undoNextTriggerTime = Time.time + undoRepeatDelay;
            }
            return;
        }
        else
        {
            undoHoldTime = 0f;
        }

        if (inputReset.triggered)
        {
            turnManager.ResetTurn();
            return;
        }

        if (isDead) return;
        var inputValue = inputMove.ReadValue<Vector2>();

        if (inputValue != Vector2.zero)
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
                // 次のステージを取得
                StageSO nextStage = gridManager.CurrentStageSO.GetAdjacentStage(newDirection);

                // 次のステージが存在する場合のみ実行
                if (nextStage != null)
                {
                    var newPos = currentPos + Utility.GetVectorFromDirection(newDirection);
                    var moveCommand = new MoveCommand(this, currentPos, newPos, currentDirection, newDirection);
                    commandManager.ExecuteCommand(moveCommand);
                    gridManager.TryTransition(newDirection);
                    return;
                }

                return; // 移動処理はせず終了
            }

            isProcessingAction = true;
            TryMoveAsync(currentDirection).Forget();
        }

        if (inputAttack.triggered)
        {
            turnManager.EnsureTurnStartSnapshot();
            isProcessingAction = true;
            TryAttack();
        }
    }

    private async UniTaskVoid TryMoveAsync(Direction direction)
    {
        bool didMove = await TryMove(direction);

        if (!didMove)
        {
            // 移動しなかった（攻撃や移動失敗）場合は何もしない
            return;
        }

        // GameSpeed == 0 (Normal) のときは移動完了を待つ
        if (GameSettings.GameSpeed == 0)
        {
            await UniTask.WaitUntil(() => !isMoving);
        }

        OnActionComplete();
    }

    protected override async void OnActionComplete()
    {
        turnManager.OnPlayerActionComplete().Forget();
        isProcessingAction = false;
    }

    protected override void OnMoveFailed()
    {
        // 移動失敗時はターンを消費せず、再び操作可能にする
        isProcessingAction = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }
}