using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class GridManager : MonoBehaviour
{
    [SerializeField] private WorldMapSO worldMapSO = null!;
    [SerializeField] private StageSO[] stageSOs = null!;
    private StageSO initialStageSO = null!;
    private StageSO currentStageSO = null!;
    public StageSO CurrentStageSO => currentStageSO;
    private Dictionary<StageSO, List<Enemy>> stageEnemiesMap = new Dictionary<StageSO, List<Enemy>>();

    [Header("Tilemap")]

    [SerializeField] private Tilemap tilemap = null!;
    [SerializeField] private Tilemap transitionTilemap = null!;
    [SerializeField] private TileBase wallTile = null!;
    [SerializeField] private TileBase floorTile = null!;
    [SerializeField] private TileBase playerWarpTile = null!;
    [SerializeField] private TileBase playerWarpTileIdle = null!;
    [SerializeField] private TileBase enemyWarpTile = null!;
    [SerializeField] private GameObject warpChangeEffect = null!;
    [SerializeField] private Transform gridParent = null!;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab = null!;
    [SerializeField] private GameObject patolEnemyPrefab = null!;
    [SerializeField] private GameObject chaserEnemyPrefab = null!;

    [Header("Settings")]
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private Vector2 tilemapOffset = new Vector2(-96f, -64f);


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
    private TurnManager turnManager = null!;
    private CommandManager commandManager = null!;
    private int playerCurrentHP;
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
        turnManager = GetComponent<TurnManager>();
        commandManager = GetComponent<CommandManager>();
        // debug
        currentStageSO = stageSOs[0];
        //initialStageSO = stageSOs[0];
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
            //ContinueGame();
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
            Debug.Log("aaa");
        }

        tilemap.ClearAllTiles();
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
        gridParent.transform.position = Vector3.zero;
        LoadStage(initialStageSO, true, currentStageSO.PlayerStartPosition);

        if(playerScript != null)
        {
            playerScript.WarpToNewStage(currentStageSO.PlayerStartPosition, currentStageSO.PlayerStartDirection);
        }
    }

    public async UniTask SwitchStageAsync(StageSO nextstage, Direction entryDirection)
    {
        //isTransitioning = true;
        GenerateTransitionTilemap(nextstage, entryDirection);

        var dirVector = Utility.GetVectorFromDirection(entryDirection);
        // スライド演出用のオフセット
        Vector3 transitionOffset = (Vector3)(dirVector * tilemapOffset * 2);

        // Grid全体を動かす準備
        gridParent.DOMove(transitionOffset, TransitionDuration).SetEase(Ease.OutQuad);

        List<GameObject> tempEnemyObjects = CreateTemporaryEnemies(nextstage, transitionOffset);

        // 演出待ち
        await UniTask.Delay(TransitionDelayMs);

        // 後片付け
        foreach (var enemy in tempEnemyObjects)
        {
            if(enemy != null) Destroy(enemy);
        }

        gridParent.position = Vector3.zero;
        //isTransitioning = false;
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
        // 4. プレイヤーのアクション完了状態をリセット
        //turnManager.OnPlayerActionComplete().Forget();
    }

    private List<GameObject> CreateTemporaryEnemies(StageSO nextstage, Vector3 transitionOffset)
    {
        List<GameObject> tempEnemyObjects = new List<GameObject>();

        if (stageEnemiesMap.ContainsKey(nextstage))
        {
            // ---------------------------------------------------------
            // パターンA: 既に訪れたことがあるステージ (現在の位置を使用)
            // ---------------------------------------------------------
            List<Enemy> existingEnemies = stageEnemiesMap[nextstage];

            foreach (var enemyScript in existingEnemies)
            {
                // 死んでいる敵を表示したくない場合はスキップ
                if (enemyScript.IsDead) continue;

                // 敵の現在位置を取得
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
            // ---------------------------------------------------------
            // パターンB: 初めてのステージ (初期配置を使用) - 既存ロジック
            // ---------------------------------------------------------
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
        //currentResetPos = nextstage.PlayerStartPosition;
        //currentResetDir = Utility.GetDirectionFromVector(Utility.GetVectorFromDirection(entryDirection));
        currentStageSO = nextstage;
        LoadStage(nextstage, false, newStartPos);
        //currentResetDir = playerScript.CurrentDirection;
        turnManager.OnPlayerActionComplete().Forget();
    }

    public void SwitchStageDirect(StageSO stage, Vector2Int startPos)
    {
        currentStageSO = stage;
        LoadStage(stage, false, startPos);
    }

    private void LoadStage(StageSO stage, bool isFirstInit, Vector2Int startPos)
    {
        tilemap.ClearAllTiles();
        // 敵の削除
        foreach (var enemy in enemyObjects)
        {
            if(enemy != null) enemy.gameObject.SetActive(false);
        }
        //enemyObjects.Clear();
        //enemyScripts.Clear();

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

        //GenerateEnemies(stage);
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
                // 死んでいる敵を表示するかどうかはゲームデザイン次第
                // ここでは「死体も残る」あるいは「死んだ敵は非表示のまま」などの制御が可能
                if (!enemy.IsDead)
                {
                    enemy.gameObject.SetActive(true);
                }
                else
                {
                    // 死んでいてもオブジェクトは存在させるが、見えなくしておくなど
                    // Undoで蘇生するためにGameObject自体はActiveにしておき、Spriteだけ消す等の工夫が必要かも
                    // 簡単のため一旦SetActive(true)して、Enemy側でDeadなら描画オフにするのが安全
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

        // 生成順序などでソートが必要ならここで
        enemyScripts.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private void GenerateTilemap(StageSO stage)
    {
        tilemap.ClearAllTiles();
        cellData = new CellType[width, height];
        FillTilemap(stage, tilemap, true);
    }

    private void GenerateTransitionTilemap(StageSO stage, Direction entryDirection)
    {
        transitionTilemap.ClearAllTiles();
        Vector2Int dir = Utility.GetVectorFromDirection(entryDirection);
        transitionTilemap.transform.position = -new Vector3(dir.x * tilemapOffset.x * 2, dir.y * tilemapOffset.y * 2, 0);

        FillTilemap(stage, transitionTilemap, false);

        // transitionTilemapにもWarpタイルを設定（次のステージの敵状態を見て判定）
        UpdateWarpTilesForTransition(stage, transitionTilemap);
    }

    private void FillTilemap(StageSO stage, Tilemap targetTilemap, bool updateCellData)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pos = new Vector3Int(x, y, 0) + new Vector3Int((int)tilemapOffset.x, (int)tilemapOffset.y, 0) / (int)cellSize;
                var type = stage.GetWallAt(x, y);

                if (updateCellData)
                {
                    cellData[x, y] = type;
                }

                switch (type)
                {
                    case CellType.Wall:
                        targetTilemap.SetTile(pos, wallTile);
                        break;
                    case CellType.Floor:
                        targetTilemap.SetTile(pos, floorTile);
                        break;
                    case CellType.Warp:
                        // Warpタイルは後で動的に設定するのでここではFloorを配置
                        targetTilemap.SetTile(pos, floorTile);
                        break;
                    default:
                        targetTilemap.SetTile(pos, null);
                        break;
                }
            }
        }

        // Warpタイルを敵の状態に応じて更新
        UpdateWarpTiles();
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

        // Warpセルを探して適切なタイルを設定
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (cellData[x, y] == CellType.Warp)
                {
                    var pos = new Vector3Int(x, y, 0) + new Vector3Int((int)tilemapOffset.x, (int)tilemapOffset.y, 0) / (int)cellSize;
                    TileBase tileToSet;
                    if(hasAliveEnemy) tileToSet = enemyWarpTile;
                    else tileToSet = playerWarpTile;
                    tilemap.SetTile(pos, tileToSet);
                    Vector3 worldPos = GridToWorldPosition(new Vector2Int(x, y));

                    if (!isTransitioning && stateChanged && !hasAliveEnemy)
                    {
                        Instantiate(warpChangeEffect, worldPos, Quaternion.identity);
                        AudioManager.Instance.PlaySE(AudioManager.SEType.enabledWarp);
                    }
                }
            }
        }

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
        // "ここは現在地(true)で、クリア状態は isCleared です"
        GameProgressManager.Instance.UpdateStageStatus(coords, true, isCleared);
    }

    // TurnManagerから呼ばれるpublicメソッド
    public void UpdateWarpTilesPublic()
    {
        UpdateWarpTiles();
    }

    // 次のステージ（transitionTilemap用）のWarpタイルを更新
    private void UpdateWarpTilesForTransition(StageSO nextStage, Tilemap targetTilemap)
    {
        // 次のステージに敵がいるかチェック（stageEnemiesMapを使用）
        bool hasAliveEnemy = false;
        if (stageEnemiesMap.TryGetValue(nextStage, out var nextStageEnemies))
        {
            foreach (var enemy in nextStageEnemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    hasAliveEnemy = true;
                    break;
                }
            }
        }
        else
        {
            // まだ訪れていないステージの場合は、初期データから判定
            hasAliveEnemy = nextStage.EnemyStartData.Count > 0;
        }

        // Warpセルを探して適切なタイルを設定
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (nextStage.GetWallAt(x, y) == CellType.Warp)
                {
                    var pos = new Vector3Int(x, y, 0) + new Vector3Int((int)tilemapOffset.x, (int)tilemapOffset.y, 0) / (int)cellSize;
                    TileBase tileToSet = hasAliveEnemy ? enemyWarpTile : playerWarpTile;
                    targetTilemap.SetTile(pos, tileToSet);
                }
            }
        }
    }

    private void GeneratePlayer(StageSO stage, Vector2Int pos, Direction dir)
    {
        Vector3 worldPos = GridToWorldPosition(pos);
        playerObject = Instantiate(playerPrefab, worldPos, Quaternion.identity, gridParent);
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
        GameObject enemy = Instantiate(prefab, worldPos, Quaternion.identity, gridParent);
        var enemyScript = enemy.GetComponent<Enemy>();
        enemyScript.Initialize(this, turnManager, commandManager, gridPos, dir, maxHp, order);
        return enemyScript;
    }

    private Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection)
    {
        // 例えば「右(Right)」へ抜けたなら、次のステージの「左端(x=0)」の同じy座標に出る

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
        // Load stage at specified startPos (use direct switch to honor provided position)
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

    // グリッド座標をワールド座標に変換
    public Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        // Tilemapの中心に合わせる
        return tilemap.CellToWorld(new Vector3Int(gridPos.x, gridPos.y, 0))
               + new Vector3(cellSize / 2f, cellSize / 2f, 0f) + new Vector3(tilemapOffset.x, tilemapOffset.y, 0);
    }

    // ワールド座標をグリッド座標に変換
    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        Vector3Int cellPos = tilemap.WorldToCell(worldPos);
        return new Vector2Int(cellPos.x, cellPos.y);
    }
    #endregion

    #region Save
    public void SaveGame()
    {
        if (playerScript == null) return;
        // ★修正: 敵リスト(enemyScripts)も渡す
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

            // LoadStageを呼ぶ (isFirstInit=falseにして、指定した座標を使わせる)
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

    // LoadStageのオーバーロード、または既存LoadStageの改修でも良いですが、
    // わかりやすく「指定座標で開始する専用メソッド」を作ります
    private void LoadStageWithCustomStart(StageSO stage, Vector2Int startPos, Direction startDir, int playerHP)
    {
        tilemap.ClearAllTiles();

        // 敵の削除など
        foreach (var enemy in enemyObjects) if(enemy != null) enemy.gameObject.SetActive(false);

        width = stage.Width;
        height = stage.Height;

        // ★重要: 強制的に指定座標に配置
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

        //turnManager.OnPlayerActionComplete().Forget();
    }

    private void ApplySavedEnemyData(List<EnemySaveData> savedList)
    {
        // ステージデータ(SO)の敵リスト順序と、生成されたenemyScriptsの順序は一致している前提
        // もし数が合わない場合は、セーブデータが古い可能性があるので中断
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
                // 死んでいる場合は強制的に倒す（エフェクトなしで即死させる処理が必要）
                // Enemyクラスに「ForceKill()」や「SetDead(true)」のようなメソッドを作るのがベスト
                // ここでは簡易的にSetActiveを切るか、HPを0にする
                enemy.ForceKillSilent(); // ※Enemy側に実装が必要
            }
            else
            {
                enemy.ReviveSilent();
                // 2. 生きている場合は位置と向きを復元
                enemy.WarpTo(new Vector2Int(data.gridX, data.gridY), (Direction)data.direction);
                // HPの復元など
                enemy.SetHP(data.currentHP);
                //enemy.SetDirection((Direction)data.direction);
            }
        }
    }
    #endregion
}