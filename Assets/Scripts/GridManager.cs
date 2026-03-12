using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UniRx;

[RequireComponent(typeof(GridVisualizer))]
[RequireComponent(typeof(UnitManager))]
public partial class GridManager : MonoBehaviour
{
    [Header("UI Manager")]
    [SerializeField] private GridUIManager uiManager = null!; // Set in Inspector or acquire
    public GridUIManager UIManager => uiManager;

    [SerializeField] private WorldMapSO worldMapSO = null!;
    public WorldMapSO WorldMapSO => worldMapSO;
    [SerializeField] private StageSO[] stageSOs = null!;
    private StageSO initialStageSO = null!;
    private StageSO currentStageSO = null!;
    public StageSO CurrentStageSO => currentStageSO;

    // ステージごとの動的データを管理するクラス
    private StageDataManager stageData = new StageDataManager();
    public StageDataManager StageData => stageData;

    // --- Legacy Accessors (Delegated to StageData) ---
    internal Dictionary<StageSO, List<Enemy>> StageEnemiesMap => stageData.StageEnemiesMap;
    // ロード時に復元されたがまだ実体化されていない敵データ
    internal Dictionary<StageSO, List<EnemySaveData>> LoadedStageEnemyData => stageData.LoadedStageEnemyData;
    // Undo時の敵インスタンス重複を防ぐための待機リスト
    internal Dictionary<StageSO, List<Enemy>> PendingStageEnemies => stageData.PendingStageEnemies;
    // Destroyed Walls: Stage -> Set of positions
    internal Dictionary<StageSO, HashSet<Vector2Int>> DestroyedWalls => stageData.DestroyedWalls;

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
    [SerializeField] private GameObject blockedPrefab = null!;

    private CellType[,] cellData = new CellType[0, 0];
    private int width;
    private int height;

    // UnitManagerへの参照
    private UnitManager unitManager = null!;

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
    internal bool IsTransitioning { get => isTransitioning; set => isTransitioning = value; }

    // Internal accessors for subsystems
    internal StageSO InitialStageSO { get => initialStageSO; set => initialStageSO = value; }
    internal StageSO CurrentStageSOInternal { get => currentStageSO; set => currentStageSO = value; }
    internal Vector2Int CurrentResetMapPos { get => currentResetMapPos; set => currentResetMapPos = value; }
    internal StageSO[] StageSOs => stageSOs;
    internal SaveFeedback SaveFeedback => uiManager.SaveFeedback;

    internal TurnManager TurnManager => turnManager;
    internal CommandManager CommandManager => commandManager;

    // Prefab and Presenter Accessors
    internal GameObject PlayerPrefab => playerPrefab;
    internal StairConfirmationPresenter StairConfirmationPresenter => uiManager.StairConfirmationPresenter;
    internal GameObject SlimeEnemyPrefab => slimeEnemyPrefab;
    internal GameObject GoblinEnemyPrefab => goblinEnemyPrefab;
    internal GameObject SkeletonEnemyPrefab => skeletonEnemyPrefab;
    internal GameObject ZombieEnemyPrefab => zombieEnemyPrefab;
    internal GameObject BoarEnemyPrefab => boarEnemyPrefab;
    internal GameObject CrystalEnemyPrefab => crystalEnemyPrefab;
    internal GameObject ErrorEffectPrefab => errorEffectPrefab;
    public GameObject BlockedPrefab => blockedPrefab;

    private const float TransitionDuration = 0.2f;
    private const int TransitionDelayMs = 250;

    private Vector2Int currentResetMapPos;

    private bool isEventPlaying = false;
    private bool isWaitingForTitleInput = false;
    public bool IsEventPlaying => isEventPlaying;
    public bool IsStairDialogOpen => uiManager.IsStairDialogOpen;

