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
    private Vector2Int cursorPosition;

    // 入力タイマー関連
    [SerializeField] private float initialMoveDelay = 0.5f;
    [SerializeField] private float moveRepeatRate = 0.1f;
    private float currentMoveTimer;
    private bool isHoldingMove;

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

        if (!isMapOpen) return;

        HandleCursorMove();
        HandleSelection();
    }

    private void HandleMapToggle()
    {
        if (mapAction.triggered)
        {
            isMapOpen = !isMapOpen;
            turnManager.SetMapState(isMapOpen);
            if (isMapOpen)
            {
                // 開くときに最新データで更新
                view.RefreshMap(model.GetAllMapData());
                // カーソルを現在位置へ
                cursorPosition = model.GetCurrentPlayerPosition();
                view.UpdateCursorPosition(cursorPosition);

                view.ShowAsync().Forget();
            }
            else
            {
                view.HideAsync().Forget();
            }
        }
    }

    private void HandleCursorMove()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        bool hasInput = moveInput.sqrMagnitude > 0.25f;

        if (!hasInput)
        {
            isHoldingMove = false;
            currentMoveTimer = 0f;
            return;
        }

        // 長押し・連打制御
        if (!isHoldingMove)
        {
            isHoldingMove = true;
            currentMoveTimer = initialMoveDelay;
            ApplyMove(moveInput); // 初回移動
        }
        else
        {
            currentMoveTimer -= Time.deltaTime;
            if (currentMoveTimer <= 0f)
            {
                currentMoveTimer = moveRepeatRate;
                ApplyMove(moveInput); // 連続移動
            }
        }
    }

    private void ApplyMove(Vector2 moveInput)
    {
        int dx = 0;
        int dy = 0;
        if (moveInput.x > 0.5f) dx = 1;
        else if (moveInput.x < -0.5f) dx = -1;

        if (moveInput.y > 0.5f) dy = 1;
        else if (moveInput.y < -0.5f) dy = -1;

        // X方向トライ
        if (dx != 0)
        {
            Vector2Int nextPos = new Vector2Int(cursorPosition.x + dx, cursorPosition.y);
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
            Vector2Int nextPos = new Vector2Int(cursorPosition.x, cursorPosition.y + dy);
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
                    CloseMapAndResume();
                }
            }
            else
            {
                AudioManager.Instance?.PlaySE(AudioManager.SEType.cancel);
            }
        }
    }

    private void CloseMapAndResume()
    {
        // マップを閉じる
        isMapOpen = false;
        turnManager.SetMapState(false);
        view.HideAsync().Forget();
    }
}