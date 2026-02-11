using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UniRx;

[RequireComponent(typeof(GridVisualizer))]
[RequireComponent(typeof(UnitManager))]
public class GridManager : MonoBehaviour
{
    [SerializeField] private TextPopUpEach popup = null!;
    [SerializeField] private ShowWin showWin = null!;
    [SerializeField] private SaveFeedback saveFeedback = null!;
    [SerializeField] private WorldMapSO worldMapSO = null!;
    public WorldMapSO WorldMapSO => worldMapSO;
    [SerializeField] private StageSO[] stageSOs = null!;
    private StageSO initialStageSO = null!;
    private StageSO currentStageSO = null!;
    public StageSO CurrentStageSO => currentStageSO;
    private Dictionary<StageSO, List<Enemy>> stageEnemiesMap = new Dictionary<StageSO, List<Enemy>>();
    // ロード時に復元されたがまだ実体化されていない敵データ
    private Dictionary<StageSO, List<EnemySaveData>> loadedStageEnemyData = new Dictionary<StageSO, List<EnemySaveData>>();

    // Destroyed Walls: Stage -> Set of positions
    private Dictionary<StageSO, HashSet<Vector2Int>> destroyedWalls = new Dictionary<StageSO, HashSet<Vector2Int>>();

    [Header("Visualizer")]
    [SerializeField] private GridVisualizer gridVisualizer = null!;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab = null!;
    [SerializeField] private GameObject slimeEnemyPrefab = null!;
    [SerializeField] private GameObject goblinEnemyPrefab = null!;
    [SerializeField] private GameObject skeletonEnemyPrefab = null!;
    [SerializeField] private GameObject zombieEnemyPrefab = null!;
    [SerializeField] private GameObject boarEnemyPrefab = null!;
    [SerializeField] private GameObject crystalEnemyPrefab = null!;
    [SerializeField] private GameObject errorEffectPrefab = null!;

    private CellType[,] cellData = new CellType[0, 0];
    private int width;
    private int height;

    // UnitManagerへの参照
    private UnitManager unitManager = null!;
    public void SortEnemies()
    {
        unitManager.SortEnemiesByOrder();
    }

    public UnitManager UnitManager => unitManager;

    // 後方互換性プロパティ (他クラスへの影響を最小限にするため)
    public GameObject PlayerObject => unitManager.PlayerObject;
    public List<GameObject> EnemyObjects => unitManager.EnemyObjects;
    public Player PlayerScript => unitManager.Player;
    public List<Enemy> EnemyScripts => unitManager.Enemies;
    public GridVisualizer Visualizer => gridVisualizer;

    private TurnManager turnManager = null!;
    private CommandManager commandManager = null!;
    private bool isTransitioning = false;
    public bool IsTransitioning => isTransitioning;

    // Undo時の敵インスタンス重複を防ぐための待機リスト
    private Dictionary<StageSO, List<Enemy>> pendingStageEnemies = new Dictionary<StageSO, List<Enemy>>();

    public void AddPendingEnemy(StageSO stage, Enemy enemy)
    {
        if (!pendingStageEnemies.ContainsKey(stage)) pendingStageEnemies[stage] = new List<Enemy>();
        pendingStageEnemies[stage].Add(enemy);
    }


    private const float TransitionDuration = 0.2f;
    private const int TransitionDelayMs = 250;

    private Vector2Int currentResetMapPos;

    [SerializeField] private ShowWipeOut showWipeOut = null!;
    [SerializeField] private TransitionOut transitionOut = null!;

    [SerializeField] private End end = null!;
    [SerializeField] private WorldMapPresenter worldMapPresenter = null!;
    [SerializeField] private StairConfirmationPresenter stairConfirmationPresenter = null!;

    private bool isEventPlaying = false;
    private bool isWaitingForTitleInput = false;
    public bool IsEventPlaying => isEventPlaying;
    public bool IsStairDialogOpen => stairConfirmationPresenter != null && stairConfirmationPresenter.IsOpen;

    public bool IsPositionInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void Start()
    {
        if (gridVisualizer == null)
            gridVisualizer = GetComponent<GridVisualizer>();

        gridVisualizer.OnSyncMapActivated += HandleSyncMapActivated;

        if (showWipeOut == null) showWipeOut = FindObjectOfType<ShowWipeOut>();
        if (transitionOut == null) transitionOut = FindObjectOfType<TransitionOut>();
        if (end == null) end = FindObjectOfType<End>();
        if (worldMapPresenter == null) worldMapPresenter = FindObjectOfType<WorldMapPresenter>();

        if (showWipeOut != null)
        {
            showWipeOut.OnHide += HandleWipeOutHidden;
        }

        unitManager = GetComponent<UnitManager>();
        turnManager = GetComponent<TurnManager>();
        commandManager = GetComponent<CommandManager>();

        // UnitManagerのイベント購読
        unitManager.OnUnitStateChanged
             .Subscribe(_ => OnEnemyStateChanged())
             .AddTo(this);

        // debug
        currentStageSO = stageSOs[0];

        GameProgressManager.Instance.InitEntireProgress();

        GameProgressManager.SaveType loadType = GameProgressManager.Instance.NextLoadType ?? GameProgressManager.SaveType.Auto;
        GameProgressManager.Instance.NextLoadType = null; // Reset

        ContinueGame(loadType);
    }

    private void HandleSyncMapActivated()
    {
        PlayEndingSequence().Forget();
    }

    private async UniTaskVoid PlayEndingSequence()
    {
        isEventPlaying = true;

        if (showWipeOut != null)
        {
            showWipeOut.Show();
            AudioManager.Instance.PlaySE(AudioManager.SEType.wipeout);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(2.0f));

        if (transitionOut != null)
        {
            await transitionOut.AnimateAsync();
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));

        if (end != null)
        {
            end.Show();
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));

        isWaitingForTitleInput = true;
    }

    private void HandleWipeOutHidden()
    {
        // isEventPlaying = false;
    }

    private void OnDestroy()
    {
        if (gridVisualizer != null)
        {
            gridVisualizer.OnSyncMapActivated -= HandleSyncMapActivated;
        }

        if (showWipeOut != null)
        {
            showWipeOut.OnHide -= HandleWipeOutHidden;
        }
    }

    void Update()
    {
        if (isWaitingForTitleInput)
        {
            var attackAction = UnityEngine.InputSystem.InputSystem.actions.FindAction("Attack");
            if (attackAction != null && attackAction.triggered)
            {
                SceneManager.LoadScene("Title");
            }
            return;
        }

        if (isEventPlaying) return;

#if UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.L))
        {
            GameProgressManager.Instance.DeleteSaveFile(GameProgressManager.SaveType.Manual);
            GameProgressManager.Instance.DeleteSaveFile(GameProgressManager.SaveType.Auto);
            Debug.Log("Game progress reset.");
        }

        if(Input.GetKeyDown(KeyCode.F12))
        {
            GameProgressManager.Instance.DebugUnlockAllStages(worldMapSO);
            SyncWorldMapStatus(); // Refresh visuals immediately
            gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0f, true);
        }