    public bool IsPositionInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void Start()
    {
        // Initialize Subsystems
        saveSystem = new GridSaveSystem(this);
        stageEnemySystem = new StageEnemySystem(this);
        stageTransitionSystem = new StageTransitionSystem(this);
        stairTraversalSystem = new StairTraversalSystem(this);

        if (gridVisualizer == null)
            gridVisualizer = GetComponent<GridVisualizer>();

        gridVisualizer.OnSyncMapActivated += HandleSyncMapActivated;

        if (uiManager == null)
            uiManager = GetComponent<GridUIManager>() ?? GetComponentInChildren<GridUIManager>() ?? FindObjectOfType<GridUIManager>();

        if (uiManager != null)
        {
            uiManager.Initialize();
            uiManager.SubscribeWipeOut(HandleWipeOutHidden);
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

        var allStages = worldMapSO != null ? worldMapSO.GetAllStages() : new List<StageSO>();
        GameProgressManager.Instance.RegisterStagesForClearCheck(allStages.ToArray());
    }

    private void HandleSyncMapActivated()
    {
        PlayEndingSequence().Forget();
    }

    private async UniTaskVoid PlayEndingSequence()
    {
        isEventPlaying = true;

        await uiManager.PlayEndingSequence();

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

        if (uiManager != null)
        {
            uiManager.UnsubscribeWipeOut(HandleWipeOutHidden);
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
            gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsHierarchicalCellCleared, 0f, true);
        }
#endif
    }

    public void Reset()
    {
        isEventPlaying = false;
        uiManager.HideWipeOut();

        bool wasPlayerHealed = false;
        if (unitManager.Player != null)
        {
            wasPlayerHealed = unitManager.Player.HP.Value < unitManager.Player.MaxHP || unitManager.Player.IsDead;
        }

        // Determine which stage to reset to (Current Stage)
        StageSO stageToReset = currentStageSO;
        if (stageToReset == null)
        {
             if (initialStageSO == null)
             {
                 initialStageSO = worldMapSO.GetStageAt(currentResetMapPos.x, currentResetMapPos.y);
             }
             stageToReset = initialStageSO;
        }

        gridVisualizer.ClearAllTiles(true);
        GameProgressManager.Instance.ResetStageCleared();

        unitManager.ClearAllUnits(); // Destroys Player and current active enemies
        ResetAllEnemies(); // Destroys any remaining enemies and clears saved data to reset positions
        DestroyedWalls.Clear(); // Return walls to normal on reset

        currentStageSO = stageToReset; // Ensure current is set
        gridVisualizer.ResetGridParentPosition();

        // Determine reset position: Prioritize StairUp, fallback to PlayerStartPosition
        Vector2Int resetPos = stageToReset.PlayerStartPosition;
        Vector2Int? stairUpPos = stageToReset.GetStairUpPosition();
        if (stairUpPos.HasValue)
        {
            resetPos = stairUpPos.Value;
        }

        LoadStage(stageToReset, true, resetPos, null, true, true);

        if(unitManager.Player != null)
        {
            unitManager.Player.WarpToNewStage(resetPos, stageToReset.PlayerStartDirection);
            // Ensure HP is full on reset
            unitManager.Player.SetHP(unitManager.Player.MaxHP);

            if (wasPlayerHealed)
            {
                Debug.Log("Player HP Restored via Reset");
                unitManager.Player.PlayHealEffect();
            }
        }

        // 敵のHPも全回復させる（死亡状態の敵も含めて全員リセット）
        foreach (var enemy in unitManager.Enemies)
        {
            if (enemy != null)
            {
                DOTween.Kill(enemy.gameObject); // 残存するDOTweenアニメーションを強制停止
                enemy.Revive();                 // 先に復活処理（isDead=false、ビジュアルリセット）
                enemy.SetHP(enemy.MaxHP);       // その後HP全回復
            }
        }
    }

    /// <summary>
    /// Undo履歴をクリアする
    /// </summary>
    public void ClearUndoHistory()
    {
        commandManager.Warped();
    }

    /// <summary>
    /// プレイヤーのHPを最大まで回復する
    /// </summary>
    public void RestorePlayerHP()
    {
        if (unitManager.Player != null)
        {
            bool wasHealed = unitManager.Player.HP.Value < unitManager.Player.MaxHP || unitManager.Player.IsDead;
            unitManager.Player.Revive();
            unitManager.Player.SetHP(unitManager.Player.MaxHP);
            if (wasHealed)
            {
                Debug.Log("Player HP Restored");
                unitManager.Player.PlayHealEffect();
            }
        }
    }

    #region Grid Data / Cell Type
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

