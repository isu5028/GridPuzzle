using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(GridVisualizer))]
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
    private GameObject playerObject = null!;
    private List<GameObject> enemyObjects = new List<GameObject>();
    private Player playerScript = null!;
    private List<Enemy> enemyScripts = new List<Enemy>();
    public GameObject PlayerObject => playerObject;
    public List<GameObject> EnemyObjects => enemyObjects;
    public Player PlayerScript => playerScript;
    public List<Enemy> EnemyScripts => enemyScripts;
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
    }

    public bool IsPositionInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void Start()
    {
        turnManager = GetComponent<TurnManager>();
        commandManager = GetComponent<CommandManager>();

        // debug
        currentStageSO = stageSOs[0];

        GameProgressManager.Instance.InitEntireProgress();
        ContinueGame();
    }

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

        foreach (var enemy in enemyObjects)
        {
            if(enemy != null) Destroy(enemy);
        }
        if(playerObject != null) Destroy(playerObject);

        enemyObjects.Clear();
        enemyScripts.Clear();
        stageEnemiesMap.Clear();
        playerScript = null!;
        playerObject = null!;

        currentStageSO = initialStageSO;
        gridVisualizer.ResetGridParentPosition();
        LoadStage(initialStageSO, true, currentStageSO.PlayerStartPosition);

        if(playerScript != null)
        {
            playerScript.WarpToNewStage(currentStageSO.PlayerStartPosition, currentStageSO.PlayerStartDirection);
        }
    }

    public async UniTask SwitchStageAsync(StageSO nextstage, Direction entryDirection)
    {
        // 次のステージに敵がいるかどうかをチェック（TransitionTilemapのWarp表示用）
        bool nextStageHasEnemy = CheckIfStageHasEnemy(nextstage);
        gridVisualizer.SetupTransitionTilemap(nextstage, entryDirection, nextStageHasEnemy);

        var dirVector = Utility.GetVectorFromDirection(entryDirection);
        // スライド演出用のオフセット
        Vector3 transitionOffset = (Vector3)(dirVector * gridVisualizer.TilemapOffset * 2);

        // Grid全体を動かす準備
        gridVisualizer.MoveGridParent(transitionOffset, TransitionDuration);

        List<GameObject> tempEnemyObjects = CreateTemporaryEnemies(nextstage, transitionOffset);

        // 演出待ち
        await UniTask.Delay(TransitionDelayMs);

        // 後片付け
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
        playerScript.SetHP(playerScript.MaxHP);

        // 2. 現在のステージ情報を更新
        currentStageSO = stage;
        initialStageSO = stage;
        commandManager.Warped();

        currentResetMapPos = worldMapSO.GetStageCoordinates(stage);

        // 3. ステージをロード
        LoadStage(stage, true, stage.PlayerStartPosition);
    }

    private List<GameObject> CreateTemporaryEnemies(StageSO nextstage, Vector3 transitionOffset)
    {
        List<GameObject> tempEnemyObjects = new List<GameObject>();

        if (stageEnemiesMap.ContainsKey(nextstage))
        {
            // パターンA: 既に訪れたことがあるステージ (現在の位置を使用)
            List<Enemy> existingEnemies = stageEnemiesMap[nextstage];

            foreach (var enemyScript in existingEnemies)
            {
                if (enemyScript.IsDead) continue;

                Vector3 worldPos = GridToWorldPosition(enemyScript.CurrentPos);

                GameObject prefab = enemyScript.Type == EnemyType.Patrol
                    ? patolEnemyPrefab
                    : chaserEnemyPrefab;

                var tempScript = InstantiateAndInitializeEnemy(prefab, worldPos - transitionOffset, enemyScript.CurrentPos, enemyScript.CurrentDirection, enemyScript.MaxHP, enemyScript.Order);
                tempEnemyObjects.Add(tempScript.gameObject);
            }
        }
        else
        {
            // パターンB: 初めてのステージ (初期配置を使用)
            List<EnemyStartData> enemyDataListTemp = nextstage.EnemyStartData;
            foreach (var enemyData in enemyDataListTemp)
            {
                Vector3 worldPos = GridToWorldPosition(enemyData.Position);

                GameObject prefab = enemyData.EnemyType == EnemyType.Patrol
                    ? patolEnemyPrefab
                    : chaserEnemyPrefab;

                var tempScript = InstantiateAndInitializeEnemy(prefab, worldPos - transitionOffset, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder);
                tempEnemyObjects.Add(tempScript.gameObject);
            }
        }
        return tempEnemyObjects;
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

        foreach (var enemy in enemyObjects)
        {
            if(enemy != null) enemy.gameObject.SetActive(false);
        }

        width = stage.Width;
        height = stage.Height;

        Direction startDir = isFirstInit ? stage.PlayerStartDirection : playerScript.CurrentDirection;
        Vector2Int pos = isFirstInit ? stage.PlayerStartPosition : startPos;

        if (playerObject == null)
        {
            GeneratePlayer(stage, pos, startDir);
        }
        else
        {
            playerScript.WarpToNewStage(pos, startDir);
        }

        PrepareEnemiesForStage(stage);
        GenerateTilemap(stage);

        turnManager.Initialize(); // ターン数リセットなど
        SyncWorldMapStatus();
    }

    private void PrepareEnemiesForStage(StageSO stage)
    {
        // 既にこのステージの敵が生成済みかチェック
        if (stageEnemiesMap.ContainsKey(stage))
        {
            // --- 既存の敵を再利用 ---
            enemyScripts = stageEnemiesMap[stage];

            // 全員表示する
            foreach (var enemy in enemyScripts)
            {
                if (!enemy.IsDead)
                {
                    enemy.gameObject.SetActive(true);
                }
                else
                {
                    enemy.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            // --- 初めて来るステージなら生成 ---
            enemyScripts = new List<Enemy>();
            GenerateEnemies(stage);

            // 辞書に登録
            stageEnemiesMap.Add(stage, enemyScripts);
        }

        // 生成順序などでソート
        enemyScripts.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private void GenerateTilemap(StageSO stage)
    {
        // 論理データを初期化・構築
        cellData = new CellType[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cellData[x, y] = stage.GetWallAt(x, y);
            }
        }

        // Visualizerに描画依頼
        gridVisualizer.RefreshTilemap(width, height, cellData);

        // Warpタイルを敵の状態に応じて更新
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

    private bool HasAliveEnemy()
    {
        foreach (var enemy in enemyScripts)
        {
            if (enemy != null && !enemy.IsDead)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateWarpTiles()
    {
        // 現在のステージに敵が残っているかチェック
        bool hasAliveEnemy = HasAliveEnemy();

        // 状態が変化したかチェック
        bool currentWarpState = hasAliveEnemy; // true: enemyWarpTile, false: playerWarpTile
        bool stateChanged = previousWarpState != currentWarpState;

        // Visualizerに描画更新依頼
        gridVisualizer.UpdateWarpTiles(width, height, cellData, hasAliveEnemy, isTransitioning, stateChanged);

        // 状態を更新
        previousWarpState = currentWarpState;
        SyncWorldMapStatus();
    }

    private void SyncWorldMapStatus()
    {
        if (worldMapSO == null || currentStageSO == null) return;

        // 1. WorldMapSOを使って、現在のStageSOがマップのどこ(x, y)にあるか調べる
        Vector2Int coords = worldMapSO.GetStageCoordinates(currentStageSO);

        // マップ外のステージ（テスト用など）なら無視
        if (coords.x == -1)
            return;

        // 2. 敵がいるか確認（敵がいない = クリア）
        bool isCleared = !HasAliveEnemy();

        // 3. ProgressManagerに通知
        GameProgressManager.Instance.UpdateStageStatus(coords, true, isCleared);
    }

    // TurnManagerから呼ばれるpublicメソッド
    public void UpdateWarpTilesPublic()
    {
        UpdateWarpTiles();
    }

    private void GeneratePlayer(StageSO stage, Vector2Int pos, Direction dir)
    {
        Vector3 worldPos = GridToWorldPosition(pos);
        playerObject = Instantiate(playerPrefab, worldPos, Quaternion.identity, gridVisualizer.GridParent);
        playerScript = playerObject.GetComponent<Player>();
        // HPはここでMaxが入るが、SwitchStage側で上書きする
        playerScript.Initialize(this,
                                turnManager,
                                commandManager,
                                pos,
                                dir,
                                stage.PlayerMaxHP,
                                stage.PlayerStartOrder);

    }

    private void GenerateEnemies(StageSO stage)
    {
        List<EnemyStartData> enemyDataList = stage.EnemyStartData;

        foreach (var enemyData in enemyDataList)
        {
            Vector3 worldPos = GridToWorldPosition(enemyData.Position);

            GameObject prefab = enemyData.EnemyType == EnemyType.Patrol
                ? patolEnemyPrefab
                : chaserEnemyPrefab;

            var enemyScript = InstantiateAndInitializeEnemy(prefab, worldPos, enemyData.Position, enemyData.Direction, enemyData.MaxHP, enemyData.SetActionOrder);
            enemyScript.name = $"Enemy_{enemyData.EnemyType}_{enemyObjects.Count}";

            enemyObjects.Add(enemyScript.gameObject);
            enemyScripts.Add(enemyScript);
        }
    }

    private Enemy InstantiateAndInitializeEnemy(GameObject prefab, Vector3 worldPos, Vector2Int gridPos, Direction dir, int maxHp, int order)
    {
        GameObject enemy = Instantiate(prefab, worldPos, Quaternion.identity, gridVisualizer.GridParent);
        var enemyScript = enemy.GetComponent<Enemy>();
        enemyScript.Initialize(this, turnManager, commandManager, gridPos, dir, maxHp, order);
        return enemyScript;
    }

    private Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection)
    {
        Vector2Int current = playerScript.CurrentPos;
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

    // Playerから呼ばれる遷移チェック用メソッド
    public async UniTask ExecuteTransition(StageSO stage, Direction direction)
    {
        isTransitioning = true;
        TransitionCommand transitionCommand = new TransitionCommand(this, currentStageSO, stage, direction, playerScript.CurrentPos);
        commandManager.ExecuteCommand(transitionCommand);
        await SwitchStageAsync(stage, direction);
        SwitchStage(stage, direction);
        isTransitioning = false;
    }

    // Undo用: コマンドを追加せずに遷移演出だけを実行してステージを切り替える
    public async UniTask PlayTransitionWithoutCommand(StageSO stage, Direction direction, Vector2Int startPos)
    {
        isTransitioning = true;
        await SwitchStageAsync(stage, direction);
        SwitchStageDirect(stage, startPos);
        isTransitioning = false;
    }

    #region utility methods
    public bool IsCellWalkable(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return false;
        CellType cellType = cellData[pos.x, pos.y];
        return cellType == CellType.Floor || cellType == CellType.Ice || cellType == CellType.DamageFloor || cellType == CellType.Warp;
    }

    public Unit GetUnitAtPosition(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return null;

        // プレイヤーをチェック
        if (playerScript != null && playerScript.CurrentPos == pos)
            return playerScript;

        // 敵をチェック
        foreach (var enemy in enemyScripts)
        {
            if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos)
                return enemy;
        }

        return null;
    }

    // グリッド座標をワールド座標に変換（プロキシ）
    public Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        return gridVisualizer.GridToWorldPosition(gridPos);
    }

    // ワールド座標をグリッド座標に変換（プロキシ）
    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        return gridVisualizer.WorldToGridPosition(worldPos);
    }
    #endregion

    #region Save
    public void SaveGame()
    {
        if (playerScript == null) return;
        GameProgressManager.Instance.SaveProgress(
            playerScript.CurrentPos,
            playerScript.CurrentDirection,
            enemyScripts,
            currentResetMapPos,
            playerScript.HP
        );
    }

    public void ContinueGame()
    {
        // 1. データをロード
        GameProgressManager.Instance.LoadProgress();

        if (!GameProgressManager.Instance.HasLoadedData)
        {
            // データがなければニューゲーム扱い
            Reset();
            return;
        }

        // 2. ワールドマップの現在地から StageSO を特定
        Vector2Int mapPos = GameProgressManager.Instance.GetCurrentMapPosition();
        StageSO savedStage = worldMapSO.GetStageAt(mapPos.x, mapPos.y);

        if (savedStage != null)
        {
            // 3. ロードされたステージ情報をセット
            currentStageSO = savedStage;
            currentResetMapPos = GameProgressManager.Instance.SavedResetMapPos;
            Debug.Log("ResetMapPos" + currentResetMapPos);

            // 4. 保存されていた座標と向きを使ってロード
            Vector2Int startPos = GameProgressManager.Instance.SavedPlayerGridPos;
            Direction startDir = GameProgressManager.Instance.SavedPlayerDirection;
            int playerHP = GameProgressManager.Instance.SavedPlayerHP;

            // LoadStageを呼ぶ
            LoadStageWithCustomStart(savedStage, startPos, startDir, playerHP);
            ApplySavedEnemyData(GameProgressManager.Instance.SavedEnemyList);
            UpdateWarpTilesPublic();

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


        foreach (var enemy in enemyObjects) if(enemy != null) enemy.gameObject.SetActive(false);

        width = stage.Width;
        height = stage.Height;


        if (playerObject == null)
        {
            GeneratePlayer(stage, startPos, startDir);
        }
        else
        {
            playerScript.WarpToNewStage(startPos, startDir);
        }
        playerScript.SetHP(playerHP);
        PrepareEnemiesForStage(stage);

        GenerateTilemap(stage);

        turnManager.Initialize();
        SyncWorldMapStatus();
    }

    private void ApplySavedEnemyData(List<EnemySaveData> savedList)
    {

        if (savedList == null || savedList.Count != enemyScripts.Count)
        {
            return;
        }

        for (int i = 0; i < enemyScripts.Count; i++)
        {
            Enemy enemy = enemyScripts[i];
            EnemySaveData data = savedList[i];

            // 1. 生死状態の復元
            if (data.isDead)
            {
                enemy.ForceKillSilent();
            }
            else
            {
                enemy.ReviveSilent();
                // 2. 生きている場合は位置と向きを復元
                enemy.WarpTo(new Vector2Int(data.gridX, data.gridY), (Direction)data.direction);
                enemy.SetHP(data.currentHP);
            }
        }
    }
    #endregion
}