#endif
    }

    public void Reset()
    {
        isEventPlaying = false;
        if (showWipeOut != null) showWipeOut.Hide();

        if (initialStageSO == null)
        {
            initialStageSO = worldMapSO.GetStageAt(currentResetMapPos.x, currentResetMapPos.y);
        }

        gridVisualizer.ClearAllTiles();
        GameProgressManager.Instance.ResetStageCleared();

        unitManager.ClearAllUnits(); // Destroys Player and current active enemies
        CleanupAllEnemies(); // Destroys any remaining enemies (inactive/other stages) and clears map
        destroyedWalls.Clear(); // Return walls to normal on reset

        currentStageSO = initialStageSO;
        gridVisualizer.ResetGridParentPosition();
        LoadStage(initialStageSO, true, currentStageSO.PlayerStartPosition);

        if(unitManager.Player != null)
        {
            unitManager.Player.WarpToNewStage(currentStageSO.PlayerStartPosition, currentStageSO.PlayerStartDirection);
        }
    }

    public async UniTask SwitchStageAsync(StageSO nextstage, Direction entryDirection)
    {
        // 次のステージの遷移準備
        HashSet<Vector2Int> destroyed = destroyedWalls.ContainsKey(nextstage) ? destroyedWalls[nextstage] : null;

        gridVisualizer.SetupTransitionTilemap(nextstage, entryDirection, destroyed, GameProgressManager.Instance.IsPositionCleared);

        var dirVector = Utility.GetVectorFromDirection(entryDirection);
        Vector3 transitionOffset = (Vector3)(dirVector * gridVisualizer.TilemapOffset * 2);

        // Prepare ACTUAL enemies for the next stage
        PrepareEnemiesForTransition(nextstage, transitionOffset);

        gridVisualizer.MoveGridParent(transitionOffset, TransitionDuration);

        await UniTask.Delay(TransitionDelayMs);
    }

    private void PrepareEnemiesForTransition(StageSO nextstage, Vector3 transitionOffset)
    {
        // 1. Ensure enemies are loaded in memory
        EnsureStageEnemiesLoaded(nextstage);

        // 2. Get the list of enemies
        List<Enemy> enemies = stageEnemiesMap[nextstage];

        // 3. Set them active and offset their position
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];

            // Safety Check: Remove null/destroyed enemies from list
            if (enemy == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            // Set position relative to the visually offset grid
            // GridToWorldPosition returns the "standard" world pos for that grid coord.
            // We subtract transitionOffset so they appear in the sliding-in map.
            Vector3 worldPos = GridToWorldPosition(enemy.CurrentPos);
            enemy.transform.position = worldPos - transitionOffset;

            // Activate
            enemy.gameObject.SetActive(true);

            // Visual updates
            enemy.UnitAnimator.SetDirection(enemy.CurrentDirection);
            if (enemy.IsDead)
            {
                enemy.ApplyDeadStateVisual();
            }
        }
    }

    private void CleanupAllEnemies()
    {
        foreach (var list in stageEnemiesMap.Values)
        {
            foreach (var enemy in list)
            {
                if (enemy != null) Destroy(enemy.gameObject);
            }
        }
        stageEnemiesMap.Clear();
    }

    public void WarpToStage(StageSO stage)
    {
        WarpToStageAsync(stage).Forget();
    }

    /// <summary>
    /// ステージの初期位置にワープする（アニメーション付き）
    /// </summary>
    public async UniTask WarpToStageAsync(StageSO stage)
    {
        if (stage == null) return;

        isTransitioning = true;

        // 現在のステージと同じ場合はアニメーションなしでリロード
        if (stage == currentStageSO)
        {
            Vector2Int targetPos = stage.PlayerStartPosition;

            CleanupAllEnemies();
            destroyedWalls.Clear();
            GameProgressManager.Instance.ResetStageCleared();

            if (unitManager.Player != null)
            {
                unitManager.Player.Revive();
                unitManager.Player.SetHP(unitManager.Player.MaxHP);
            }

            currentStageSO = stage;
            initialStageSO = stage;
            commandManager.Warped();
            currentResetMapPos = stage.MapPosition;

            gridVisualizer.ResetGridParentPosition();
            LoadStage(stage, true, targetPos);

            if (unitManager.Player != null)
            {
                unitManager.Player.WarpToNewStage(targetPos, stage.PlayerStartDirection);
            }

            SaveGame(GameProgressManager.SaveType.Auto);
            SyncWorldMapStatus();

            isTransitioning = false;
            return;
        }

        // 現在のステージ情報を取得
        Vector2Int currentMapPos = currentStageSO != null ? currentStageSO.MapPosition : new Vector2Int(1, 0);
        Vector2Int targetMapPos = stage.MapPosition;

        // 移動方向を決定
        WarpDirection warpDir = DetermineWarpDirection(currentMapPos, targetMapPos);

        // ワープ先位置
        Vector2Int targetPos2 = stage.PlayerStartPosition;

        // ===== 1. プレイヤーをフェードアウト =====
        if (unitManager.Player != null)
        {
            bool goingDown = (warpDir == WarpDirection.Down);
            await unitManager.Player.FadeOutAsync(0.15f, goingDown);
        }

        // ===== 2. 画面遷移の準備 =====
        CleanupAllEnemies();
        destroyedWalls.Clear();
        GameProgressManager.Instance.ResetStageCleared();

        if (unitManager.Player != null)
        {
            unitManager.Player.Revive();
            unitManager.Player.SetHP(unitManager.Player.MaxHP);
        }

        // 過渡先の準備
        HashSet<Vector2Int> destroyed = destroyedWalls.ContainsKey(stage) ? destroyedWalls[stage] : null;

        // ワープ遷移用タイルマップの準備
        gridVisualizer.SetupWarpTransitionTilemap(stage, warpDir, targetPos2, destroyed, GameProgressManager.Instance.IsPositionCleared);

        // 遷移先ステージの敵を準備
        Vector3 transitionOffset = gridVisualizer.GetWarpTransitionOffset(warpDir);
        PrepareEnemiesForTransition(stage, transitionOffset);

        // 現在のステージの敵を非表示に
        unitManager.SetAllEnemiesActive(false);

        // プレイヤーも移動アニメーションに追従させる
        if (unitManager.Player != null)
        {
            unitManager.Player.gameObject.transform.SetParent(gridVisualizer.GridParent);
        }

        // ===== 3. ステージ遷移アニメーション =====
        AudioManager.Instance?.PlaySE(AudioManager.SEType.warp);
        gridVisualizer.MoveGridParent(transitionOffset, TransitionDuration);
        await UniTask.Delay(TransitionDelayMs);

        // ===== 4. ステージをロード =====
        currentStageSO = stage;
        initialStageSO = stage;
        commandManager.Warped();
        currentResetMapPos = stage.MapPosition;

        // プレイヤーの親を戻す
        if (unitManager.Player != null)
        {
            unitManager.Player.gameObject.transform.SetParent(unitManager.transform);
        }

        // ステージロード
        LoadStage(stage, true, targetPos2);

        // ===== 5. プレイヤーをフェードイン =====
        if (unitManager.Player != null)
        {
            bool goingDown = (warpDir == WarpDirection.Down);
            await unitManager.Player.FadeInAsync(0.2f, goingDown);
        }

        SaveGame(GameProgressManager.SaveType.Auto);
        SyncWorldMapStatus();

        isTransitioning = false;
    }

    /// <summary>
    /// ステージの上り階段の位置にワープする（アニメーションなし）
    /// </summary>
    public void WarpToStageAtStairUp(StageSO stage)
    {
        WarpToStageAtStairUpAsync(stage).Forget();
    }

    /// <summary>
    /// ステージの上り階段の位置にワープする（アニメーション付き）
    /// </summary>
    public async UniTask WarpToStageAtStairUpAsync(StageSO stage)
    {
        if (stage == null) return;

        isTransitioning = true;

        // 上り階段の位置を探す
        Vector2Int stairUpPos = FindStairUpPosition(stage);

        // 現在のステージと同じ場合はアニメーションなしでリロード
        if (stage == currentStageSO)
        {
            CleanupAllEnemies();
            destroyedWalls.Clear();
            GameProgressManager.Instance.ResetStageCleared();

            if (unitManager.Player != null)
            {
                unitManager.Player.Revive();
                unitManager.Player.SetHP(unitManager.Player.MaxHP);
            }

            currentStageSO = stage;
            initialStageSO = stage;
            commandManager.Warped();
            currentResetMapPos = stage.MapPosition;

            gridVisualizer.ResetGridParentPosition();
            LoadStage(stage, false, stairUpPos, stage.PlayerStartDirection);

            if (unitManager.Player != null)
            {
                unitManager.Player.WarpToNewStage(stairUpPos, stage.PlayerStartDirection);
            }

            SaveGame(GameProgressManager.SaveType.Auto);
            SyncWorldMapStatus();

            isTransitioning = false;
            return;
        }

        // 現在のステージ情報を取得
        Vector2Int currentMapPos = currentStageSO != null ? currentStageSO.MapPosition : new Vector2Int(1, 0);
        Vector2Int targetMapPos = stage.MapPosition;

        // 移動方向を決定
        WarpDirection warpDir = DetermineWarpDirection(currentMapPos, targetMapPos);

        // ===== 1. プレイヤーをフェードアウト =====
        if (unitManager.Player != null)
        {
            bool goingDown = (warpDir == WarpDirection.Down);
            await unitManager.Player.FadeOutAsync(0.15f, goingDown);
        }

        // ===== 2. 画面遷移の準備 =====
        CleanupAllEnemies();
        destroyedWalls.Clear();
        GameProgressManager.Instance.ResetStageCleared();

        if (unitManager.Player != null)
        {
            unitManager.Player.Revive();
            unitManager.Player.SetHP(unitManager.Player.MaxHP);
        }

        // 過渡先の準備
        HashSet<Vector2Int> destroyed = destroyedWalls.ContainsKey(stage) ? destroyedWalls[stage] : null;

        // ワープ遷移用タイルマップの準備
        gridVisualizer.SetupWarpTransitionTilemap(stage, warpDir, stairUpPos, destroyed, GameProgressManager.Instance.IsPositionCleared);

        // 遷移先ステージの敵を準備
        Vector3 transitionOffset = gridVisualizer.GetWarpTransitionOffset(warpDir);
        PrepareEnemiesForTransition(stage, transitionOffset);

        // 現在のステージの敵を非表示に
        unitManager.SetAllEnemiesActive(false);

        // プレイヤーも移動アニメーションに追従させる
        if (unitManager.Player != null)
        {
            unitManager.Player.gameObject.transform.SetParent(gridVisualizer.GridParent);
        }

        // ===== 3. ステージ遷移アニメーション =====
        AudioManager.Instance?.PlaySE(AudioManager.SEType.warp);
        gridVisualizer.MoveGridParent(transitionOffset, TransitionDuration);
        await UniTask.Delay(TransitionDelayMs);

        // ===== 4. ステージをロード =====
        currentStageSO = stage;
        initialStageSO = stage;
        commandManager.Warped();
        currentResetMapPos = stage.MapPosition;

        // プレイヤーの親を戻す
        if (unitManager.Player != null)
        {
            unitManager.Player.gameObject.transform.SetParent(unitManager.transform);
        }

        // ステージロード
        LoadStage(stage, false, stairUpPos, stage.PlayerStartDirection);

        // ===== 5. プレイヤーをフェードイン =====
        if (unitManager.Player != null)
        {
            bool goingDown = (warpDir == WarpDirection.Down);
            await unitManager.Player.FadeInAsync(0.2f, goingDown);
        }

        SaveGame(GameProgressManager.SaveType.Auto);
        SyncWorldMapStatus();

        isTransitioning = false;
    }

    public enum WarpDirection
    {
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Same
    }

    private WarpDirection DetermineWarpDirection(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;  // 正: 右, 負: 左
        int dy = to.y - from.y;  // 正: 下(深い階), 負: 上(浅い階)

        // 斜め方向
        if (dy > 0 && dx < 0) return WarpDirection.DownLeft;   // 下の階・左
        if (dy > 0 && dx > 0) return WarpDirection.DownRight;  // 下の階・右
        if (dy < 0 && dx < 0) return WarpDirection.UpLeft;     // 上の階・左
        if (dy < 0 && dx > 0) return WarpDirection.UpRight;    // 上の階・右

        // 垂直方向のみ
        if (dy > 0) return WarpDirection.Down;
        if (dy < 0) return WarpDirection.Up;

        // 水平方向のみ
        if (dx > 0) return WarpDirection.Right;
        if (dx < 0) return WarpDirection.Left;

        return WarpDirection.Same;
    }

    /// <summary>
    /// 階段移動用のワープ方向を決定
    /// </summary>
    private WarpDirection DetermineStairWarpDirection(StageSO fromStage, StageSO toStage, bool goingDown)
    {
        if (fromStage == null || toStage == null)
        {
            return goingDown ? WarpDirection.Down : WarpDirection.Up;
        }

        // 階段の列（StairColumn）を使用して水平方向を決定
        int fromColumn = fromStage.StairColumn;
        int toColumn = toStage.StairColumn;

        // ハブ（中央列=1）からの移動の場合
        if (fromStage.MapPosition.y == 0)
        {
            // ハブ→ダンジョン
            if (toColumn == 0) return WarpDirection.DownLeft;   // 左の階段
            if (toColumn == 2) return WarpDirection.DownRight;  // 右の階段
            return WarpDirection.Down;  // 中央の階段
        }

        // ダンジョン→ハブの場合
        if (toStage.MapPosition.y == 0)
        {
            if (fromColumn == 0) return WarpDirection.UpRight;  // 左列からハブへ
            if (fromColumn == 2) return WarpDirection.UpLeft;   // 右列からハブへ
            return WarpDirection.Up;  // 中央列からハブへ
        }

        // 同じ列内での移動（通常の階段移動）
        if (fromColumn == toColumn)
        {
            return goingDown ? WarpDirection.Down : WarpDirection.Up;
        }

        // 異なる列への移動（通常はないが念のため）
        int dx = toColumn - fromColumn;
        if (goingDown)
        {
            if (dx < 0) return WarpDirection.DownLeft;
            if (dx > 0) return WarpDirection.DownRight;
            return WarpDirection.Down;
        }
        else
        {
            if (dx < 0) return WarpDirection.UpLeft;
            if (dx > 0) return WarpDirection.UpRight;
            return WarpDirection.Up;
        }
    }

    private Vector2Int FindStairUpPosition(StageSO stage)
    {
        if (stage == null) return stage.PlayerStartPosition;

        // ステージ内でStairUpセルを探す
        for (int y = 0; y < stage.Height; y++)
        {
            for (int x = 0; x < stage.Width; x++)
            {
                if (GetCellTypeAt(stage, new Vector2Int(x, y)) == CellType.StairUp)
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        // 見つからなければ初期位置
        return stage.PlayerStartPosition;
    }

    /// <summary>
    /// Undo履歴をクリアする
    /// </summary>
    public void ClearUndoHistory()
    {
        commandManager.Warped();
    }

    /// <summary>
    /// 全ステージの敵をリセット（初期位置に戻す）
    /// </summary>
    public void ResetAllEnemies()
    {
        CleanupAllEnemies();
        loadedStageEnemyData.Clear();
        pendingStageEnemies.Clear();
    }

    /// <summary>
    /// プレイヤーのHPを最大まで回復する
    /// </summary>
    public void RestorePlayerHP()
    {
        if (unitManager.Player != null)
        {
            unitManager.Player.Revive();
            unitManager.Player.SetHP(unitManager.Player.MaxHP);
        }
    }

    /// <summary>
    /// 階段の上にいるかチェックし、いれば確認ダイアログを表示する
    /// </summary>
    /// <returns>ダイアログを表示した場合はtrue</returns>
    public bool CheckAndShowStairDialog()
    {
        if (currentStageSO == null || unitManager.Player == null || worldMapSO == null)
        {
            return false;
        }

        Vector2Int playerPos = unitManager.Player.CurrentPos;
        CellType cellType = GetCellTypeAt(currentStageSO, playerPos);

        // StairUpまたはStairDownかチェック
        if (cellType != CellType.StairUp && cellType != CellType.StairDown)
        {
            return false;
        }

        StageSO targetStage = null;

        // 1. まず現在のステージのStairConnectionsを確認（位置ベースの個別設定）
        targetStage = currentStageSO.GetConnectedStage(playerPos);

        // 2. 設定がなければ、ステージ全体のデフォルト接続（Up/Down）を使用
        if (targetStage == null)
        {
            if (cellType == CellType.StairUp)
            {
                targetStage = currentStageSO.DefaultUpStage;
            }
            else if (cellType == CellType.StairDown)
            {
                targetStage = currentStageSO.DefaultDownStage;
            }
        }

        // 3. それでもなければ、従来のフロアインデックスベースの遷移を使用（フォールバック）
        if (targetStage == null && worldMapSO != null)
        {
            // 移動先フロアインデックスを計算
            int currentFloor = currentStageSO.FloorIndex;
            int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;

            // 移動先ステージを取得
            targetStage = worldMapSO.GetStageByFloorIndex(targetFloor);

            if (targetStage == null)
            {
                Debug.Log($"[Stair] No stage found for {cellType} (Wait for assignment or floor index match)");
                return false;
            }
        }

        int targetFloorIndex = targetStage.FloorIndex;
        Debug.Log($"[Stair] {cellType}: Floor {currentStageSO.FloorIndex} -> {targetFloorIndex} ({targetStage.StageName})");

        // 到着先に敵がいる場合、プレイヤーの向きに押し出し可能かチェック
        // プレイヤーは同じ座標に出現するので、playerPosをチェック
        Direction playerDir = unitManager.Player.CurrentDirection;
        if (IsPositionOccupiedInStage(targetStage, playerPos))
        {
            if (!CanPushChainInDirection(targetStage, playerPos, playerDir))
            {
                Debug.Log($"[Stair] Cannot take stairs: enemy at destination cannot be pushed");
                return false;
            }
        }

        // 確認ダイアログを表示
        ShowStairConfirmationDialog(cellType, targetStage);
        return true;
    }

    /// <summary>
    /// 階段確認ダイアログを表示する
    /// </summary>
    private void ShowStairConfirmationDialog(CellType stairType, StageSO targetStage)
    {
        if (stairConfirmationPresenter == null)
        {
            Debug.LogError("[Stair] stairConfirmationPresenter is NULL! Please assign it in the Inspector.");
            return;
        }

        // 表示名を生成（例: "B1F", "1F"など）
        string displayName = GetFloorDisplayName(targetStage.FloorIndex);
        bool goingDown = stairType == CellType.StairDown;

        stairConfirmationPresenter.ShowAsync(stairType, targetStage, displayName, (confirmed) =>
        {
            if (confirmed)
            {
                // 移動する
                TakeStairAsync(targetStage, goingDown).Forget();
            }
            else
            {
                // 移動しない（ここから敵のターンへ進める）
                // プレイヤーの行動完了処理を呼び出す（プレイヤーの状態リセットも含む）
                if (unitManager.Player != null)
                {
                    unitManager.Player.ForceActionComplete();
                }
            }
        }).Forget();
    }

    /// <summary>
    /// フロアインデックスから表示名を生成
    /// </summary>
    private string GetFloorDisplayName(int floorIndex)
    {
        if (floorIndex == 0) return "1F";
        if (floorIndex > 0) return $"B{floorIndex}F";  // 地下
        return $"{-floorIndex + 1}F";  // 地上（負のインデックスの場合）
    }

    /// <summary>
    /// 階段を使ってステージを移動する（遷移アニメーション付き）
    /// 階段移動では同じグリッド座標を維持する
    /// </summary>
    private async UniTask TakeStairAsync(StageSO targetStage, bool goingDown)
    {
        if (targetStage == null) return;

        isTransitioning = true;

        // プレイヤーの現在位置を取得（階段移動では同じ座標を維持する）
        Vector2Int currentPlayerPos = unitManager.Player != null ? unitManager.Player.CurrentPos : Vector2Int.zero;
        Direction currentDirection = unitManager.Player != null ? unitManager.Player.CurrentDirection : Direction.Right;

        // 次のステージでも同じ座標に出現するので、オフセットは0
        Vector2Int targetPlayerPos = currentPlayerPos;

        // 階段のワープ方向を決定
        WarpDirection stairWarpDir = DetermineStairWarpDirection(currentStageSO, targetStage, goingDown);

        // 次のステージの遷移準備
        HashSet<Vector2Int> destroyed = destroyedWalls.ContainsKey(targetStage) ? destroyedWalls[targetStage] : null;

        // チェイン押し出し処理をPrepareEnemiesForTransitionの前に実行
        // （PrepareEnemiesForTransition内のEnsureStageEnemiesLoadedがloadedStageEnemyDataを消費するため）
        List<(Vector2Int from, Vector2Int to)> pushMoves = null;
        if (IsPositionOccupiedInStage(targetStage, targetPlayerPos))
        {
            var pushInfo = GetChainPushInfo(targetStage, targetPlayerPos, currentDirection);
            if (pushInfo != null && pushInfo.Value.canPush)
            {
                pushMoves = pushInfo.Value.pushMoves;
                PushChainInStageData(targetStage, pushMoves);
            }
        }

        // ===== 1. プレイヤーをフェードアウト（短め）=====
        if (unitManager.Player != null)
        {
            await unitManager.Player.FadeOutAsync(0.15f, goingDown);
        }

        // 階段遷移用のトランジション準備（ワープ方向を使用）
        gridVisualizer.SetupStairTransitionTilemapWithDirection(targetStage, stairWarpDir, currentPlayerPos, targetPlayerPos, destroyed, GameProgressManager.Instance.IsPositionCleared);

        // 遷移先ステージの敵を準備
        Vector3 transitionOffset = gridVisualizer.GetWarpTransitionOffset(stairWarpDir);
        PrepareEnemiesForTransition(targetStage, transitionOffset);

        // 現在のステージの敵を非表示に
        unitManager.SetAllEnemiesActive(false);

        // プレイヤーも移動アニメーションに追従させる
        if (unitManager.Player != null)
        {
            unitManager.Player.gameObject.transform.SetParent(gridVisualizer.GridParent);
        }

        // ===== 2. ステージ遷移アニメーション =====
        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
        gridVisualizer.MoveGridParent(transitionOffset, TransitionDuration);
        await UniTask.Delay(TransitionDelayMs);

        // ===== 2. ステージ遷移（コマンド化によるUndo対応）=====

        // プレイヤーの親を元に戻す（コマンド内でやるか、ここでやるか。コマンド内で一貫させる）

        var cmd = new PlayerStairTransitionCommand(this, currentStageSO, targetStage, currentPlayerPos, currentDirection, targetPlayerPos, currentDirection, pushMoves);
        commandManager.ExecuteCommand(cmd);

        // ===== 3. プレイヤーをフェードイン（短め）=====
        if (unitManager.Player != null)
        {
            await unitManager.Player.FadeInAsync(0.2f, goingDown);
        }

        // 階段を降りた場合、対応するマップセルを解放
        if (goingDown && targetStage != null)
        {
            int column = targetStage.StairColumn;
            int depth = targetStage.MapPosition.y;
            GameProgressManager.Instance.UnlockCellByStairDescent(column, depth);
        }

        // マップ状態を即座に更新
        SyncWorldMapStatus();

        // オートセーブ（コマンド実行後にセーブされているはずだが、念のためタイミングはExecute内推奨）

            // ターン完了（敵のターンはスキップし、履歴を確定させる）
            // 確実にターン記録が終わってから入力受付を再開するためにawaitする
            await turnManager.OnPlayerActionComplete(true);

        isTransitioning = false;
    }

    public class PlayerStairTransitionCommand : ICommand, IAsyncUndoCommand
    {
        private GridManager gm;
        private StageSO sourceStage;
        private StageSO targetStage;
        private Vector2Int sourcePos;
        private Direction sourceDir;
        private Vector2Int targetPos;
        private Direction targetDir;

        // チェイン押し出し情報（TakeStairAsyncで事前に実行済み、Undo用に保持）
        private List<(Vector2Int from, Vector2Int to)> pushMoves;

        public PlayerStairTransitionCommand(GridManager gm, StageSO source, StageSO target,
            Vector2Int sPos, Direction sDir, Vector2Int tPos, Direction tDir,
            List<(Vector2Int from, Vector2Int to)> pushMoves = null)
        {
            this.gm = gm;
            this.sourceStage = source;
            this.targetStage = target;
            this.sourcePos = sPos;
            this.sourceDir = sDir;
            this.targetPos = tPos;
            this.targetDir = tDir;
            this.pushMoves = pushMoves;
        }

        public void Execute()
        {
            // 押し出しはTakeStairAsyncでPrepareEnemiesForTransitionの前に既に実行済み
            // ここでは二重実行しない

            // 移動処理（ターゲット位置へ）
            PerformTransition(targetStage, targetPos, targetDir);

            // オートセーブ
            gm.SaveGame(GameProgressManager.SaveType.Auto);
        }

        public void Undo()
        {
            UndoAsync().Forget();
        }

        public async UniTask UndoAsync()
        {
            PerformTransition(sourceStage, sourcePos, sourceDir);

            // チェイン押し出しを元に戻す
            if (pushMoves != null && pushMoves.Count > 0)
            {
                gm.UndoPushChainInStageData(targetStage, pushMoves);
            }
        }

        private void PerformTransition(StageSO stage, Vector2Int p, Direction d)
        {
            if (gm.unitManager.Player != null)
            {
                gm.unitManager.Player.gameObject.transform.SetParent(null);
            }

            gm.currentStageSO = stage;

            if (gm.unitManager.Player != null)
            {
                gm.unitManager.Player.WarpToNewStage(p, d);
            }

            gm.LoadStage(stage, false, p, d, false);
        }
    }

    #region Enemy Stair Transition
    /// <summary>
    /// 敵が階段マスに到達した際に、階段経由でステージ移動できるかチェックする
    /// </summary>
    public (StageSO targetStage, Vector2Int targetPos)? GetEnemyStairDestination(Enemy enemy, Vector2Int stairPos, Direction approachDirection)
    {
        if (currentStageSO == null) return null;

        CellType cellType = GetCellTypeAt(currentStageSO, stairPos);
        if (cellType != CellType.StairUp && cellType != CellType.StairDown)
        {
            return null;
        }

        StageSO targetStage = null;
        Vector2Int targetStairPos = Vector2Int.zero;

        StairConnectionData? connectionData = currentStageSO.GetStairConnectionData(stairPos);
        if (connectionData != null)
        {
            targetStage = connectionData.Value.TargetStage;
            targetStairPos = connectionData.Value.TargetPosition;
            if (targetStairPos == Vector2Int.zero && connectionData.Value.Position != Vector2Int.zero)
            {
                targetStairPos = stairPos;
            }
        }
        else
        {
            if (cellType == CellType.StairUp)
            {
                targetStage = currentStageSO.DefaultUpStage;
                targetStairPos = stairPos;
            }
            else if (cellType == CellType.StairDown)
            {
                targetStage = currentStageSO.DefaultDownStage;
                targetStairPos = stairPos;
            }

            if (targetStage == null && worldMapSO != null)
            {
                int currentFloor = currentStageSO.FloorIndex;
                int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;
                targetStage = worldMapSO.GetStageByFloorIndex(targetFloor);
                targetStairPos = stairPos;
            }
        }

        if (targetStage == null)
        {
            return null;
        }

        // 敵は階段マス自体に留まる
        Vector2Int targetPos = targetStairPos;

        if (!IsWalkableAt(targetStage, targetPos))
        {
            return null;
        }

        // 階段位置に敵がいる場合、到着方向に押し出し可能かチェック
        if (IsPositionOccupiedInStage(targetStage, targetPos))
        {
            if (!CanPushChainInDirection(targetStage, targetPos, approachDirection))
            {
                return null;
            }
        }

        return (targetStage, targetPos);
    }

    private bool IsPositionOccupiedInStage(StageSO stage, Vector2Int pos)
    {
        if (stage == currentStageSO)
        {
            return unitManager.GetUnitAtPosition(pos) != null;
        }

        bool hasStageEnemies = stageEnemiesMap.ContainsKey(stage);

        if (hasStageEnemies)
        {
            // stageEnemiesMapが存在する場合、インスタンスが権威あるソース
            foreach (var enemy in stageEnemiesMap[stage])
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos)
                {
                    return true;
                }
            }
        }

        // Pending敵をチェック（階段移動で到着してまだstageEnemiesMapに入っていない敵）
        if (pendingStageEnemies.TryGetValue(stage, out var pendingList))
        {
            foreach (var enemy in pendingList)
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos)
                {
                    return true;
                }
            }
        }

        if (hasStageEnemies)
        {
            // stageEnemiesMapがある場合、loadedStageEnemyDataは追加データ（階段遷移で来た敵）のみチェック
            // 初期配置の敵はstageEnemiesMapに既にインスタンス化されているので、データからは除外
            if (loadedStageEnemyData.TryGetValue(stage, out var dataList))
            {
                foreach (var data in dataList)
                {
                    // index == -1 は階段遷移で追加された敵（初期配置のindexは0以上）
                    if (!data.isDead && data.index == -1 && data.gridX == pos.x && data.gridY == pos.y)
                    {
                        return true;
                    }
                }
            }
        }
        else if (loadedStageEnemyData.TryGetValue(stage, out var enemyDataList))
        {
            // stageEnemiesMapがない場合、loadedStageEnemyDataを全てチェック
            foreach (var data in enemyDataList)
            {
                if (!data.isDead && data.gridX == pos.x && data.gridY == pos.y)
                {
                    return true;
                }
            }
        }
        else if (stage.EnemyStartData != null)
        {
            // どのデータソースもない場合のみ、初期配置データをフォールバックとしてチェック
            foreach (var startData in stage.EnemyStartData)
            {
                if (startData.Position == pos) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 指定位置の占有ユニットをチェイン（連鎖）で指定方向に押し出し可能かチェック
    /// pushDir方向に連続して並ぶ全てのユニットを1マスずつ押し出す
    /// </summary>
    public bool CanPushChainInDirection(StageSO stage, Vector2Int pos, Direction pushDir)
    {
        if (!IsPositionOccupiedInStage(stage, pos)) return false;
        Vector2Int dirVec = Utility.GetVectorFromDirection(pushDir);
        Vector2Int current = pos;
        // チェインの末端（空きマス）を探す
        while (IsPositionOccupiedInStage(stage, current))
        {
            current += dirVec;
        }
        // 末端が歩行可能であれば全員押し出せる
        return IsWalkableAt(stage, current);
    }

    /// <summary>
    /// チェイン（連鎖）押し出し情報を返す
    /// posから pushDir方向に連続する全ユニットを1マスずつ押し出す
    /// pushMovesは最も遠いユニットから順に格納（位置衝突回避のため）
    /// </summary>
    public (bool canPush, List<(Vector2Int from, Vector2Int to)> pushMoves)? GetChainPushInfo(StageSO stage, Vector2Int pos, Direction pushDir)
    {
        if (!IsPositionOccupiedInStage(stage, pos)) return null;

        Vector2Int dirVec = Utility.GetVectorFromDirection(pushDir);
        List<Vector2Int> chain = new List<Vector2Int>();
        Vector2Int current = pos;

        // チェインを収集
        while (IsPositionOccupiedInStage(stage, current))
        {
            chain.Add(current);
            current += dirVec;
        }

        // 末端が歩行可能かチェック
        bool canPush = IsWalkableAt(stage, current);

        // 押し出しペアを構築（最も遠いユニットから順に）
        var pushMoves = new List<(Vector2Int from, Vector2Int to)>();
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            pushMoves.Add((chain[i], chain[i] + dirVec));
        }

        return (canPush, pushMoves);
    }

    /// <summary>
    /// 指定ステージ・位置にいる敵の向いている方向を取得
    /// </summary>
    private Direction? GetOccupantDirectionInStage(StageSO stage, Vector2Int pos)
    {
        // アクティブステージのユニット
        if (stage == currentStageSO)
        {
            Unit unit = unitManager.GetUnitAtPosition(pos);
            if (unit != null && unit is Enemy enemy && !enemy.IsDead)
            {
                return enemy.CurrentDirection;
            }
        }

        // ロード済み敵データ
        if (loadedStageEnemyData.TryGetValue(stage, out var dataList))
        {
            foreach (var data in dataList)
            {
                if (!data.isDead && data.gridX == pos.x && data.gridY == pos.y)
                {
                    return (Direction)data.direction;
                }
            }
        }
        else if (stage.EnemyStartData != null)
        {
            foreach (var startData in stage.EnemyStartData)
            {
                if (startData.Position == pos)
                {
                    return startData.Direction;
                }
            }
        }

        // Pending敵
        if (pendingStageEnemies.TryGetValue(stage, out var pendingList))
        {
            foreach (var enemy in pendingList)
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos)
                {
                    return enemy.CurrentDirection;
                }
            }
        }

        // ステージ敵マップ（非アクティブ）
        if (stageEnemiesMap.TryGetValue(stage, out var enemyList))
        {
            foreach (var enemy in enemyList)
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos)
                {
                    return enemy.CurrentDirection;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// loadedStageEnemyDataが未作成の場合、EnemyStartDataから初期化する
    /// </summary>
    private void EnsureLoadedStageEnemyDataInitialized(StageSO stage)
    {
        if (!loadedStageEnemyData.ContainsKey(stage))
        {
            loadedStageEnemyData[stage] = new List<EnemySaveData>();
            if (!stageEnemiesMap.ContainsKey(stage) && stage.EnemyStartData != null)
            {
                int idx = 0;
                foreach (var esd in stage.EnemyStartData)
                {
                    loadedStageEnemyData[stage].Add(new EnemySaveData
                    {
                        gridX = esd.Position.x,
                        gridY = esd.Position.y,
                        direction = (int)esd.Direction,
                        isDead = false,
                        currentHP = esd.MaxHP,
                        maxHP = esd.MaxHP,
                        enemyType = (int)esd.EnemyType,
                        order = esd.SetActionOrder,
                        index = idx++,
                        ammo = esd.Ammo
                    });
                }
            }
        }
    }

    /// <summary>
    /// 1体の敵の位置を更新する（全データソース対象）
    /// </summary>
    private void PushSingleOccupantInStageData(StageSO stage, Vector2Int fromPos, Vector2Int toPos)
    {
        bool foundInData = false, foundInPending = false, foundInMap = false;

        // loadedStageEnemyData内のデータを更新
        if (loadedStageEnemyData.TryGetValue(stage, out var dataList))
        {
            for (int i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                if (!data.isDead && data.gridX == fromPos.x && data.gridY == fromPos.y)
                {
                    data.gridX = toPos.x;
                    data.gridY = toPos.y;
                    dataList[i] = data;
                    foundInData = true;
                    break;
                }
            }
        }

        // Pending敵のインスタンスも更新
        if (pendingStageEnemies.TryGetValue(stage, out var pendingList))
        {
            foreach (var enemy in pendingList)
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == fromPos)
                {
                    enemy.WarpTo(toPos, enemy.CurrentDirection);
                    foundInPending = true;
                    break;
                }
            }
        }

        // stageEnemiesMap内のインスタンスも更新
        if (stageEnemiesMap.TryGetValue(stage, out var enemyList))
        {
            foreach (var enemy in enemyList)
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == fromPos)
                {
                    enemy.WarpTo(toPos, enemy.CurrentDirection);
                    foundInMap = true;
                    break;
                }
            }
        }

        Debug.Log($"[PushSingle] {fromPos}->{toPos} foundInData={foundInData} foundInPending={foundInPending} foundInMap={foundInMap}");
    }

    /// <summary>
    /// チェイン（連鎖）押し出しを実行する
    /// pushMovesは最も遠いユニットから順に格納されている前提
    /// </summary>
    private void PushChainInStageData(StageSO stage, List<(Vector2Int from, Vector2Int to)> pushMoves)
    {
        Debug.Log($"[PushChain] Stage={stage.name}, moves={pushMoves.Count}, hasLoaded={loadedStageEnemyData.ContainsKey(stage)}, hasMap={stageEnemiesMap.ContainsKey(stage)}, hasPending={pendingStageEnemies.ContainsKey(stage)}");
        // 初回訪問時（どのデータソースにも存在しない）のみ初期化
        if (!loadedStageEnemyData.ContainsKey(stage) && !stageEnemiesMap.ContainsKey(stage) && !pendingStageEnemies.ContainsKey(stage))
        {
            EnsureLoadedStageEnemyDataInitialized(stage);
            Debug.Log($"[PushChain] Initialized loadedStageEnemyData from EnemyStartData, count={loadedStageEnemyData[stage].Count}");
        }
        foreach (var (from, to) in pushMoves)
        {
            Debug.Log($"[PushChain] Push from {from} to {to}");
            PushSingleOccupantInStageData(stage, from, to);
        }
    }

    /// <summary>
    /// チェイン押し出しを元に戻す（Undo用）
    /// pushMovesの逆順で、from/toを入れ替えて実行する
    /// 既存のデータソースのみ更新し、新規データは作成しない
    /// </summary>
    private void UndoPushChainInStageData(StageSO stage, List<(Vector2Int from, Vector2Int to)> pushMoves)
    {
        Debug.Log($"[UndoPushChain] Stage={stage.name}, moves={pushMoves.Count}, hasLoaded={loadedStageEnemyData.ContainsKey(stage)}, hasMap={stageEnemiesMap.ContainsKey(stage)}, hasPending={pendingStageEnemies.ContainsKey(stage)}");
        // Undo時は初期化しない（既存のデータソースのみ更新）
        // 逆順で元に戻す（最も近いユニットから順に戻す）
        for (int i = pushMoves.Count - 1; i >= 0; i--)
        {
            var (from, to) = pushMoves[i];
            Debug.Log($"[UndoPushChain] Undo: move from {to} back to {from}");
            PushSingleOccupantInStageData(stage, to, from);
        }

        // stageEnemiesMapが存在する場合、loadedStageEnemyDataの初期配置データは
        // stageEnemiesMapと重複するゴーストデータなので削除する
        if (stageEnemiesMap.ContainsKey(stage) && loadedStageEnemyData.TryGetValue(stage, out var dataList))
        {
            int before = dataList.Count;
            // index >= 0 の初期配置データを除去（index == -1 は階段遷移の追加データなので残す）
            dataList.RemoveAll(d => d.index >= 0);
            Debug.Log($"[UndoPushChain] Cleaned loadedStageEnemyData: {before} -> {dataList.Count}");
            if (dataList.Count == 0)
            {
                loadedStageEnemyData.Remove(stage);
                Debug.Log($"[UndoPushChain] Removed empty loadedStageEnemyData for {stage.name}");
            }
        }
    }

    /// <summary>
    /// 敵を階段経由で別ステージに移動させる
    /// </summary>
    public async UniTask MoveEnemyToStage(Enemy enemy, StageSO targetStage, Vector2Int targetPos, Direction direction)
    {
        if (enemy == null || targetStage == null) return;

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);

        bool goingDown = false;
        if (currentStageSO != null)
        {
            CellType cell = GetCellTypeAtLogic(currentStageSO, enemy.CurrentPos);
            if (cell == CellType.StairDown) goingDown = true;
        }

        await enemy.FadeOutAsync(0.15f, goingDown);

        var cmd = new EnemyStairTransitionCommand(this, enemy, currentStageSO, targetStage, targetPos, direction);
        commandManager.ExecuteCommand(cmd);
    }

    private class EnemyStairTransitionCommand : ICommand, IAsyncUndoCommand
    {
        private GridManager gm;
        private Enemy targetEnemy;
        private StageSO sourceStage;
        private StageSO targetStage;
        private EnemySaveData movedData;
        private EnemySaveData restoreData;
        private bool isExecuted = false;

        // チェイン押し出し情報
        private List<(Vector2Int from, Vector2Int to)> chainPushMoves;

        public EnemyStairTransitionCommand(GridManager gm, Enemy enemy, StageSO source, StageSO target, Vector2Int targetPos, Direction dir)
        {
            this.gm = gm;
            this.targetEnemy = enemy;
            this.sourceStage = source ?? gm.currentStageSO;
            this.targetStage = target;

            this.movedData = new EnemySaveData
            {
                gridX = targetPos.x,
                gridY = targetPos.y,
                direction = (int)dir,
                isDead = false,
                currentHP = enemy.HP.Value,
                aiState = enemy.EnemyAI != null ? enemy.EnemyAI.GetState() : 0,
                enemyType = (int)enemy.Type,
                order = enemy.Order,
                maxHP = enemy.MaxHP,
                index = -1,
                ammo = enemy.CurrentAmmo.Value
            };

            this.restoreData = new EnemySaveData
            {
                gridX = enemy.CurrentPos.x,
                gridY = enemy.CurrentPos.y,
                direction = (int)enemy.CurrentDirection,
                isDead = false,
                currentHP = enemy.HP.Value,
                aiState = enemy.EnemyAI != null ? enemy.EnemyAI.GetState() : 0,
                enemyType = (int)enemy.Type,
                order = enemy.Order,
                maxHP = enemy.MaxHP,
                index = -1,
                ammo = enemy.CurrentAmmo.Value
            };

            // チェイン押し出し情報を事前計算（到着ユニットの方向で押し出す）
            var pushInfo = gm.GetChainPushInfo(target, targetPos, dir);
            if (pushInfo != null && pushInfo.Value.canPush)
            {
                chainPushMoves = pushInfo.Value.pushMoves;
            }
        }

        public void Execute()
        {
            if (targetEnemy != null)
            {
                if (gm.stageEnemiesMap.ContainsKey(sourceStage))
                {
                    gm.stageEnemiesMap[sourceStage].Remove(targetEnemy);
                }

                gm.unitManager.RemoveEnemy(targetEnemy);

                targetEnemy.WarpTo(new Vector2Int(movedData.gridX, movedData.gridY), (Direction)movedData.direction);
                targetEnemy.gameObject.SetActive(false);
                targetEnemy.transform.SetParent(gm.transform);

                // ※ pendingへの追加はchainPushの後に行う（pushが新到着ユニットを巻き込まないようにするため）
            }

            if (!gm.loadedStageEnemyData.ContainsKey(targetStage))
            {
                gm.loadedStageEnemyData[targetStage] = new List<EnemySaveData>();

                if (!gm.stageEnemiesMap.ContainsKey(targetStage))
                {
                    if (targetStage.EnemyStartData != null)
                    {
                        int idx = 0;
                        foreach (var esd in targetStage.EnemyStartData)
                        {
                            gm.loadedStageEnemyData[targetStage].Add(new EnemySaveData
                            {
                                gridX = esd.Position.x,
                                gridY = esd.Position.y,
                                direction = (int)esd.Direction,
                                isDead = false,
                                currentHP = esd.MaxHP,
                                maxHP = esd.MaxHP,
                                enemyType = (int)esd.EnemyType,
                                order = esd.SetActionOrder,
                                index = idx++,
                                ammo = esd.Ammo
                            });
                        }
                    }
                }
            }

            // チェイン押し出し処理（既存の敵のみ対象、新到着ユニットはまだpending/dataに追加していない）
            if (chainPushMoves != null && chainPushMoves.Count > 0)
            {
                gm.PushChainInStageData(targetStage, chainPushMoves);
            }

            // 押し出し後にpendingへ追加（pushに巻き込まれないことを保証）
            if (targetEnemy != null)
            {
                gm.AddPendingEnemy(targetStage, targetEnemy);
            }

            // 押し出し後にデータ追加（新到着ユニットはstair位置に配置）
            // 移動先ステージの全敵の最大Orderを算出（全データソース対象）
            int maxOrder = 0;

            // loadedStageEnemyData
            if (gm.loadedStageEnemyData.TryGetValue(targetStage, out var targetList))
            {
                foreach (var d in targetList)
                {
                    if (!d.isDead && d.order > maxOrder) maxOrder = d.order;
                }
            }

            // stageEnemiesMap（実体化済み敵）
            if (gm.stageEnemiesMap.TryGetValue(targetStage, out var stageEnemies))
            {
                foreach (var e in stageEnemies)
                {
                    if (e != null && !e.IsDead && e.Order > maxOrder) maxOrder = e.Order;
                }
            }

            // pendingStageEnemies（遷移待ち敵）
            if (gm.pendingStageEnemies.TryGetValue(targetStage, out var pendingEnemies))
            {
                foreach (var e in pendingEnemies)
                {
                    if (e != null && !e.IsDead && e != targetEnemy && e.Order > maxOrder) maxOrder = e.Order;
                }
            }

            var newData = movedData;
            newData.order = maxOrder + 1;
            movedData = newData;

            // 敵インスタンスのOrderも更新
            if (targetEnemy != null)
            {
                targetEnemy.SetOrder(movedData.order);
            }

            gm.loadedStageEnemyData[targetStage].Add(movedData);
            isExecuted = true;
        }

        public void Undo()
        {
            UndoAsync().Forget();
        }

        public async UniTask UndoAsync()
        {
            if (!isExecuted) return;

            // 1. ターゲットステージからデータを削除
            if (gm.loadedStageEnemyData.ContainsKey(targetStage))
            {
                var list = gm.loadedStageEnemyData[targetStage];
                int removeIdx = -1;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var d = list[i];
                    if (d.gridX == movedData.gridX && d.gridY == movedData.gridY && d.enemyType == movedData.enemyType)
                    {
                        removeIdx = i;
                        break;
                    }
                }
                if (removeIdx >= 0)
                {
                    list.RemoveAt(removeIdx);
                }
            }

            // Pendingリストからも削除
            if (gm.pendingStageEnemies.ContainsKey(targetStage))
            {
                gm.pendingStageEnemies[targetStage].Remove(targetEnemy);
            }

            // ターゲットステージの敵リストからも削除（プレイヤーがそのステージに移動していた場合）
            if (gm.stageEnemiesMap.ContainsKey(targetStage) && gm.stageEnemiesMap[targetStage].Contains(targetEnemy))
            {
                gm.stageEnemiesMap[targetStage].Remove(targetEnemy);
            }

            // 2. 敵を元のステージへ復帰
            if (targetEnemy != null)
            {
                targetEnemy.SetHP(restoreData.currentHP);
                targetEnemy.SetOrder(restoreData.order);
                if (targetEnemy.CurrentAmmo != null) targetEnemy.SetAmmo(restoreData.ammo);
                if (targetEnemy.EnemyAI != null) targetEnemy.EnemyAI.SetState(restoreData.aiState);

                if (gm.currentStageSO == sourceStage)
                {
                    // プレイヤーがソースステージにいる: 敵を即座に復元
                    targetEnemy.WarpTo(new Vector2Int(restoreData.gridX, restoreData.gridY), (Direction)restoreData.direction);
                    targetEnemy.transform.SetParent(gm.gridVisualizer.GridParent);
                    targetEnemy.gameObject.SetActive(true);

                    targetEnemy.SetDissolveAmount(1f);
                    await targetEnemy.FadeInAsync(0.15f, false);

                    gm.unitManager.AddEnemy(targetEnemy);
                    if (!gm.stageEnemiesMap.ContainsKey(sourceStage))
                    {
                        gm.stageEnemiesMap[sourceStage] = new List<Enemy>();
                    }
                    if (!gm.stageEnemiesMap[sourceStage].Contains(targetEnemy))
                    {
                        gm.stageEnemiesMap[sourceStage].Add(targetEnemy);
                    }
                    gm.unitManager.SortEnemiesByOrder();
                }
                else
                {
                    // プレイヤーが別のステージにいる: Pendingに入れてデータを復元
                    targetEnemy.WarpTo(new Vector2Int(restoreData.gridX, restoreData.gridY), (Direction)restoreData.direction);
                    targetEnemy.gameObject.SetActive(false);
                    targetEnemy.transform.SetParent(gm.transform);

                    gm.AddPendingEnemy(sourceStage, targetEnemy);

                    if (!gm.loadedStageEnemyData.ContainsKey(sourceStage))
                    {
                        gm.loadedStageEnemyData[sourceStage] = new List<EnemySaveData>();
                    }
                    gm.loadedStageEnemyData[sourceStage].Add(restoreData);
                }
            }

            // チェイン押し出しを元に戻す
            if (chainPushMoves != null && chainPushMoves.Count > 0)
            {
                gm.UndoPushChainInStageData(targetStage, chainPushMoves);
            }
        }
    }
    #endregion

    private bool IsWalkableType(CellType cellType)
    {
        return cellType == CellType.Floor ||
               cellType == CellType.Ice ||
               cellType == CellType.DamageFloor ||
               cellType == CellType.Warp ||
               cellType == CellType.SyncMap ||
               cellType == CellType.Tutorial ||
               cellType == CellType.StairUp ||
               cellType == CellType.StairDown;
    }

    public bool IsWalkableAt(StageSO stage, Vector2Int pos)
    {
        CellType type = GetCellTypeAt(stage, pos);
        return IsWalkableType(type);
    }

    private GameObject getEnemyPrefab(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Slime:
                return slimeEnemyPrefab;
            case EnemyType.Goblin:
                return goblinEnemyPrefab;
            case EnemyType.Skeleton:
                return skeletonEnemyPrefab;
            case EnemyType.Zombie:
                return zombieEnemyPrefab;
            case EnemyType.Boar:
                return boarEnemyPrefab;
            case EnemyType.Crystal:
                return crystalEnemyPrefab;
            default:
                return null!;
        }
    }

    public void SwitchStage(StageSO nextstage, Direction entryDirection)
    {
        Vector2Int newStartPos = CalculateEntryPosition(nextstage, entryDirection);
        currentStageSO = nextstage;
        LoadStage(nextstage, false, newStartPos, null, true);
        turnManager.OnPlayerActionComplete().Forget();
    }

    public void SwitchStageDirect(StageSO stage, Vector2Int startPos)
    {
        currentStageSO = stage;
        LoadStage(stage, false, startPos, null, true);
    }

    private void LoadStage(StageSO stage, bool isFirstInit, Vector2Int startPos, Direction? customStartDir = null, bool resetTurnManager = true)
    {
        gridVisualizer.ResetGridParentPosition(); // Ensure parent is at zero before loading
        gridVisualizer.ClearAllTiles();

        // 現在の敵を非表示にする
        unitManager.SetAllEnemiesActive(false);

        width = stage.Width;
        height = stage.Height;

        Direction startDir = customStartDir ?? (isFirstInit ? stage.PlayerStartDirection : (unitManager.Player != null ? unitManager.Player.CurrentDirection : stage.PlayerStartDirection));
        Vector2Int pos = isFirstInit ? stage.PlayerStartPosition : startPos;

        if (unitManager.PlayerObject == null)
        {
            GeneratePlayer(stage, pos, startDir);
        }
        else
        {
            unitManager.Player.WarpToNewStage(pos, startDir);
        }

        PrepareEnemiesForStage(stage);
        GenerateTilemap(stage);

        if (resetTurnManager)
        {
            turnManager.Initialize();
        }
        SyncWorldMapStatus();
        CheckAndResolveCollisions();
        gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0f, false, !isTransitioning);
    }

    private void CheckAndResolveCollisions()
    {
        // Undo中は以前の安全な状態に戻るはずなのでチェックしない
        if (commandManager != null && commandManager.IsUndoing) return;

        List<Unit> activeUnits = new List<Unit>();
        if (unitManager.Player != null && !unitManager.Player.IsDead) activeUnits.Add(unitManager.Player);
        foreach (var e in unitManager.Enemies)
        {
            if (e != null && !e.IsDead && e.gameObject.activeInHierarchy) activeUnits.Add(e);
        }

        HashSet<Unit> unitsToKill = new HashSet<Unit>();
        Dictionary<Vector2Int, List<Unit>> posMap = new Dictionary<Vector2Int, List<Unit>>();

        foreach (var unit in activeUnits)
        {
            // 1. Check Wall Collision
            CellType cell = GetCellTypeAtLogic(currentStageSO, unit.CurrentPos);
            if (cell == CellType.Wall)
            {
                unitsToKill.Add(unit);
            }

            // 2. Group by Position for Overlap Check
            if (!posMap.ContainsKey(unit.CurrentPos))
            {
                posMap[unit.CurrentPos] = new List<Unit>();
            }
            posMap[unit.CurrentPos].Add(unit);
        }

        // Check Overlaps
        foreach (var kvp in posMap)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var u in kvp.Value) unitsToKill.Add(u);
            }
        }

        // Apply Death
        foreach (var unit in unitsToKill)
        {
            //Debug.Log($"[GridManager] Unit {unit.name} died due to collision at {unit.CurrentPos}");

            // Execute InstantDeathCommand
            // visual effect is handled inside the command now
            commandManager.ExecuteCommand(new InstantDeathCommand(unit, 999, errorEffectPrefab));
        }
    }



    public Enemy GetEnemyAt(StageSO stage, Vector2Int pos)
    {
        EnsureStageEnemiesLoaded(stage);
        if (!stageEnemiesMap.ContainsKey(stage)) return null;

        foreach (var e in stageEnemiesMap[stage])
        {
            if (e.CurrentPos == pos && !e.IsDead) return e;
        }
        return null;
    }

    public void EnsureStageEnemiesLoaded(StageSO stage)
    {
        // 既存のリストを取得、なければ新規作成してMapに追加
        List<Enemy> currentEnemies;
        if (stageEnemiesMap.ContainsKey(stage))
        {
            currentEnemies = stageEnemiesMap[stage];
        }
        else
        {
            currentEnemies = new List<Enemy>();
            stageEnemiesMap.Add(stage, currentEnemies);

            // まだロードされていないステージなら、初期配置（または全セーブデータ）から生成
            // loadedStageEnemyDataがある場合はそこから、なければ初期配置から生成
            if (!loadedStageEnemyData.ContainsKey(stage))
            {
                var newEnemies = GenerateEnemies(stage);
                currentEnemies.AddRange(newEnemies);
                // 初期生成時は非アクティブにする（後でPrepareEnemiesForStageで制御）
                foreach(var e in newEnemies) e.gameObject.SetActive(false);
            }
        }

        // 追加のロード待ちデータ（移動してきた敵など）があれば生成してマージ
        if (loadedStageEnemyData.ContainsKey(stage))
        {
            List<EnemySaveData> saveDataList = loadedStageEnemyData[stage];
            List<Enemy> pending = pendingStageEnemies.ContainsKey(stage) ? pendingStageEnemies[stage] : null;

            List<Enemy> newEnemies = GenerateEnemiesFromSaveData(stage, saveDataList, pending);

            currentEnemies.AddRange(newEnemies);
            // 生成した敵はとりあえず非アクティブ（Prepareで制御）
            foreach(var e in newEnemies) e.gameObject.SetActive(false);

            if (pending != null) pending.Clear(); // 消費したのでクリア

            // 処理済みデータを削除
            loadedStageEnemyData.Remove(stage);
        }
    }

    private List<Enemy> GenerateEnemiesFromSaveData(StageSO stage, List<EnemySaveData> saveDataList, List<Enemy> pendingEnemies = null)
    {
        List<Enemy> generatedEnemies = new List<Enemy>();
        List<EnemyStartData> defaultDataList = stage.EnemyStartData;

        foreach (var data in saveDataList)
        {
            // Pendingリストから、位置とタイプが一致する既存インスタンスを探して再利用する（Duplication防止）
            Enemy recycled = null;
            if (pendingEnemies != null)
            {
                foreach(var p in pendingEnemies)
                {
                    // Execute時に移動先座標へWarpToされている前提
                    if (p.CurrentPos.x == data.gridX && p.CurrentPos.y == data.gridY && (int)p.Type == data.enemyType)
                    {
                        recycled = p;
                        break;
                    }
                }
            }

            if (recycled != null)
            {
                generatedEnemies.Add(recycled);
                pendingEnemies.Remove(recycled);

                // データに基づいて状態を確実に戻す
                recycled.SetHP(data.currentHP);
                recycled.SetAIState(data.aiState);
                if(data.isDead) recycled.ForceKillSilent();
                else recycled.ReviveSilent();

                // 親をVisualizerに戻す
                recycled.transform.SetParent(gridVisualizer.transform);
                continue;
            }

            // 以下、通常の生成ロジック
            Vector3 worldPos = GridToWorldPosition(new Vector2Int(data.gridX, data.gridY));

            // Allow cross-stage enemies by checking saved type first, then fallback to stage data
            GameObject prefab = null;
            if (data.enemyType != 0) // Assuming 0 is invalid or we handle default. Actually 0 might be Slime?
                                     // Enum starts at 0. If we default to 0, old saves are Slime. That's fine.
            {
                 prefab = getEnemyPrefab((EnemyType)data.enemyType);
            }
            else
            {
                 // Fallback for old saves or safety
                 if(data.index >= 0 && data.index < stage.EnemyStartData.Count)
                    prefab = getEnemyPrefab(stage.EnemyStartData[data.index].EnemyType);
            }

            if (prefab != null)
            {
                 // Order restoration: logic uses data.order if available.
                 // If old save (order=0), it might conflict, but re-sorting will handle relative order.
                 int orderToUse = data.order;
                 if (orderToUse == 0 && data.index >= 0 && data.index < stage.EnemyStartData.Count)
                     orderToUse = stage.EnemyStartData[data.index].SetActionOrder; // Fallback

                 int maxHPToUse = data.maxHP;
                 if (maxHPToUse == 0)
                 {
                     if (data.index >= 0 && data.index < stage.EnemyStartData.Count)
                        maxHPToUse = stage.EnemyStartData[data.index].MaxHP; // Fallback
                     else
                        maxHPToUse = 3; // Default fallback
                 }

                var newEnemy = InstantiateAndInitializeEnemy(prefab, worldPos, new Vector2Int(data.gridX, data.gridY), (Direction)data.direction, maxHPToUse, orderToUse, data.ammo);

                newEnemy.SetHP(data.currentHP);
                newEnemy.SetAIState(data.aiState);
                if(data.isDead) newEnemy.ForceKillSilent();

                // Ensure name is unique/helpful
                newEnemy.name = $"Enemy_{(EnemyType)data.enemyType}_{data.index}_Loaded";

                // Force update direction visuals immediately
                newEnemy.SetDirection((Direction)data.direction);

                generatedEnemies.Add(newEnemy);
            }
        }
        return generatedEnemies;
    }

    private void PrepareEnemiesForStage(StageSO stage)
    {
        EnsureStageEnemiesLoaded(stage);

        List<Enemy> enemies = stageEnemiesMap[stage];
        unitManager.SetEnemies(enemies);

        // 状態に応じて表示
        foreach (var enemy in enemies)
        {
            // 先にSetActive(true)してからWarpTo(SetDirection)しないと、Animatorがリセットされて向きが初期化されてしまう
            enemy.gameObject.SetActive(true);

            // Update visual position to match logical position (in case it moved while inactive/background)
            enemy.WarpTo(enemy.CurrentPos, enemy.CurrentDirection);

            // DissolveAmountをリセット（階段移動後にフェードアウト状態のままになっている場合に対応）
            // 0 = 見える, 1 = 見えない
            enemy.SetDissolveAmount(0f);

            // ロード直後の「死んでいるけどActiveでありたい」要件を満たすため、SetActive(true)した上で見た目を調整
            if (enemy.IsDead)
            {
                enemy.ApplyDeadStateVisual();
            }
        }

        unitManager.SortEnemiesByOrder();
    }

    private List<Enemy> GenerateEnemiesFromSaveData(StageSO stage, List<EnemySaveData> saveDataList)
    {
        List<Enemy> generatedEnemies = new List<Enemy>();
        // ステージ初期データのPrefab情報を使うため、stage.EnemyStartDataを参照する
        // ただし、SavedDataのIndexとEnemyStartDataの順序が一致している前提
        List<EnemyStartData> defaultDataList = stage.EnemyStartData;

        foreach (var data in saveDataList)
        {
            Vector3 worldPos = GridToWorldPosition(new Vector2Int(data.gridX, data.gridY));

            // Allow cross-stage enemies by checking saved type first, then fallback to stage data
            GameObject prefab = null;
            if (data.enemyType != 0) // Assuming 0 is invalid or we handle default. Actually 0 might be Slime?
                                     // Enum starts at 0. If we default to 0, old saves are Slime. That's fine.
            {
                 prefab = getEnemyPrefab((EnemyType)data.enemyType);
            }
            else
            {
                 // Fallback for old saves or safety
                 if(data.index >= 0 && data.index < stage.EnemyStartData.Count)
                    prefab = getEnemyPrefab(stage.EnemyStartData[data.index].EnemyType);
            }

            if (prefab != null)
            {
                 // Order restoration: logic uses data.order if available.
                 // If old save (order=0), it might conflict, but re-sorting will handle relative order.
                 int orderToUse = data.order;
                 if (orderToUse == 0 && data.index >= 0 && data.index < stage.EnemyStartData.Count)
                     orderToUse = stage.EnemyStartData[data.index].SetActionOrder; // Fallback

                 int maxHPToUse = data.maxHP;
                 if (maxHPToUse == 0)
                 {
                     if (data.index >= 0 && data.index < stage.EnemyStartData.Count)
                        maxHPToUse = stage.EnemyStartData[data.index].MaxHP; // Fallback
                     else
                        maxHPToUse = 3; // Default fallback
                 }

                var newEnemy = InstantiateAndInitializeEnemy(prefab, worldPos, new Vector2Int(data.gridX, data.gridY), (Direction)data.direction, maxHPToUse, orderToUse, data.ammo);

                newEnemy.SetHP(data.currentHP);
                newEnemy.SetAIState(data.aiState);
                if(data.isDead) newEnemy.ForceKillSilent();

                // Ensure name is unique/helpful
                newEnemy.name = $"Enemy_{(EnemyType)data.enemyType}_{data.index}_Loaded";

                // Force update direction visuals immediately
                newEnemy.SetDirection((Direction)data.direction);

                generatedEnemies.Add(newEnemy);
            }
        }
        return generatedEnemies;
    }

    private void GenerateTilemap(StageSO stage)
    {
        cellData = new CellType[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Use the stage logic + overrides
                cellData[x, y] = GetCellTypeAtLogic(stage, new Vector2Int(x, y));
            }
        }

        gridVisualizer.RefreshTilemap(width, height, cellData, stage, GameProgressManager.Instance.IsPositionCleared);
        OnEnemyStateChanged(true);
    }

    public CellType GetCellTypeAt(StageSO stage, Vector2Int pos)
    {
        // Wrapper for external calls
        return GetCellTypeAtLogic(stage, pos);
    }

    // internal logic
    private CellType GetCellTypeAtLogic(StageSO stage, Vector2Int pos)
    {
        // 1. Check destroyed walls
        if (destroyedWalls.ContainsKey(stage) && destroyedWalls[stage].Contains(pos))
        {
            return CellType.Floor;
        }

        // 2. Default from SO
        return stage.GetWallAt(pos.x, pos.y);
    }

    public void BreakWall(StageSO stage, Vector2Int pos)
    {
        if (!destroyedWalls.ContainsKey(stage))
        {
            destroyedWalls[stage] = new HashSet<Vector2Int>();
        }

        if (!destroyedWalls[stage].Contains(pos))
        {
            destroyedWalls[stage].Add(pos);

            // Reflect if current stage
            if (stage == currentStageSO)
            {
                if(pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                {
                    cellData[pos.x, pos.y] = CellType.Floor;
                    // Visualizer update (Might need a method to update single tile, but Refresh is safe)
                    // Optimization: Add UpdateTile(x, y, type) to Visualizer later if needed.
                    gridVisualizer.RefreshTilemap(width, height, cellData, stage, GameProgressManager.Instance.IsPositionCleared);
                    OnEnemyStateChanged();
                }
            }

            // --- Recursive Break for Adjacent Stage ---
            CheckAndBreakNeighborWall(stage, pos, Direction.Left, pos.x == 0);
            CheckAndBreakNeighborWall(stage, pos, Direction.Right, pos.x == stage.Width - 1);
            CheckAndBreakNeighborWall(stage, pos, Direction.Down, pos.y == 0);
            CheckAndBreakNeighborWall(stage, pos, Direction.Up, pos.y == stage.Height - 1);
        }
    }

    private void CheckAndBreakNeighborWall(StageSO currentStage, Vector2Int currentPos, Direction dir, bool isEdge)
    {
        if (!isEdge) return;

        StageSO neighbor = currentStage.GetAdjacentStage(dir);
        if (neighbor == null) return;

        // Create a 'dummy' unit position (just for calculation) or calculate manually
        // If we break wall at (0, y), neighbor is Left.
        // Corresponding pos in Left neighbor is (NeighborWidth-1, y).

        Vector2Int targetPos = Vector2Int.zero;
        switch(dir)
        {
            case Direction.Left:
                targetPos = new Vector2Int(neighbor.Width - 1, currentPos.y);
                break;
            case Direction.Right:
                targetPos = new Vector2Int(0, currentPos.y);
                break;
            case Direction.Down:
                targetPos = new Vector2Int(currentPos.x, neighbor.Height - 1);
                break;
            case Direction.Up:
                targetPos = new Vector2Int(currentPos.x, 0);
                break;
        }

        // Check if the target cell is actually a wall
        // We use GetCellTypeAt(neighbor, targetPos) which checks *its* destroyed state too.
        // But we want to know if it *was* a wall originally or currently.
        // If it's already Floor (or destroyed), BreakWall will filter it out (if destroyed) or do nothing (if original floor).
        // Wait, BreakWall blindly adds to destroyed set if not present.
        // If original is Floor, we shouldn't add to destroyed set?
        // BreakWallCommand doesn't check if it's wall. It assumes caller checked.
        // But here we are automating. We should check if it IS a wall before breaking.

        // Use GetCellTypeAtLogic to see current state (including destroyed).
        CellType type = GetCellTypeAtLogic(neighbor, targetPos);
        if (type == CellType.Wall)
        {
             BreakWall(neighbor, targetPos);
        }
    }

    public void RestoreWall(StageSO stage, Vector2Int pos)
    {
        if (destroyedWalls.ContainsKey(stage))
        {
            if (destroyedWalls[stage].Remove(pos))
            {
                // Reflect if current
                if (stage == currentStageSO)
                {
                     if(pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                     {
                         // Re-read default
                         cellData[pos.x, pos.y] = stage.GetWallAt(pos.x, pos.y);
                         gridVisualizer.RefreshTilemap(width, height, cellData, stage, GameProgressManager.Instance.IsPositionCleared);
                         OnEnemyStateChanged();
                     }
                }
            }
        }
    }

    public bool CheckIfStageHasEnemy(StageSO stage)
    {
        // 1. Check active runtime list
        if (stageEnemiesMap.TryGetValue(stage, out var enemies))
        {
             foreach (var enemy in enemies)
             {
                 if(enemy != null && !enemy.IsDead) return true;
             }
             return false; // Loaded and empty/all dead -> No enemies
        }

        // 2. Check loaded save data (not yet instantiated)
        if (loadedStageEnemyData.TryGetValue(stage, out var saveDataList))
        {
            foreach (var data in saveDataList)
            {
                if (!data.isDead) return true;
            }
            return false; // All dead in save data
        }

        // 3. Fallback to default SO data (First time visit)
        return stage.EnemyStartData.Count > 0;
    }

    public void SyncStageStatus(StageSO stage)
    {
        if (worldMapSO == null || stage == null) return;

        Vector2Int coords = worldMapSO.GetStageCoordinates(stage);
        if (coords.x == -1) return;

        bool hasEnemy = CheckIfStageHasEnemy(stage);
        bool isCleared = !hasEnemy;
        bool isCurrent = (stage == currentStageSO);

        GameProgressManager.Instance.UpdateStageStatus(coords, isCurrent, isCleared, worldMapSO);
    }

    private bool previousEnemyAliveState = true;

    private void OnEnemyStateChanged(bool quiet = false)
    {
        bool hasAliveEnemy = unitManager.HasAliveEnemy();

        bool currentState = hasAliveEnemy;
        bool stateChanged = previousEnemyAliveState != currentState;

        SyncWorldMapStatus();

        if (!quiet && stateChanged)
        {
            if(!hasAliveEnemy)
            {
                if (showWin != null) showWin.Show();
            }
            gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0.5f, true);
        }

        previousEnemyAliveState = currentState;
    }

    private void SyncWorldMapStatus()
    {
        if (currentStageSO == null) return;

        bool isCleared = !unitManager.HasAliveEnemy();

        // 階層マップでクリア状態をマーク（永続的）
        if (isCleared)
        {
            Vector2Int mapPos = currentStageSO.MapPosition;
            bool wasAlreadyCleared = GameProgressManager.Instance.IsHierarchicalCellCleared(mapPos);
            GameProgressManager.Instance.MarkHierarchicalCellAsCleared(mapPos);

            // 初めてクリアした場合はオートセーブ
            if (!wasAlreadyCleared)
            {
                SaveGame(GameProgressManager.SaveType.Auto);
                Debug.Log($"Auto-saved: Stage at {mapPos} cleared for the first time");
            }
        }

        // 旧マップシステム（互換性のため）
        if (worldMapSO == null) return;

        Vector2Int coords = worldMapSO.GetStageCoordinates(currentStageSO);
        if (coords.x == -1) return;

        bool isNewAreaUnlocked = GameProgressManager.Instance.UpdateStageStatus(coords, true, isCleared, worldMapSO);

        if (isNewAreaUnlocked) popup.Show();
    }

    // TurnManagerから直接呼ばれる代わりにイベント経由になったため不要だが、
    // まだ完全移行できていない部分のために残すか、ラッパーとして残す
    public void UpdateWarpTilesPublic()
    {
        OnEnemyStateChanged();
    }

    private void GeneratePlayer(StageSO stage, Vector2Int pos, Direction dir)
    {
        Vector3 worldPos = GridToWorldPosition(pos);
        GameObject playerObj = Instantiate(playerPrefab, worldPos, Quaternion.identity, gridVisualizer.GridParent);
        var pScript = playerObj.GetComponent<Player>();
        pScript.Initialize(this,
                                turnManager,
                                commandManager,
                                pos,
                                dir,
                                stage.PlayerMaxHP,
                                stage.PlayerStartOrder);

        unitManager.RegisterPlayer(playerObj);
    }

    private List<Enemy> GenerateEnemies(StageSO stage)
    {
        List<EnemyStartData> enemyDataList = stage.EnemyStartData;
        List<Enemy> generatedEnemies = new List<Enemy>();
        int count = 0;

        foreach (var enemyData in enemyDataList)
        {
            Vector3 worldPos = GridToWorldPosition(enemyData.Position);

            GameObject prefab = getEnemyPrefab(enemyData.EnemyType);

            var enemyScript = InstantiateAndInitializeEnemy(prefab, worldPos, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder, enemyData.Ammo);
            enemyScript.name = $"Enemy_{enemyData.EnemyType}_{count}";
            count++;

            // Force update direction visuals immediately
            enemyScript.SetDirection(enemyData.Direction);

            generatedEnemies.Add(enemyScript);
        }
        return generatedEnemies;
    }

    private Enemy InstantiateAndInitializeEnemy(GameObject prefab, Vector3 worldPos, Vector2Int gridPos, Direction dir, int maxHp, int order, int ammo = -1)
    {
        GameObject enemyObj = Instantiate(prefab, worldPos, Quaternion.identity, gridVisualizer.GridParent);
        var enemyScript = enemyObj.GetComponent<Enemy>();

        // Ammo limitation applies only to Ranged Chaser
        var ai = enemyObj.GetComponent<EnemyAIChaserRanged>();
        if (ai == null)
        {
            ammo = -1;
        }

        // Note: Initialize内でGridManagerを渡しているが、UnitManagerを使うようにUnit側も修正が必要かもしれない
        // 一旦はGridManagerのプロキシメソッド(GetUnitAtPositionなど)経由でUnitManagerにアクセスする形になる
        enemyScript.Initialize(this, turnManager, commandManager, gridPos, dir, maxHp, order, ammo);
        return enemyScript;
    }

    public Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection)
    {
        Vector2Int current = unitManager.Player.CurrentPos; // Note: This uses Player pos. We need generic pos.
        // Wait, the original method uses Player.CurrentPos. This is specific to Player transition!
        return CalculateEntryPosition(nextStage, fromDirection, unitManager.Player.CurrentPos);
    }

    public Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection, Vector2Int currentPos)
    {
        int targetX = 0;
        int targetY = 0;

        switch (fromDirection)
        {
            case Direction.Right: // 左端に出る
                targetX = 0;
                targetY = Mathf.Clamp(currentPos.y, 0, nextStage.Height - 1);
                break;
            case Direction.Left: // 右端に出る
                targetX = nextStage.Width - 1;
                targetY = Mathf.Clamp(currentPos.y, 0, nextStage.Height - 1);
                break;
            case Direction.Up: // 下端に出る
                targetX = Mathf.Clamp(currentPos.x, 0, nextStage.Width - 1);
                targetY = 0;
                break;
            case Direction.Down: // 上端に出る
                targetX = Mathf.Clamp(currentPos.x, 0, nextStage.Width - 1);
                targetY = nextStage.Height - 1;
                break;
        }
        return new Vector2Int(targetX, targetY);
    }

    public bool TryTransition(Direction direction)
    {
        // ステージ間の直接移動は無効（階段経由のみ）
        return false;
    }

    public async UniTask ExecuteTransition(StageSO stage, Direction direction)
    {
        isTransitioning = true;
        AudioManager.Instance.TriggerStageChangeEffect();
        TransitionCommand transitionCommand = new TransitionCommand(this, currentStageSO, stage, direction, unitManager.Player.CurrentPos);
        commandManager.ExecuteCommand(transitionCommand);
        await SwitchStageAsync(stage, direction);
        SwitchStage(stage, direction);
        isTransitioning = false;
        gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0f, true);
    }

    public async UniTask PlayTransitionWithoutCommand(StageSO stage, Direction direction, Vector2Int startPos)
    {
        isTransitioning = true;
        AudioManager.Instance.TriggerStageChangeEffect();
        await SwitchStageAsync(stage, direction);
        SwitchStageDirect(stage, startPos);
        isTransitioning = false;
        gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0f, true);
    }

    #region utility methods (Proxy to UnitManager / Visualizer)
    public bool IsCellWalkable(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return false;
        CellType cellType = cellData[pos.x, pos.y];
        return cellType == CellType.Floor || cellType == CellType.Ice || cellType == CellType.DamageFloor || cellType == CellType.Warp || cellType == CellType.SyncMap || cellType == CellType.Tutorial || cellType == CellType.StairUp || cellType == CellType.StairDown;
    }

    public Unit GetUnitAtPosition(Vector2Int pos)
    {
        return unitManager.GetUnitAtPosition(pos);
    }

    public Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        return gridVisualizer.GridToWorldPosition(gridPos);
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        return gridVisualizer.WorldToGridPosition(worldPos);
    }
    #endregion

    public StageSO GetStageOfEnemy(Enemy enemy)
    {
        // Search all
        foreach (var pair in stageEnemiesMap)
        {
            if (pair.Value.Contains(enemy)) return pair.Key;
        }
        return null;
    }

    public List<Enemy> GetEnemiesOnStage(StageSO stage)
    {
        if (stageEnemiesMap.TryGetValue(stage, out var list))
        {
            return list;
        }
        return null;
    }

    #region Save
    public void SaveGame(GameProgressManager.SaveType saveType = GameProgressManager.SaveType.Manual)
    {
        if (unitManager.Player == null) return;

        // 全ステージの敵情報を収集
        List<StageEnemyData> allStagesEnemies = new List<StageEnemyData>();

        // 1. まずloadedStageEnemyData（未訪問だがロード済みのデータ）をそのまま追加
        foreach (var kvp in loadedStageEnemyData)
        {
            // 階層マップのMapPositionを使用
            Vector2Int coords = kvp.Key.MapPosition;
            allStagesEnemies.Add(new StageEnemyData { stageX = coords.x, stageY = coords.y, enemies = kvp.Value });
        }

        // 2. 次にstageEnemiesMap（訪問済み・実体化済み）を追加。重複がある場合はこちらを優先（上書き）
        foreach (var kvp in stageEnemiesMap)
        {
            StageSO stage = kvp.Key;
            List<Enemy> enemies = kvp.Value;

            // 階層マップのMapPositionを使用
            Vector2Int coords = stage.MapPosition;

            // 既存リストから削除（もしloadedにあれば）
            allStagesEnemies.RemoveAll(d => d.stageX == coords.x && d.stageY == coords.y);

            // データ作成
            List<EnemySaveData> enemyDataList = new List<EnemySaveData>();
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                enemyDataList.Add(new EnemySaveData
                {
                    index = i,
                    gridX = enemy.CurrentPos.x,
                    gridY = enemy.CurrentPos.y,
                    direction = (int)enemy.CurrentDirection,
                    isDead = enemy.IsDead,
                    currentHP = enemy.HP.Value,
                    aiState = enemy.GetAIState(),
                    enemyType = (int)enemy.Type,
                    order = enemy.Order,
                    maxHP = enemy.MaxHP,
                    ammo = enemy.CurrentAmmo.Value
                });
            }

            allStagesEnemies.Add(new StageEnemyData { stageX = coords.x, stageY = coords.y, enemies = enemyDataList });
        }

        // 5. 全ステージの破壊された壁情報
        List<StageWallData> allStagesWalls = new List<StageWallData>();
        foreach (var kvp in destroyedWalls)
        {
            // 階層マップのMapPositionを使用
            Vector2Int coords = kvp.Key.MapPosition;
            List<Vector2IntData> walls = new List<Vector2IntData>();
            foreach (var wallPos in kvp.Value)
            {
                walls.Add(new Vector2IntData(wallPos));
            }
            allStagesWalls.Add(new StageWallData { stageX = coords.x, stageY = coords.y, destroyedWalls = walls });
        }
        Debug.Log($"[GridManager] Saving: Captured {allStagesEnemies.Count} stages with enemies, {allStagesWalls.Count} stages with destroyed walls.");

        GameProgressManager.Instance.SaveProgress(
            unitManager.Player.CurrentPos,
            unitManager.Player.CurrentDirection,
            unitManager.EnemyObjects.Count > 0 ? unitManager.Enemies : new List<Enemy>(), // Currently active enemies? unitManager.Enemies is correct
            currentResetMapPos,
            unitManager.Player.HP.Value,
            allStagesEnemies,
            allStagesWalls,
            saveType
        );

        saveFeedback.PlayFeedback();
    }

    public void ContinueGame(GameProgressManager.SaveType saveType)
    {
        GameProgressManager.Instance.LoadProgress(saveType);

        if (GameProgressManager.Instance.HasLoadedData)
        {
            // --- Restore Destroyed Walls ---
            destroyedWalls.Clear();
            if (GameProgressManager.Instance.SavedAllStagesWalls != null)
            {
                foreach (var wallData in GameProgressManager.Instance.SavedAllStagesWalls)
                {
                    // MapPositionからステージを検索
                    Vector2Int mapPos = new Vector2Int(wallData.stageX, wallData.stageY);
                    StageSO stage = worldMapSO.GetStageByMapPosition(mapPos);
                    if (stage != null)
                    {
                        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
                        foreach (var w in wallData.destroyedWalls) walls.Add(w.ToVector2Int());
                        destroyedWalls[stage] = walls;
                    }
                }
            }
            Debug.Log($"[GridManager] Loaded: Restored walls for {destroyedWalls.Count} stages.");

            // Restore enemies for non-current stages (deferred loading)
            loadedStageEnemyData.Clear();
            if (GameProgressManager.Instance.SavedAllStagesEnemies != null)
            {
                foreach (var stageData in GameProgressManager.Instance.SavedAllStagesEnemies)
                {
                    // MapPositionからステージを検索
                    Vector2Int mapPos = new Vector2Int(stageData.stageX, stageData.stageY);
                    StageSO stage = worldMapSO.GetStageByMapPosition(mapPos);
                    if (stage != null)
                    {
                        loadedStageEnemyData[stage] = stageData.enemies;
                    }
                }
            }
            Debug.Log($"[GridManager] Loaded: Restored enemy data for {loadedStageEnemyData.Count} stages.");

            // Load Initial Stage (Current Stage) based on saved map pos
            Vector2Int currentMapPos = GameProgressManager.Instance.GetCurrentMapPosition();
            StageSO savedStage = worldMapSO.GetStageByMapPosition(currentMapPos);

            if (savedStage != null)
            {
                currentStageSO = savedStage;
                initialStageSO = savedStage;
                currentResetMapPos = GameProgressManager.Instance.SavedResetMapPos;

                Vector2Int startPos = GameProgressManager.Instance.SavedPlayerGridPos;
                Direction startDir = GameProgressManager.Instance.SavedPlayerDirection;
                int playerHP = GameProgressManager.Instance.SavedPlayerHP;

                // Load Stage (This will spawn player and enemies)
                LoadStage(currentStageSO, false, startPos, startDir);

                unitManager.Player.SetHP(playerHP);

                commandManager.Warped(); // Clear undo stack
            }
            else
            {
                 // Fallback if saved stage not found
                 ResetAndStartNewGame();
            }
        }
        else
        {
            ResetAndStartNewGame();
            if (worldMapPresenter != null)
            {
                worldMapPresenter.OpenMap(true);
            }
        }
    }

    private void ResetAndStartNewGame()
    {
        // New Game
        currentResetMapPos = worldMapSO.GetStageCoordinates(stageSOs[0]);
        initialStageSO = stageSOs[0];
        currentStageSO = initialStageSO;
        LoadStage(initialStageSO, true, currentStageSO.PlayerStartPosition);
    }

    private void LoadStageWithCustomStart(StageSO stage, Vector2Int startPos, Direction startDir, int playerHP)
    {
        gridVisualizer.ClearAllTiles();

        unitManager.SetAllEnemiesActive(false);

        width = stage.Width;
        height = stage.Height;

        if (unitManager.PlayerObject == null)
        {
            GeneratePlayer(stage, startPos, startDir);
        }
        else
        {
            unitManager.Player.WarpToNewStage(startPos, startDir);
        }
        unitManager.Player.SetHP(playerHP);
        PrepareEnemiesForStage(stage);

        GenerateTilemap(stage);

        turnManager.Initialize();
        turnManager.Initialize();
        SyncWorldMapStatus();
        gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0f, false, !isTransitioning);
    }

    private void ApplySavedEnemyData(List<EnemySaveData> savedList)
    {
        List<Enemy> enemies = unitManager.Enemies;
        if (savedList == null || savedList.Count != enemies.Count)
        {
            return;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            EnemySaveData data = savedList[i];

            if (data.isDead)
            {
                enemy.ForceKillSilent();
                //Debug.Log("Enemy is dead");
            }
            else
            {
                enemy.ReviveSilent();
                enemy.WarpTo(new Vector2Int(data.gridX, data.gridY), (Direction)data.direction);
                enemy.SetHP(data.currentHP);
                //Debug.Log("Enemy is revived");
            }
        }
    }
    #endregion
}