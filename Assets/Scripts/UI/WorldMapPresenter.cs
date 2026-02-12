using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

public class WorldMapPresenter : MonoBehaviour
{
    // Viewへの参照（Inspectorで設定）
    [SerializeField] private WorldMapView view = null!;
    [SerializeField] private GridManager gridManager = null!;
    [SerializeField] private TurnManager turnManager = null!;
    [SerializeField] private WorldMapSO worldMapSO = null!;
    [SerializeField] private HUDFeedback mapFeedback = null!;

    // ModelはPresenterが生成、保持する
    private WorldMapModel model;

    // Input関連
    private InputAction mapAction = null!;
    private InputAction moveAction = null!;
    private InputAction selectAction = null!;

    // 状態管理
    private bool isMapOpen = false;
    public bool IsMapOpen => isMapOpen;
    private bool isAnimating = false;
    private bool isForceOpen = false;
    private Vector2Int cursorPosition;

    // 入力タイマー関連
    [SerializeField] private float initialMoveDelay = 0.2f;
    [SerializeField] private float moveRepeatRate = 0.05f;
    private float currentMoveTimer;
    private bool isHoldingMove;
    private Direction lastMoveDirection;

    private void Start()
    {
        model = new WorldMapModel(worldMapSO);
        // Input初期化
        mapAction = InputSystem.actions.FindAction("Map");
        moveAction = InputSystem.actions.FindAction("Move");
        selectAction = InputSystem.actions.FindAction("Attack");

        // View初期化
        view.Initialize();

        // 初回描画
        view.RefreshMap(model.GetAllMapData());
    }

    private void Update()
    {
        if(turnManager.IsOptionOpen || gridManager.IsEventPlaying) return;
        HandleMapToggle();

        if (!isMapOpen || isAnimating) return;

        HandleCursorMove();
        HandleSelection();
    }

    private void HandleMapToggle()
    {
        if (mapAction.triggered && !isAnimating)
        {
            if (isMapOpen)
            {
                if (isForceOpen)
                {
                    // Cannot close forced map via toggle
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
                    return;
                }

                CloseMapAsync().Forget();
                mapFeedback.PlayFeedback();
            }
            else
            {
                OpenMapAsync(false).Forget(); // Normal open
                mapFeedback.PlayFeedback();
            }
        }
    }

    public void OpenMap(bool force = false)
    {
        OpenMapAsync(force).Forget();
    }

    private async UniTaskVoid OpenMapAsync(bool force)
    {
        isAnimating = true;
        isMapOpen = true;
        isForceOpen = force;
        turnManager.SetMapState(true);

        // 開くときに最新データで更新
        view.RefreshMap(model.GetAllMapData());

        // カーソルを現在のステージの位置へ
        StageSO currentStage = gridManager.CurrentStageSO;
        if (currentStage != null)
        {
            cursorPosition = currentStage.MapPosition;
        }
        else
        {
            cursorPosition = new Vector2Int(1, 0); // ハブ
        }

        // 現在位置が無効な場合はハブへ
        if (!model.IsValidPosition(cursorPosition))
        {
            cursorPosition = new Vector2Int(1, 0); // ハブ
        }
        view.UpdateCursorPosition(cursorPosition);

        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
        await view.ShowAsync();
        isAnimating = false;
    }

    private async UniTaskVoid CloseMapAsync()
    {
        AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel); // またはselect
        isAnimating = true;
        await view.HideAsync();

