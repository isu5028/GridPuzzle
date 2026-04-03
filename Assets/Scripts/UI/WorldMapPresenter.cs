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

    [Header("Input Feedback")]
    [SerializeField] private HUDFeedback moveUpFeedback;
    [SerializeField] private HUDFeedback moveDownFeedback;
    [SerializeField] private HUDFeedback moveLeftFeedback;
    [SerializeField] private HUDFeedback moveRightFeedback;
    [SerializeField] private HUDFeedback mapFrontFeedback;
    [SerializeField] private HUDFeedback mapBackFeedback;
    [SerializeField] private HUDFeedback decideFeedback; // 追加: 決定キーの色変化用

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

        HandleMapLayerSwitch();
        HandleCursorMove();
        HandleSelection();
    }

    private void HandleMapLayerSwitch()
    {
        // "MapFront": 手前へ (Layer Index Increase: Back(0-2) -> Mid(3-5) -> Front(6-8))
        // "MapBack": 奥へ (Layer Index Decrease)
        // Note: Column mapping: Back=0-2, Mid=3-5, Front=6-8.
        // Moving "Front" means adding +3 to column index.
        // Moving "Back" means subtracting 3.

        bool mapFront = InputSystem.actions.FindAction("MapFront").triggered;
        bool mapBack = InputSystem.actions.FindAction("MapBack").triggered;

        if (mapFront)
        {
            mapFrontFeedback?.PlayFeedback();
            SwitchLayer(1);
        }
        else if (mapBack)
        {
            mapBackFeedback?.PlayFeedback();
            SwitchLayer(-1);
        }
    }

    private void SwitchLayer(int direction)
    {
        int currentLayer = GetLayerFromColumn(cursorPosition.x);
        int targetLayer = currentLayer + direction;
        int unlockedLayers = GameProgressManager.Instance.GetUnlockedLayerCount();

        if (targetLayer < 0 || targetLayer >= unlockedLayers) return;

        int offset = direction * 3;
        Vector2Int newPos = new Vector2Int(cursorPosition.x + offset, cursorPosition.y);

        if (model.IsValidPosition(newPos))
        {
            cursorPosition = newPos;
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            view.UpdateCursorPosition(cursorPosition);
            UpdateDecideKeyColor(); // カーソル移動後に更新
        }
    }

    private int GetLayerFromColumn(int col)
    {
        if (col <= 2) return 0; // Back
        if (col <= 5) return 1; // Mid
        return 2; // Front
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
                // プレイヤーが死亡中はマップを開けない
                if (gridManager.PlayerScript != null && gridManager.PlayerScript.IsDead)
                {
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
                    return;
                }

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

        int unlockedLayers = GameProgressManager.Instance.GetUnlockedLayerCount();
        view.SetVisibleLayerCount(unlockedLayers);

        bool showLayerFeedback = unlockedLayers > 1;
        if (mapFrontFeedback != null) mapFrontFeedback.SetVisible(showLayerFeedback);
        if (mapBackFeedback != null) mapBackFeedback.SetVisible(showLayerFeedback);

        StageSO currentStage = gridManager.CurrentStageSO;
        if (currentStage != null)
        {
            cursorPosition = currentStage.MapPosition;
            int stageLayer = GetLayerFromColumn(cursorPosition.x);
            if (stageLayer >= unlockedLayers)
            {
                cursorPosition = new Vector2Int(1, 0);
            }
        }
        else
        {
            cursorPosition = new Vector2Int(1, 0);
        }

        if (!model.IsValidPosition(cursorPosition))
        {
            cursorPosition = new Vector2Int(1, 0);
        }
        view.UpdateCursorPosition(cursorPosition);
        UpdateDecideKeyColor(); // 開いた直後に選択位置を判定して更新

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
            PlayMoveFeedback(currentDirection);
            ApplyMove(currentDirection); // 初回移動
        }
        else
        {
            currentMoveTimer -= Time.deltaTime;
            if (currentMoveTimer <= 0f)
            {
                currentMoveTimer = moveRepeatRate;
                PlayMoveFeedback(currentDirection);
                ApplyMove(currentDirection); // 連続移動
            }
        }
    }

    private void PlayMoveFeedback(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: moveUpFeedback?.PlayFeedback(); break;
            case Direction.Down: moveDownFeedback?.PlayFeedback(); break;
            case Direction.Left: moveLeftFeedback?.PlayFeedback(); break;
            case Direction.Right: moveRightFeedback?.PlayFeedback(); break;
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
            int unlockedLayers = GameProgressManager.Instance.GetUnlockedLayerCount();
            if (direction == Direction.Right)
            {
                if (cursorPosition.x == 1 && unlockedLayers >= 2) nextPos = new Vector2Int(4, 0);
                else if (cursorPosition.x == 4 && unlockedLayers >= 3) nextPos = new Vector2Int(7, 0);
                else return;
            }
            else if (direction == Direction.Left)
            {
                if (cursorPosition.x == 7) nextPos = new Vector2Int(4, 0);
                else if (cursorPosition.x == 4) nextPos = new Vector2Int(1, 0);
                else return;
            }
        }
        // ダンジョン(B1F)からハブへの移動
        else if (cursorPosition.y == 1 && direction == Direction.Up)
        {
            if (cursorPosition.x <= 2) nextPos = new Vector2Int(1, 0);
            else if (cursorPosition.x <= 5) nextPos = new Vector2Int(4, 0);
            else nextPos = new Vector2Int(7, 0);
        }
        else if (cursorPosition.y >= 1 && (direction == Direction.Left || direction == Direction.Right))
        {
            int currentLayer = GetLayerFromColumn(cursorPosition.x);
            int nextLayer = GetLayerFromColumn(nextPos.x);
            if (currentLayer != nextLayer)
            {
                // レイヤー境界を越える場合、隣のレイヤーが解禁されていれば移動可能
                int unlockedLayers = GameProgressManager.Instance.GetUnlockedLayerCount();
                if (nextLayer >= 0 && nextLayer < unlockedLayers)
                {
                    // 隣レイヤーの同じ行の対応する列に移動
                    // Right: 現レイヤー右端(col 2,5,8) → 次レイヤー左端(col 3,6)
                    // Left:  現レイヤー左端(col 0,3,6) → 前レイヤー右端(col 2,5)
                    nextPos = new Vector2Int(nextPos.x, cursorPosition.y);
                }
                else
                {
                    return;
                }
            }
        }

        // 有効な位置かチェック
        if (model.IsValidPosition(nextPos))
        {
            cursorPosition = nextPos;
            AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            view.UpdateCursorPosition(cursorPosition);
            UpdateDecideKeyColor(); // カーソル移動後に更新
        }
    }

    // 追加: 現在のカーソル位置でワープできるかどうか判定し、決定キーのフィードバック色を変える
    private void UpdateDecideKeyColor()
    {
        if (decideFeedback == null) return;

        bool canWarp = false;
        if (model.IsHubPosition(cursorPosition))
        {
            StageSO hubStage = model.GetStageAt(cursorPosition);
            canWarp = hubStage != null && model.IsTraversable(cursorPosition);
        }
        else
        {
            var cellType = model.GetCellType(cursorPosition);
            StageSO targetStage = model.GetStageAt(cursorPosition);
            canWarp = cellType != WorldMapCellType.Unknown && model.IsTraversable(cursorPosition) && targetStage != null;
        }

        decideFeedback.SetInteractable(canWarp);
    }

    private void HandleSelection()
    {
        if (selectAction.triggered)
        {
            // ハブの場合
            if (model.IsHubPosition(cursorPosition))
            {
                StageSO hubStage = model.GetStageAt(cursorPosition);
                if (hubStage != null && model.IsTraversable(cursorPosition))
                {
                    decideFeedback?.PlayFeedback(); // 押下時にアニメーション
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);
                    WarpToStageWithResetAsync(hubStage, true).Forget();
                }
                else
                {
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
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
                    decideFeedback?.PlayFeedback(); // 押下時にアニメーション
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
    /// プレイヤーHP回復、Undo履歴削除、および全ての敵を復活させる処理が含まれる
    /// </summary>
    private async UniTaskVoid WarpToStageWithResetAsync(StageSO targetStage, bool isHub)
    {
        // まずマップを閉じつつワープ準備
        isAnimating = true;
        isForceOpen = false;
        turnManager.SetMapState(false);

        // 敵の復活処理 (ハブ・ダンジョン問わずマップからワープ時は常に復活させる)
        gridManager.ResetAllEnemies();

        // ワープ先の位置を決定
        Vector2Int targetPos;
        Direction targetDir;
        // ハブ・ダンジョン問わず、上り階段 (BrokenStairUp を含む) の位置を優先し、なければ PlayerStartPosition へ
        targetPos = GridCalculation.FindStairUpPosition(targetStage);
        targetDir = targetStage.PlayerStartDirection;

        // MapViewフェードアウトとステージ移動アニメーションを並行実行（HP回復もコマンド内で処理）
        await UniTask.WhenAll(
            view.HideAsync(),
            gridManager.WarpWithAnimationAsync(targetStage, targetPos, targetDir, restoreHP: true)
        );

        // ワープアニメーション（WarpTransitionCommandの発行）が完了した後にUndo履歴を削除する
        gridManager.ClearUndoHistory();

        isMapOpen = false;
        isAnimating = false;
    }
}