    public bool IsBoundaryCoordinate(StageSO stage, Vector2Int pos)
    {
        if (pos.x == -1 && pos.y >= 0 && pos.y < stage.Height) return true;
        if (pos.x == stage.Width && pos.y >= 0 && pos.y < stage.Height) return true;
        if (pos.y == -1 && pos.x >= 0 && pos.x < stage.Width) return true;
        if (pos.y == stage.Height && pos.x >= 0 && pos.x < stage.Width) return true;
        return false;
    }

    public StageSO GetAdjacentStageFromBoundary(StageSO stage, Vector2Int pos)
    {
        if (pos.x == -1) return stage.GetAdjacentStage(Direction.Left);
        if (pos.x == stage.Width) return stage.GetAdjacentStage(Direction.Right);
        if (pos.y == -1) return stage.GetAdjacentStage(Direction.Down);
        if (pos.y == stage.Height) return stage.GetAdjacentStage(Direction.Up);
        return null;
    }

    internal void LoadStage(StageSO stage, bool isFirstInit, Vector2Int startPos, Direction? customStartDir = null, bool resetTurnManager = true, bool keepTutorial = false)
    {
        gridVisualizer.ResetGridParentPosition(); // Ensure parent is at zero before loading
        gridVisualizer.ClearAllTiles(keepTutorial);

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

        stageEnemySystem.PrepareEnemiesForStage(stage);
        GenerateTilemap(stage, keepTutorial);

        if (resetTurnManager)
        {
            turnManager.Initialize();
        }
        SyncWorldMapStatus();
        CheckAndResolveCollisions();
        gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsHierarchicalCellCleared, 0f, false, !isTransitioning);
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
            // Execute InstantDeathCommand
            // visual effect is handled inside the command now
            commandManager.ExecuteCommand(new InstantDeathCommand(unit, 999, errorEffectPrefab));
        }
    }

    private void GenerateTilemap(StageSO stage, bool keepTutorial = false)
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

        HashSet<Vector2Int> destroyed = DestroyedWalls.ContainsKey(stage) ? DestroyedWalls[stage] : null;
        gridVisualizer.RefreshTilemap(width, height, cellData, stage, destroyed, GameProgressManager.Instance.IsHierarchicalCellCleared, keepTutorial);
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
        if (DestroyedWalls.ContainsKey(stage) && DestroyedWalls[stage].Contains(pos))
        {
            return CellType.Floor;
        }

        // 2. Default from SO
        return stage.GetWallAt(pos.x, pos.y);
    }
    #endregion

    #region Wall Management
    public void BreakWall(StageSO stage, Vector2Int pos)
    {
        if (!DestroyedWalls.ContainsKey(stage))
        {
            DestroyedWalls[stage] = new HashSet<Vector2Int>();
        }

        if (!DestroyedWalls[stage].Contains(pos))
        {
            DestroyedWalls[stage].Add(pos);

            // Reflect if current stage
            if (stage == currentStageSO)
            {
                // 壁破壊エフェクトをタイル更新前に再生（タイルが消える前にスプライトを取得するため）
                // OOB境界壁（x=-1, x=width, y=-1, y=height）も含めてエフェクトを出す
                gridVisualizer.PlayWallBreakEffect(pos);

                if(pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                {
                    cellData[pos.x, pos.y] = CellType.Floor;
                }

                // Visualizer update (Always refresh to handle OOB as well)
                HashSet<Vector2Int> destroyed = DestroyedWalls.ContainsKey(stage) ? DestroyedWalls[stage] : null;
                gridVisualizer.RefreshTilemap(width, height, cellData, stage, destroyed, GameProgressManager.Instance.IsHierarchicalCellCleared);
                OnEnemyStateChanged();
            }

            // --- Recursive Break for Adjacent Stage (Virtual Walls ONLY) ---
            // Only propagate to the "Outermost" wall (Virtual Wall) of the neighbor.
            // Do NOT break inner walls (Real Walls) automatically, so units might crash into them.

            // Left Neighbor Interactions: Breaking Left Boundary (-1) -> Breaks Neighbor's Right Boundary (Width)
            if (pos.x == -1)
            {
                // 隣接ステージの境界壁が破壊される = 現在ステージ側も境界エフェクト（PropagateBreakが呼ぶ）
                PropagateBreak(stage, Direction.Left, new Vector2Int(stage.GetAdjacentStage(Direction.Left)?.Width ?? 0, pos.y));
            }

            // Right Neighbor Interactions: Breaking Right Boundary (Width) -> Breaks Neighbor's Left Boundary (-1)
            if (pos.x == stage.Width)
            {
                PropagateBreak(stage, Direction.Right, new Vector2Int(-1, pos.y));
            }

            // Down Neighbor Interactions: Breaking Bottom Boundary (-1) -> Breaks Neighbor's Top Boundary (Height)
            if (pos.y == -1)
            {
                PropagateBreak(stage, Direction.Down, new Vector2Int(pos.x, stage.GetAdjacentStage(Direction.Down)?.Height ?? 0));
            }

            // Up Neighbor Interactions: Breaking Top Boundary (Height) -> Breaks Neighbor's Bottom Boundary (-1)
            if (pos.y == stage.Height)
            {
                PropagateBreak(stage, Direction.Up, new Vector2Int(pos.x, -1));
            }
        }
    }

    private void PropagateBreak(StageSO currentStage, Direction dir, Vector2Int targetPos)
    {
        StageSO neighbor = currentStage.GetAdjacentStage(dir);
        if (neighbor == null) return;

        // Only propagate if we hit valid coordinates (Prevent recursive errors if width/height 0, though unlikely)
        // Also check if valid break target

        // Infinite recursion is prevented by the `Contains` check in BreakWall.
        BreakWall(neighbor, targetPos);
    }

    public void RestoreWall(StageSO stage, Vector2Int pos)
    {
        if (DestroyedWalls.ContainsKey(stage))
        {
            if (DestroyedWalls[stage].Remove(pos))
            {
                // Reflect if current
                if (stage == currentStageSO)
                {
                     if(pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                     {
                         // Re-read default
                         cellData[pos.x, pos.y] = stage.GetWallAt(pos.x, pos.y);
                     }

                     HashSet<Vector2Int> destroyed = DestroyedWalls.ContainsKey(stage) ? DestroyedWalls[stage] : null;
                     gridVisualizer.RefreshTilemap(width, height, cellData, stage, destroyed, GameProgressManager.Instance.IsHierarchicalCellCleared);
                     OnEnemyStateChanged();
                }

                // --- Recursive Restore for Adjacent Stage (Virtual Walls ONLY) ---
                if (pos.x == -1) PropagateRestore(stage, Direction.Left, new Vector2Int(stage.GetAdjacentStage(Direction.Left)?.Width ?? 0, pos.y));
                if (pos.x == stage.Width) PropagateRestore(stage, Direction.Right, new Vector2Int(-1, pos.y));
                if (pos.y == -1) PropagateRestore(stage, Direction.Down, new Vector2Int(pos.x, stage.GetAdjacentStage(Direction.Down)?.Height ?? 0));
                if (pos.y == stage.Height) PropagateRestore(stage, Direction.Up, new Vector2Int(pos.x, -1));
            }
        }
    }

    private void PropagateRestore(StageSO currentStage, Direction dir, Vector2Int targetPos)
    {
        StageSO neighbor = currentStage.GetAdjacentStage(dir);
        if (neighbor == null) return;

        RestoreWall(neighbor, targetPos);
    }
    #endregion

    #region World Map Sync
    public void SyncStageStatus(StageSO stage)
    {
        if (worldMapSO == null || stage == null) return;

        Vector2Int coords = worldMapSO.GetStageCoordinates(stage);
        if (coords.x == -1) return;

        bool isCleared = GameProgressManager.Instance.IsStageFullyDefeated(stage);
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
            gridVisualizer.UpdateSyncMapVisuals(GameProgressManager.Instance.IsHierarchicalCellCleared, 0.5f, true);
        }

        previousEnemyAliveState = currentState;
    }

    internal void SyncWorldMapStatus()
    {
        if (currentStageSO == null) return;

        bool popupShown = false;
        bool dataSavedThisFrame = false;

        GameProgressManager.Instance.SetCurrentMapPosition(currentStageSO.MapPosition);

        // 現在のステージに到達した時点で、階層マップのセルをアンロックする
        // これにより境界遷移（ステージ端からの移動）でも正しくレイヤーが解放される
        int column = currentStageSO.StairColumn;
        int depth = currentStageSO.MapPosition.y;
        GameProgressManager.Instance.UnlockCellByStairDescent(column, depth);

        foreach (var stage in stageSOs)
        {
            if (stage == null) continue;

            bool isCurrent = (stage == currentStageSO);
            bool isStageCleared;
            if (isCurrent)
            {
                isStageCleared = GameProgressManager.Instance.IsStageFullyDefeated(stage)
                              || GameProgressManager.Instance.IsHierarchicalCellCleared(stage.MapPosition);
            }
            else
            {
                isStageCleared = GameProgressManager.Instance.IsHierarchicalCellCleared(stage.MapPosition);
            }

            if (worldMapSO != null)
            {
                Vector2Int coords = worldMapSO.GetStageCoordinates(stage);
                if (coords.x != -1)
                {
                    bool isNewAreaUnlocked = GameProgressManager.Instance.UpdateStageStatus(coords, isCurrent, isStageCleared, worldMapSO);

                    if (isNewAreaUnlocked && !popupShown)
                    {
                        uiManager.ShowPopup();
                        popupShown = true;
                    }
                }
            }

            if (isStageCleared)
            {
                Vector2Int mapPos = stage.MapPosition;
                if (!GameProgressManager.Instance.IsHierarchicalCellCleared(mapPos))
                {
                    GameProgressManager.Instance.MarkHierarchicalCellAsCleared(mapPos);
                    dataSavedThisFrame = true;
                }
            }
        }

        if (dataSavedThisFrame)
        {
            SaveGame(GameProgressManager.SaveType.Auto);
        }
    }
    #endregion

    #region Player Generation
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
    #endregion

    #region Utility Methods (Proxy to UnitManager / Visualizer)
    public bool IsCellWalkable(Vector2Int pos)
    {
        // 1. Check Walkable (Floor or Broken Wall)
        // IsWalkableAt handles DestroyedWalls for OOB coordinates
        if (!IsWalkableAt(currentStageSO, pos)) return false;

        // 2. Check Unit Occupancy
        // Note: GetUnitAtPosition checks bounds internally and returns null if OOB, so it's safe.
        if (GetUnitAtPosition(pos) != null) return false;

        return true;
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

    // ==========================================
    // Save System Delegation
    // ==========================================
    private ISaveSystem saveSystem;

    public void SaveGame(GameProgressManager.SaveType saveType = GameProgressManager.SaveType.Manual) => saveSystem.SaveGame(saveType);
    public void ContinueGame(GameProgressManager.SaveType saveType) => saveSystem.ContinueGame(saveType);
    public void ResetAndStartNewGame() => saveSystem.ResetAndStartNewGame();

    public void LoadStageWithCustomStart(StageSO stage, Vector2Int startPos, Direction dir, bool resetEnemy = false)
        => saveSystem.LoadStageWithCustomStart(stage, startPos, dir, resetEnemy);

    // ==========================================
    // Stage Enemy System Delegation
    // ==========================================
    private IStageEnemySystem stageEnemySystem;
    internal IStageEnemySystem StageEnemySystem => stageEnemySystem;

    public Enemy GetEnemyAt(StageSO stage, Vector2Int pos) => stageEnemySystem.GetEnemyAt(stage, pos);
    public void EnsureStageEnemiesLoaded(StageSO stage) => stageEnemySystem.EnsureStageEnemiesLoaded(stage);
    public bool CheckIfStageHasEnemy(StageSO stage) => stageEnemySystem.CheckIfStageHasEnemy(stage);
    public StageSO GetStageOfEnemy(Enemy enemy) => stageEnemySystem.GetStageOfEnemy(enemy);
    public List<Enemy> GetEnemiesOnStage(StageSO stage) => stageEnemySystem.GetEnemiesOnStage(stage);

    internal void ResetAllEnemies() => stageEnemySystem.ResetAllEnemies();
    internal void ApplySavedEnemyData(StageSO stage, List<EnemySaveData> savedEnemies) => stageEnemySystem.ApplySavedEnemyData(stage, savedEnemies);
    internal void AddPendingEnemy(StageSO stage, Enemy enemy) => stageEnemySystem.AddPendingEnemy(stage, enemy);
    public int GetMaxEnemyOrderInStage(StageSO stage) => stageEnemySystem.GetMaxEnemyOrderInStage(stage);


    // ==========================================
    // Stage Transition System Delegation
    // ==========================================
    private IStageTransitionSystem stageTransitionSystem;
    internal IStageTransitionSystem StageTransitionSystem => stageTransitionSystem;

    public UniTask SwitchStageAsync(StageSO nextstage, Direction entryDirection) => stageTransitionSystem.SwitchStageAsync(nextstage, entryDirection);
    public UniTask WarpToStageAsync(StageSO stage, Vector2Int gridPos, Direction dir, System.Action onComplete = null) => stageTransitionSystem.WarpToStageAsync(stage, gridPos, dir, onComplete);
    public void WarpToStage(StageSO stage, Vector2Int gridPos, Direction dir) => stageTransitionSystem.WarpToStage(stage, gridPos, dir);
    public void WarpToStage(StageSO stage) => WarpToStage(stage, stage.PlayerStartPosition, stage.PlayerStartDirection);
    public void WarpToStageAtStairUp(StageSO stage) => stageTransitionSystem.WarpToStageAtStairUp(stage);
    public UniTask WarpToStageAtStairUpAsync(StageSO stage) => stageTransitionSystem.WarpToStageAtStairUpAsync(stage);
    public UniTask WarpWithAnimationAsync(StageSO targetStage, Vector2Int targetPos, Direction targetDir, bool restoreHP = false, GridCalculation.WarpDirection? overrideWarpDir = null) => stageTransitionSystem.WarpWithAnimationAsync(targetStage, targetPos, targetDir, restoreHP, overrideWarpDir);
    public void SwitchStage(StageSO nextstage, Direction entryDirection) => stageTransitionSystem.SwitchStage(nextstage, entryDirection);
    public void SwitchStageDirect(StageSO nextstage, Direction entryDirection) => stageTransitionSystem.SwitchStageDirect(nextstage, entryDirection);
    public UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection) => stageTransitionSystem.PlayTransitionWithoutCommand(nextStage, entryDirection);
    public UniTask PlayTransitionWithoutCommand(StageSO nextStage, Direction entryDirection, Vector2Int entryPos) => stageTransitionSystem.PlayTransitionWithoutCommand(nextStage, entryDirection, entryPos);
    public void PrepareEnemiesForTransition(StageSO nextstage, Vector3 transitionOffset) => stageTransitionSystem.PrepareEnemiesForTransition(nextstage, transitionOffset);
    public bool CheckAndPerformBoundaryTransition(Unit unit) => stageTransitionSystem.CheckAndPerformBoundaryTransition(unit);
    public bool CheckAndPerformBoundaryTransition(Unit unit, Vector2Int? overrideTargetPos) => stageTransitionSystem.CheckAndPerformBoundaryTransition(unit, overrideTargetPos);
    public bool CheckAndPerformEnemyBoundaryTransfer(Enemy enemy) => stageTransitionSystem.CheckAndPerformEnemyBoundaryTransfer(enemy);

    // ==========================================
    // Stair Traversal System Delegation
    // ==========================================
    private IStairTraversalSystem stairTraversalSystem;
    internal IStairTraversalSystem StairTraversalSystem => stairTraversalSystem;

    #endregion

    #region Stair Traversal
    public bool CheckAndShowStairDialog() => stairTraversalSystem.CheckAndShowStairDialog();
    public (StageSO targetStage, Vector2Int targetPos)? GetEnemyStairDestination(Enemy enemy, Vector2Int stairPos, Direction approachDirection) => stairTraversalSystem.GetEnemyStairDestination(enemy, stairPos, approachDirection);
    public UniTask MoveEnemyToStage(Enemy enemy, StageSO targetStage, Vector2Int targetPos, Direction direction) => stairTraversalSystem.MoveEnemyToStage(enemy, targetStage, targetPos, direction);
    public bool IsPositionOccupiedInStage(StageSO stage, Vector2Int pos) => stairTraversalSystem.IsPositionOccupiedInStage(stage, pos);

    #endregion
}