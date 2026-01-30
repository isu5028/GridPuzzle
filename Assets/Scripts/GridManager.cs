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

    private bool previousWarpState = true; // true: enemyWarpTile, false: playerWarpTile
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
             .Subscribe(_ => UpdateWarpTiles())
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
        // 次のステージに敵がいるかどうかをチェック（TransitionTilemapのWarp表示用）
        bool nextStageHasEnemy = CheckIfStageHasEnemy(nextstage);

        HashSet<Vector2Int> destroyed = destroyedWalls.ContainsKey(nextstage) ? destroyedWalls[nextstage] : null;

        gridVisualizer.SetupTransitionTilemap(nextstage, entryDirection, nextStageHasEnemy, destroyed, GameProgressManager.Instance.IsPositionCleared);

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
        CleanupAllEnemies();
        destroyedWalls.Clear(); // Return walls to normal on warp
        GameProgressManager.Instance.ResetStageCleared();
        if (unitManager.Player != null)
        {
            unitManager.Player.Revive();
            unitManager.Player.SetHP(unitManager.Player.MaxHP);
        }

        currentStageSO = stage;
        initialStageSO = stage;
        commandManager.Warped();

        currentResetMapPos = worldMapSO.GetStageCoordinates(stage);

        currentResetMapPos = worldMapSO.GetStageCoordinates(stage);

        LoadStage(stage, true, stage.PlayerStartPosition);
        // ワープ完了後にオートセーブ
        SaveGame(GameProgressManager.SaveType.Auto);
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

        // 次のステージに敵がいるかチェック（フェードアウト前に準備）
        bool nextStageHasEnemy = CheckIfStageHasEnemy(targetStage);
        HashSet<Vector2Int> destroyed = destroyedWalls.ContainsKey(targetStage) ? destroyedWalls[targetStage] : null;

        // ===== 1. プレイヤーをフェードアウト（短め）=====
        if (unitManager.Player != null)
        {
            await unitManager.Player.FadeOutAsync(0.15f, goingDown);
        }

        // 階段遷移用のトランジション準備（同じ座標でアラインメント）
        gridVisualizer.SetupStairTransitionTilemap(targetStage, goingDown, currentPlayerPos, targetPlayerPos, nextStageHasEnemy, destroyed, GameProgressManager.Instance.IsPositionCleared);

        // 遷移先ステージの敵を準備
        Vector3 transitionOffset = gridVisualizer.GetStairTransitionOffset(goingDown);
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

        var cmd = new PlayerStairTransitionCommand(this, currentStageSO, targetStage, currentPlayerPos, currentDirection, targetPlayerPos, currentDirection);
        commandManager.ExecuteCommand(cmd);

        // ===== 3. プレイヤーをフェードイン（短め）=====
        if (unitManager.Player != null)
        {
            await unitManager.Player.FadeInAsync(0.2f, goingDown);
        }

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

        public PlayerStairTransitionCommand(GridManager gm, StageSO source, StageSO target,
            Vector2Int sPos, Direction sDir, Vector2Int tPos, Direction tDir)
        {
            this.gm = gm;
            this.sourceStage = source;
            this.targetStage = target;
            this.sourcePos = sPos;
            this.sourceDir = sDir;
            this.targetPos = tPos;
            this.targetDir = tDir;
        }

        public void Execute()
        {
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
        UpdateWarpTiles(true);
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
                    UpdateWarpTiles();
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
                         UpdateWarpTiles();
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

    private void UpdateWarpTiles(bool quiet = false)
    {
        bool hasAliveEnemy = unitManager.HasAliveEnemy();

        bool currentWarpState = hasAliveEnemy;
        bool stateChanged = previousWarpState != currentWarpState;

        SyncWorldMapStatus();

        gridVisualizer.UpdateWarpTiles(width, height, cellData, hasAliveEnemy, isTransitioning, stateChanged);

        if (!quiet && stateChanged)
        {
            if(!hasAliveEnemy)
            {
                if (showWin != null) showWin.Show();
            }
            gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsPositionCleared, 0.5f, true);
        }

        previousWarpState = currentWarpState;
    }

    private void SyncWorldMapStatus()
    {
        if (worldMapSO == null || currentStageSO == null) return;

        Vector2Int coords = worldMapSO.GetStageCoordinates(currentStageSO);
        if (coords.x == -1) return;

        bool isCleared = !unitManager.HasAliveEnemy();

        bool isNewAreaUnlocked = GameProgressManager.Instance.UpdateStageStatus(coords, true, isCleared, worldMapSO);

        if (isNewAreaUnlocked) popup.Show();
    }

    // TurnManagerから直接呼ばれる代わりにイベント経由になったため不要だが、
    // まだ完全移行できていない部分のために残すか、ラッパーとして残す
    public void UpdateWarpTilesPublic()
    {
        UpdateWarpTiles();
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
            Vector2Int coords = worldMapSO.GetStageCoordinates(kvp.Key);
            if (coords.x != -1)
            {
                allStagesEnemies.Add(new StageEnemyData { stageX = coords.x, stageY = coords.y, enemies = kvp.Value });
            }
        }

        // 2. 次にstageEnemiesMap（訪問済み・実体化済み）を追加。重複がある場合はこちらを優先（上書き）
        foreach (var kvp in stageEnemiesMap)
        {
            StageSO stage = kvp.Key;
            List<Enemy> enemies = kvp.Value;

            // 現在のステージの場合は、UnitManagerの最新リストを使う必要があるかもしれないが、
            // UnitManager.Enemiesの実体はstageEnemiesMap[currentStage]と同じ参照リストのはず。
            // 念のためcurrentStageの場合はUnitManagerから取得するか、参照が同じならそのまま使う。
            // Debug確認: stageEnemiesMap[current] == unitManager.Enemies は true になる設計。

            Vector2Int coords = worldMapSO.GetStageCoordinates(stage);
            if (coords.x == -1) continue;

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
            Vector2Int coords = worldMapSO.GetStageCoordinates(kvp.Key);
            if (coords.x != -1)
            {
                List<Vector2IntData> walls = new List<Vector2IntData>();
                foreach (var wallPos in kvp.Value)
                {
                    walls.Add(new Vector2IntData(wallPos));
                }
                allStagesWalls.Add(new StageWallData { stageX = coords.x, stageY = coords.y, destroyedWalls = walls });
            }
        }
        Debug.Log($"[GridManager] Saving: Captured {allStagesWalls.Count} stages with destroyed walls.");

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
                    StageSO stage = worldMapSO.GetStageAt(wallData.stageX, wallData.stageY);
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
                    StageSO stage = worldMapSO.GetStageAt(stageData.stageX, stageData.stageY);
                    if (stage != null)
                    {
                        loadedStageEnemyData[stage] = stageData.enemies;
                    }
                }
            }

            // Load Initial Stage (Current Stage) based on saved map pos
            Vector2Int mapPos = GameProgressManager.Instance.GetCurrentMapPosition();
            StageSO savedStage = worldMapSO.GetStageAt(mapPos.x, mapPos.y);

            if (savedStage != null)
            {
                currentStageSO = savedStage;
                currentResetMapPos = GameProgressManager.Instance.SavedResetMapPos;

                Vector2Int startPos = GameProgressManager.Instance.SavedPlayerGridPos;
                Direction startDir = GameProgressManager.Instance.SavedPlayerDirection;
                int playerHP = GameProgressManager.Instance.SavedPlayerHP;

                // Load Stage (This will spawn player and enemies)
                LoadStage(currentStageSO, false, startPos, startDir);

                // direction is already set by LoadStage -> GeneratePlayer/WarpToNewStage using startDir
                // unitManager.Player.SetDirection(startDir); // Redundant now but harmless
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