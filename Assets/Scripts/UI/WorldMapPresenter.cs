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
        if(turnManager.IsOptionOpen) return;
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
                CloseMapAsync().Forget();
            }
            else
            {
                OpenMapAsync().Forget();
            }
        }
    }

    private async UniTaskVoid OpenMapAsync()
    {
        isAnimating = true;
        isMapOpen = true;
        turnManager.SetMapState(true);

        // 開くときに最新データで更新
        view.RefreshMap(model.GetAllMapData());
        // カーソルを現在位置へ
        cursorPosition = model.GetCurrentPlayerPosition();
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
        int dx = 0;
        int dy = 0;

        switch (direction)
        {
            case Direction.Right: dx = 1; break;
            case Direction.Left: dx = -1; break;
            case Direction.Up: dy = 1; break;
            case Direction.Down: dy = -1; break;
        }

        int mapSize = model.MapSize;

        // X方向トライ
        if (dx != 0)
        {
            Vector2Int nextPos = new Vector2Int((cursorPosition.x + dx + mapSize) % mapSize, cursorPosition.y);
            // ★Modelに問い合わせる（Unknownチェックなど）
            if (model.IsTraversable(nextPos))
            {
                cursorPosition = nextPos;
                AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            }
        }

        // Y方向トライ
        if (dy != 0)
        {
            // X移動後の座標を基準にするか、XY独立か。現在のロジックではX移動後のcursorPositionを使っているのでそれに従う
            Vector2Int nextPos = new Vector2Int(cursorPosition.x, (cursorPosition.y + dy + mapSize) % mapSize);
            // ★Modelに問い合わせる
            if (model.IsTraversable(nextPos))
            {
                cursorPosition = nextPos;
                if (dx == 0) AudioManager.Instance?.PlaySE(AudioManager.SEType.move);
            }
        }

        // Viewへ反映
        view.UpdateCursorPosition(cursorPosition);
    }

    private void HandleSelection()
    {
        if (selectAction.triggered)
        {
            // Unknownでなければ選択可能
            if (model.GetCellType(cursorPosition) != WorldMapCellType.Unknown)
            {
                // 1. Modelからステージデータを取得
                StageSO targetStage = model.GetStageAt(cursorPosition);

                if (targetStage != null)
                {
                    Debug.Log($"Warping to: {targetStage.name}");
                    AudioManager.Instance?.PlaySE(AudioManager.SEType.decide);

                    // 2. GridManagerを使ってワープ
                    gridManager.WarpToStage(targetStage);

                    // 3. マップを閉じる
                    // 閉じる処理を待たずにゲーム再開させるならForget、演出を待つならawait
                    // 3. マップを閉じる
                    // 閉じる処理を待たずにゲーム再開させるならForget、演出を待つならawait
                    CloseMapAndResumeAsync().Forget();
                }
            }
            else
            {
                AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
            }
        }
    }

    private async UniTaskVoid CloseMapAndResumeAsync()
    {
        // マップを閉じる
        isAnimating = true;

        await view.HideAsync();

        isMapOpen = false;
        turnManager.SetMapState(false);
        isAnimating = false;
    }
}