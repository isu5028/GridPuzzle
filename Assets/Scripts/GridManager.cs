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
    [SerializeField] private WorldMapSO worldMapSO = null!;
    [SerializeField] private StageSO[] stageSOs = null!;
    private StageSO initialStageSO = null!;
    private StageSO currentStageSO = null!;
    public StageSO CurrentStageSO => currentStageSO;
    private Dictionary<StageSO, List<Enemy>> stageEnemiesMap = new Dictionary<StageSO, List<Enemy>>();

    [Header("Visualizer")]
    [SerializeField] private GridVisualizer gridVisualizer = null!;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab = null!;
    [SerializeField] private GameObject patolEnemyPrefab = null!;
    [SerializeField] private GameObject chaserEnemyPrefab = null!;

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
    public bool IsTransitioning => isTransitioning;

    private const float TransitionDuration = 0.2f;
    private const int TransitionDelayMs = 250;

    private bool previousWarpState = true; // true: enemyWarpTile, false: playerWarpTile
    private Vector2Int currentResetMapPos;

    private void Awake()
    {
        if (gridVisualizer == null)
            gridVisualizer = GetComponent<GridVisualizer>();

        unitManager = GetComponent<UnitManager>();
    }

    public bool IsPositionInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void Start()
    {
        turnManager = GetComponent<TurnManager>();
        commandManager = GetComponent<CommandManager>();

        // UnitManagerのイベント購読 (UniRx)
        unitManager.OnUnitStateChanged
             .Subscribe(_ => UpdateWarpTiles())
             .AddTo(this);

        // debug
        currentStageSO = stageSOs[0];

        GameProgressManager.Instance.InitEntireProgress();
        ContinueGame();
    }

    // OnDestroyはUniRxの.AddTo(this)を使っていれば基本不要だが、明示的に購読解除してもよい
    // 今回はAddTo(this)で自動解除させるのでメソッドごと削除してもいいが、前回のイベント登録解除のコードが残るなら削除する

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            SaveGame();
            Debug.Log("Game progress saved.");
        }
        else if(Input.GetKeyDown(KeyCode.O))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            Debug.Log("Game progress loaded.");
        }
        else if(Input.GetKeyDown(KeyCode.L))
        {
            GameProgressManager.Instance.DeleteSaveFile();
            Debug.Log("Game progress reset.");
        }
    }

    public void Reset()
    {
        if (initialStageSO == null)
        {
            initialStageSO = worldMapSO.GetStageAt(currentResetMapPos.x, currentResetMapPos.y);
        }

        gridVisualizer.ClearAllTiles();
        GameProgressManager.Instance.ResetStageCleared();

        unitManager.ClearAllUnits();
        stageEnemiesMap.Clear();

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
        gridVisualizer.SetupTransitionTilemap(nextstage, entryDirection, nextStageHasEnemy);

        var dirVector = Utility.GetVectorFromDirection(entryDirection);
        Vector3 transitionOffset = (Vector3)(dirVector * gridVisualizer.TilemapOffset * 2);

        gridVisualizer.MoveGridParent(transitionOffset, TransitionDuration);

        List<GameObject> tempEnemyObjects = CreateTemporaryEnemies(nextstage, transitionOffset);

        await UniTask.Delay(TransitionDelayMs);

        foreach (var enemy in tempEnemyObjects)
        {
            if(enemy != null) Destroy(enemy);
        }

        gridVisualizer.ResetGridParentPosition();
    }

    public void WarpToStage(StageSO stage)
    {
        stageEnemiesMap.Clear();
        GameProgressManager.Instance.ResetStageCleared();
        unitManager.Player.SetHP(unitManager.Player.MaxHP);

        currentStageSO = stage;
        initialStageSO = stage;
        commandManager.Warped();

        currentResetMapPos = worldMapSO.GetStageCoordinates(stage);

        LoadStage(stage, true, stage.PlayerStartPosition);
    }

    private List<GameObject> CreateTemporaryEnemies(StageSO nextstage, Vector3 transitionOffset)
    {
        List<GameObject> tempEnemyObjects = new List<GameObject>();

        if (stageEnemiesMap.ContainsKey(nextstage))
        {
             // 既存の敵リストから一時的な表示用敵を作成
            List<Enemy> existingEnemies = stageEnemiesMap[nextstage];

            foreach (var enemyScript in existingEnemies)
            {
                if (enemyScript.IsDead) continue;

                Vector3 worldPos = GridToWorldPosition(enemyScript.CurrentPos);

                GameObject prefab = getEnemyPrefab(enemyScript.Type);

                var tempScript = InstantiateAndInitializeEnemy(prefab, worldPos - transitionOffset, enemyScript.CurrentPos, enemyScript.CurrentDirection, enemyScript.MaxHP, enemyScript.Order);
                tempEnemyObjects.Add(tempScript.gameObject);
            }
        }
        else
        {
            // 初期データから一時的な表示用敵を作成
            List<EnemyStartData> enemyDataListTemp = nextstage.EnemyStartData;
            foreach (var enemyData in enemyDataListTemp)
            {
                Vector3 worldPos = GridToWorldPosition(enemyData.Position);

                GameObject prefab = getEnemyPrefab(enemyData.EnemyType);

                var tempScript = InstantiateAndInitializeEnemy(prefab, worldPos - transitionOffset, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder);
                tempEnemyObjects.Add(tempScript.gameObject);
            }
        }
        return tempEnemyObjects;
    }

    private GameObject getEnemyPrefab(EnemyType type)
    {
        return type == EnemyType.Patrol ? patolEnemyPrefab : chaserEnemyPrefab;
    }

    public void SwitchStage(StageSO nextstage, Direction entryDirection)
    {
        Vector2Int newStartPos = CalculateEntryPosition(nextstage, entryDirection);
        currentStageSO = nextstage;
        LoadStage(nextstage, false, newStartPos);
        turnManager.OnPlayerActionComplete().Forget();
    }

    public void SwitchStageDirect(StageSO stage, Vector2Int startPos)
    {
        currentStageSO = stage;
        LoadStage(stage, false, startPos);
    }

    private void LoadStage(StageSO stage, bool isFirstInit, Vector2Int startPos)
    {
        gridVisualizer.ClearAllTiles();

        // 現在の敵を非表示にする
        unitManager.SetAllEnemiesActive(false);

        width = stage.Width;
        height = stage.Height;

        Direction startDir = isFirstInit ? stage.PlayerStartDirection : unitManager.Player.CurrentDirection;
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

        turnManager.Initialize();
        SyncWorldMapStatus();
    }

    private void PrepareEnemiesForStage(StageSO stage)
    {
        if (stageEnemiesMap.ContainsKey(stage))
        {
            // 既存の敵リストをUnitManagerにセット
            List<Enemy> enemies = stageEnemiesMap[stage];
            unitManager.SetEnemies(enemies);

           // 状態に応じて表示
            foreach (var enemy in enemies)
            {
                bool shouldActive = !enemy.IsDead; // 死んでいる場合でもActiveにするかどうかはゲームデザインによるが、現状はActive(true)にしておく
                enemy.gameObject.SetActive(true);
            }
        }
        else
        {
            // 新規生成
            // 一旦UnitManagerのリストを空にするか、新規リストを用意してから登録する
            // UnitManager.SetEnemies で上書きする形にする
            List<Enemy> newEnemies = GenerateEnemies(stage);
            unitManager.SetEnemies(newEnemies);
            stageEnemiesMap.Add(stage, newEnemies);
        }

        unitManager.SortEnemiesByOrder();
    }

    private void GenerateTilemap(StageSO stage)
    {
        cellData = new CellType[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cellData[x, y] = stage.GetWallAt(x, y);
            }
        }

        gridVisualizer.RefreshTilemap(width, height, cellData);
        UpdateWarpTiles();
    }

    private bool CheckIfStageHasEnemy(StageSO stage)
    {
        if (stageEnemiesMap.TryGetValue(stage, out var enemies))
        {
             foreach (var enemy in enemies)
             {
                 if(enemy != null && !enemy.IsDead) return true;
             }
             return false;
        }
        return stage.EnemyStartData.Count > 0;
    }

    private void UpdateWarpTiles()
    {
        bool hasAliveEnemy = unitManager.HasAliveEnemy();

        bool currentWarpState = hasAliveEnemy;
        bool stateChanged = previousWarpState != currentWarpState;

        gridVisualizer.UpdateWarpTiles(width, height, cellData, hasAliveEnemy, isTransitioning, stateChanged);

        previousWarpState = currentWarpState;
        SyncWorldMapStatus();
    }

    private void SyncWorldMapStatus()
    {
        if (worldMapSO == null || currentStageSO == null) return;

        Vector2Int coords = worldMapSO.GetStageCoordinates(currentStageSO);
        if (coords.x == -1) return;

        bool isCleared = !unitManager.HasAliveEnemy();

        GameProgressManager.Instance.UpdateStageStatus(coords, true, isCleared);
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

            var enemyScript = InstantiateAndInitializeEnemy(prefab, worldPos, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder);
            enemyScript.name = $"Enemy_{enemyData.EnemyType}_{count}";
            count++;

            generatedEnemies.Add(enemyScript);
        }
        return generatedEnemies;
    }

    private Enemy InstantiateAndInitializeEnemy(GameObject prefab, Vector3 worldPos, Vector2Int gridPos, Direction dir, int maxHp, int order)
    {
        GameObject enemy = Instantiate(prefab, worldPos, Quaternion.identity, gridVisualizer.GridParent);
        var enemyScript = enemy.GetComponent<Enemy>();
        // Note: Initialize内でGridManagerを渡しているが、UnitManagerを使うようにUnit側も修正が必要かもしれない
        // 一旦はGridManagerのプロキシメソッド(GetUnitAtPositionなど)経由でUnitManagerにアクセスする形になる
        enemyScript.Initialize(this, turnManager, commandManager, gridPos, dir, maxHp, order);
        return enemyScript;
    }

    private Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection)
    {
        Vector2Int current = unitManager.Player.CurrentPos;
        int targetX = 0;
        int targetY = 0;

        switch (fromDirection)
        {
            case Direction.Right: // 左端に出る
                targetX = 0;
                targetY = Mathf.Clamp(current.y, 0, nextStage.Height - 1);
                break;
            case Direction.Left: // 右端に出る
                targetX = nextStage.Width - 1;
                targetY = Mathf.Clamp(current.y, 0, nextStage.Height - 1);
                break;
            case Direction.Up: // 下端に出る
                targetX = Mathf.Clamp(current.x, 0, nextStage.Width - 1);
                targetY = 0;
                break;
            case Direction.Down: // 上端に出る
                targetX = Mathf.Clamp(current.x, 0, nextStage.Width - 1);
                targetY = nextStage.Height - 1;
                break;
        }
        return new Vector2Int(targetX, targetY);
    }

    public bool TryTransition(Direction direction)
    {
        StageSO nextStage = currentStageSO.GetAdjacentStage(direction);
        if (nextStage != null)
        {
            ExecuteTransition(nextStage, direction).Forget();
            return true;
        }
        return false;
    }

    public async UniTask ExecuteTransition(StageSO stage, Direction direction)
    {
        isTransitioning = true;
        TransitionCommand transitionCommand = new TransitionCommand(this, currentStageSO, stage, direction, unitManager.Player.CurrentPos);
        commandManager.ExecuteCommand(transitionCommand);
        await SwitchStageAsync(stage, direction);
        SwitchStage(stage, direction);
        isTransitioning = false;
    }

    public async UniTask PlayTransitionWithoutCommand(StageSO stage, Direction direction, Vector2Int startPos)
    {
        isTransitioning = true;
        await SwitchStageAsync(stage, direction);
        SwitchStageDirect(stage, startPos);
        isTransitioning = false;
    }

    #region utility methods (Proxy to UnitManager / Visualizer)
    public bool IsCellWalkable(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return false;
        CellType cellType = cellData[pos.x, pos.y];
        return cellType == CellType.Floor || cellType == CellType.Ice || cellType == CellType.DamageFloor || cellType == CellType.Warp;
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

    #region Save
    public void SaveGame()
    {
        if (unitManager.Player == null) return;
        GameProgressManager.Instance.SaveProgress(
            unitManager.Player.CurrentPos,
            unitManager.Player.CurrentDirection,
            unitManager.Enemies,
            currentResetMapPos,
            unitManager.Player.HP.Value // ReactivePropertyなのでValueを取得
        );
    }

    public void ContinueGame()
    {
        GameProgressManager.Instance.LoadProgress();

        if (!GameProgressManager.Instance.HasLoadedData)
        {
            Reset();
            return;
        }

        Vector2Int mapPos = GameProgressManager.Instance.GetCurrentMapPosition();
        StageSO savedStage = worldMapSO.GetStageAt(mapPos.x, mapPos.y);

        if (savedStage != null)
        {
            currentStageSO = savedStage;
            currentResetMapPos = GameProgressManager.Instance.SavedResetMapPos;

            Vector2Int startPos = GameProgressManager.Instance.SavedPlayerGridPos;
            Direction startDir = GameProgressManager.Instance.SavedPlayerDirection;
            int playerHP = GameProgressManager.Instance.SavedPlayerHP;

            LoadStageWithCustomStart(savedStage, startPos, startDir, playerHP);
            ApplySavedEnemyData(GameProgressManager.Instance.SavedEnemyList);
            UpdateWarpTiles(); // イベント発火前かもしれないので手動更新しておく

            Debug.Log($"Game Continued at {savedStage.name} hp:{playerHP}");
        }
        else
        {
            Debug.LogError("Saved stage not found in WorldMapSO!");
            Reset();
        }
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
        SyncWorldMapStatus();
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
            }
            else
            {
                enemy.ReviveSilent();
                enemy.WarpTo(new Vector2Int(data.gridX, data.gridY), (Direction)data.direction);
                enemy.SetHP(data.currentHP);
            }
        }
    }
    #endregion
}