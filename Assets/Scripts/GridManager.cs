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
    [SerializeField] private SaveFeedback saveFeedback = null!;
    [SerializeField] private WorldMapSO worldMapSO = null!;
    [SerializeField] private StageSO[] stageSOs = null!;
    private StageSO initialStageSO = null!;
    private StageSO currentStageSO = null!;
    public StageSO CurrentStageSO => currentStageSO;
    private Dictionary<StageSO, List<Enemy>> stageEnemiesMap = new Dictionary<StageSO, List<Enemy>>();
    // ロード時に復元されたがまだ実体化されていない敵データ
    private Dictionary<StageSO, List<EnemySaveData>> loadedStageEnemyData = new Dictionary<StageSO, List<EnemySaveData>>();

    [Header("Visualizer")]
    [SerializeField] private GridVisualizer gridVisualizer = null!;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab = null!;
    [SerializeField] private GameObject slimeEnemyPrefab = null!;
    [SerializeField] private GameObject goblinEnemyPrefab = null!;
    [SerializeField] private GameObject skeletonEnemyPrefab = null!;
    [SerializeField] private GameObject zombieEnemyPrefab = null!;

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

    public bool IsPositionInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void Start()
    {
        if (gridVisualizer == null)
            gridVisualizer = GetComponent<GridVisualizer>();

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


    void Update()
    {
        if(Input.GetKeyDown(KeyCode.L))
        {
            GameProgressManager.Instance.DeleteSaveFile(GameProgressManager.SaveType.Manual);
            GameProgressManager.Instance.DeleteSaveFile(GameProgressManager.SaveType.Auto);
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
        // ワープ完了後にオートセーブ
        SaveGame(GameProgressManager.SaveType.Auto);
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
        else if (loadedStageEnemyData.ContainsKey(nextstage))
        {
            // ロードデータから一時的な表示用敵を作成
            List<EnemySaveData> saveDataList = loadedStageEnemyData[nextstage];
            List<EnemyStartData> defaultDataList = nextstage.EnemyStartData; // Prefab情報取得用

            for (int i = 0; i < saveDataList.Count; i++)
            {
                EnemySaveData data = saveDataList[i];
                if (data.isDead) continue; // 死んでいる敵は表示しない

                if (i >= defaultDataList.Count) break;
                EnemyStartData defaultData = defaultDataList[i];

                Vector3 worldPos = GridToWorldPosition(new Vector2Int(data.gridX, data.gridY));
                GameObject prefab = getEnemyPrefab(defaultData.EnemyType);

                var tempScript = InstantiateAndInitializeEnemy(prefab, worldPos - transitionOffset, new Vector2Int(data.gridX, data.gridY), (Direction)data.direction, defaultData.MaxHP, defaultData.SetActionOrder);
                // HPや見た目の詳細まではTransition用なので反映しなくても良いが、必要ならここで行う
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
            default:
                return null!;
        }
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
            // 1. 既に実体化されている場合（セッション内で訪問済み）
            List<Enemy> enemies = stageEnemiesMap[stage];
            unitManager.SetEnemies(enemies);

           // 状態に応じて表示
            foreach (var enemy in enemies)
            {
                // ロード直後の「死んでいるけどActiveでありたい」要件を満たすため、SetActive(true)した上で見た目を調整
                enemy.gameObject.SetActive(true);
                if (enemy.IsDead)
                {
                    enemy.ApplyDeadStateVisual();
                }
            }
        }
        else if (loadedStageEnemyData.ContainsKey(stage))
        {
            // 2. ロードデータはあるがまだ実体化されていない場合
            List<EnemySaveData> saveDataList = loadedStageEnemyData[stage];
            List<Enemy> newEnemies = GenerateEnemiesFromSaveData(stage, saveDataList);
            unitManager.SetEnemies(newEnemies);
            stageEnemiesMap.Add(stage, newEnemies);

            // ロードデータは使い終わったので削除しても良いが、参照用に残しても無害
            loadedStageEnemyData.Remove(stage);
        }
        else
        {
            // 3. 全くの新規（初回訪問）
            List<Enemy> newEnemies = GenerateEnemies(stage);
            unitManager.SetEnemies(newEnemies);
            stageEnemiesMap.Add(stage, newEnemies);
        }

        unitManager.SortEnemiesByOrder();
    }

    private List<Enemy> GenerateEnemiesFromSaveData(StageSO stage, List<EnemySaveData> saveDataList)
    {
        List<Enemy> generatedEnemies = new List<Enemy>();
        // ステージ初期データのPrefab情報を使うため、stage.EnemyStartDataを参照する
        // ただし、SavedDataのIndexとEnemyStartDataの順序が一致している前提
        List<EnemyStartData> defaultDataList = stage.EnemyStartData;

        for (int i = 0; i < saveDataList.Count; i++)
        {
            EnemySaveData data = saveDataList[i];

            // 対応する初期データがない場合はスキップ（ありえないはずだが）
            if (i >= defaultDataList.Count) break;
            EnemyStartData defaultData = defaultDataList[i];

            Vector3 worldPos = GridToWorldPosition(new Vector2Int(data.gridX, data.gridY));
            GameObject prefab = getEnemyPrefab(defaultData.EnemyType);

            // 位置やHPはセーブデータから復元
            var enemyScript = InstantiateAndInitializeEnemy(prefab, worldPos, new Vector2Int(data.gridX, data.gridY), (Direction)data.direction, defaultData.MaxHP, defaultData.SetActionOrder);
            enemyScript.name = $"Enemy_{defaultData.EnemyType}_{i}";
            enemyScript.SetHP(data.currentHP);

            if (data.isDead)
            {
                enemyScript.ForceKillSilent();
            }

            generatedEnemies.Add(enemyScript);
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

        bool isNewAreaUnlocked = GameProgressManager.Instance.UpdateStageStatus(coords, true, isCleared);

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
                    currentHP = enemy.HP.Value
                });
            }

            allStagesEnemies.Add(new StageEnemyData { stageX = coords.x, stageY = coords.y, enemies = enemyDataList });
        }

        GameProgressManager.Instance.SaveProgress(
            unitManager.Player.CurrentPos,
            unitManager.Player.CurrentDirection,
            unitManager.Enemies, // これは現在ステージ用(互換性)
            currentResetMapPos,
            unitManager.Player.HP.Value, // ReactivePropertyなのでValueを取得
            allStagesEnemies, // 全ステージ用
            saveType
        );

        saveFeedback.PlayFeedback();
    }

    public void ContinueGame(GameProgressManager.SaveType saveType = GameProgressManager.SaveType.Manual)
    {
        GameProgressManager.Instance.LoadProgress(saveType);

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
            // ロードした全ステージの敵データを辞書に展開
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

            LoadStageWithCustomStart(savedStage, startPos, startDir, playerHP);
            // ApplySavedEnemyDataは不要になった（PrepareEnemiesForStageで処理されるため）
            // ただし、SavedEnemyList(現在ステージ単体保存)とloadedStageEnemyData(全ステージ保存)の整合性が取れていれば問題ない
            // 今回のSaveGame実装でallStagesEnemiesに現在ステージも含めているので、LoadStageWithCustomStart -> PrepareEnemiesForStage
            // の中で loadedStageEnemyData から復元されるはず。
            // 万が一 loadedStageEnemyData に現在ステージが含まれていない場合（古いセーブデータなど）のために
            // 念のため ApplySavedEnemyData を呼ぶかどうかだが、二重適用を避けるため外す方が安全。
            // 新仕様では loadedStageEnemyData が主となる。

            // 古いセーブデータ互換性: SavedAllStagesEnemiesが空でSavedEnemyListがある場合
            if ((GameProgressManager.Instance.SavedAllStagesEnemies == null || GameProgressManager.Instance.SavedAllStagesEnemies.Count == 0)
                 && GameProgressManager.Instance.SavedEnemyList.Count > 0)
            {
                 ApplySavedEnemyData(GameProgressManager.Instance.SavedEnemyList);
            }
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
                Debug.Log("Enemy is dead");
            }
            else
            {
                enemy.ReviveSilent();
                enemy.WarpTo(new Vector2Int(data.gridX, data.gridY), (Direction)data.direction);
                enemy.SetHP(data.currentHP);
                Debug.Log("Enemy is revived");
            }
        }
    }
    #endregion
}