        isMapOpen = false;
        turnManager.SetMapState(false);
        isAnimating = false;
    }

    private void HandleCursorMove()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        bool hasInput = moveInput.sqrMagnitude > 0.25f;

        if (!hasInput)
        {
            isHoldingMove = false;
            currentMoveTimer = 0f;
            lastMoveDirection = (Direction)(-1); // 無効な方向
            return;
        }

        Direction currentDirection = Utility.GetDirectionFromStickInput(moveInput);

        // 方向が変わったらリセット（押し直し扱い）
        if (currentDirection != lastMoveDirection)
        {
            isHoldingMove = false;
            currentMoveTimer = 0f;
            lastMoveDirection = currentDirection;
        }

        // 長押し・連打制御
        if (!isHoldingMove)
        {
            isHoldingMove = true;
            currentMoveTimer = initialMoveDelay;
            ApplyMove(currentDirection); // 初回移動
        }
        else
        {
            currentMoveTimer -= Time.deltaTime;
            if (currentMoveTimer <= 0f)
            {
                currentMoveTimer = moveRepeatRate;
                ApplyMove(currentDirection); // 連続移動
            }
        }
    }

    private void ApplyMove(Direction direction)
    {
        Vector2Int nextPos = cursorPosition;

        switch (direction)
        {
            case Direction.Right:
                nextPos = new Vector2Int(cursorPosition.x + 1, cursorPosition.y);
                break;
            case Direction.Left:
                nextPos = new Vector2Int(cursorPosition.x - 1, cursorPosition.y);
                break;
            case Direction.Up:
                nextPos = new Vector2Int(cursorPosition.x, cursorPosition.y - 1);
                // row=0がハブなので、上に行くとrow減少
                break;
            case Direction.Down:
                nextPos = new Vector2Int(cursorPosition.x, cursorPosition.y + 1);
                break;
        }

        // ハブ（row=0）からの移動特殊処理
        if (cursorPosition.y == 0)
        {
            // ハブから下へ: 各列のB1Fへ移動可能（解放されていれば）
            if (direction == Direction.Down)
            {
                // 左・中央・右のどれかに移動
                // デフォルトは中央の列
                nextPos = new Vector2Int(1, 1);
            }
            else if (direction == Direction.Left)
            {
                nextPos = new Vector2Int(0, 1);
            }
            else if (direction == Direction.Right)
            {
                nextPos = new Vector2Int(2, 1);
            }
        }
        // ダンジョンからハブへの移動
        else if (cursorPosition.y == 1 && direction == Direction.Up)
        {
            nextPos = new Vector2Int(1, 0); // ハブへ
        }

        // 有効な位置かチェック
        if (model.IsValidPosition(nextPos) && view.IsValidPosition(nextPos))
        {
            cursorPosition = nextPos;
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            view.UpdateCursorPosition(cursorPosition);
        }
    }

    private void HandleSelection()
    {
        if (selectAction.triggered)
        {
            // ハブの場合
            if (model.IsHubPosition(cursorPosition))
            {
                StageSO hubStage = model.GetStageAt(cursorPosition);
                if (hubStage != null)
                {
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
                    WarpToStageWithResetAsync(hubStage, true).Forget();
                }
                return;
            }

            // ダンジョンセルの場合
            var cellType = model.GetCellType(cursorPosition);
            if (cellType != WorldMapCellType.Unknown && model.IsTraversable(cursorPosition))
            {
                StageSO targetStage = model.GetStageAt(cursorPosition);

                if (targetStage != null)
                {
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
                    WarpToStageWithResetAsync(targetStage, false).Forget();
                }
            }
            else
            {
                AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
            }
        }
    }

    /// <summary>
    /// マップを閉じてからアニメーション付きでステージにワープ
    /// プレイヤーHP回復、Undo履歴削除。敵の位置はリセットせず保持する
    /// </summary>
    private async UniTaskVoid WarpToStageWithResetAsync(StageSO targetStage, bool isHub)
    {
        // まずマップを閉じつつワープ準備
        isAnimating = true;
        isForceOpen = false;
        turnManager.SetMapState(false);

        // ワープ先の位置を決定
        Vector2Int targetPos;
        Direction targetDir;
        if (isHub)
        {
            targetPos = targetStage.PlayerStartPosition;
            targetDir = targetStage.PlayerStartDirection;
        }
        else
        {
            // 上り階段の位置を取得
            targetPos = GridCalculation.FindStairUpPosition(targetStage);
            targetDir = targetStage.PlayerStartDirection;
        }

        // MapViewフェードアウトとステージ移動アニメーションを並行実行（HP回復もコマンド内で処理）
        await UniTask.WhenAll(
            view.HideAsync(),
            gridManager.WarpWithAnimationAsync(targetStage, targetPos, targetDir, restoreHP: true)
        );

        isMapOpen = false;
        isAnimating = false;
    }
}