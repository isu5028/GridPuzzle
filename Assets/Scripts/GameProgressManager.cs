using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class GameProgressManager : MonoBehaviour
{
    private static GameProgressManager instance = null!;
    public static GameProgressManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameProgressManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("GameProgressManager");
                    instance = obj.AddComponent<GameProgressManager>();
                    DontDestroyOnLoad(obj);
                }
            }
            return instance;
        }
    }

    // ワールドマップの進行状態
    private WorldMapCellType[][] mapData = null!;
    private Vector2Int currentMapPosition;
    private HashSet<Vector2Int> clearedPositions = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
    private int mapLength = 8;
    private string SaveFilePath => Application.persistentDataPath + "/savefile.json";
    public Vector2Int SavedPlayerGridPos { get; private set; } = Vector2Int.zero;
    public Direction SavedPlayerDirection { get; private set; } = Direction.Right;

    // セーブデータが存在したかどうかのフラグ
    public bool HasLoadedData { get; private set; } = false;
    public List<EnemySaveData> SavedEnemyList { get; private set; } = new List<EnemySaveData>();
    public List<StageEnemyData> SavedAllStagesEnemies { get; private set; } = new List<StageEnemyData>();
    public Vector2Int SavedResetMapPos { get; private set; } = Vector2Int.zero;
    public int SavedPlayerHP { get; private set; } = 0;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMapData();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }

        // 起動時にロードして設定を反映
        LoadProgress();
    }

    public void InitEntireProgress()
    {
        SetCellType(0, 0, WorldMapCellType.CurrentUnCleared);
        visitedPositions.Add(new Vector2Int(0, 0));
        currentMapPosition = Vector2Int.zero;
    }

    public void ResetStageCleared()
    {
        clearedPositions.Clear();
        for (int x = 0; x < mapData.Length; x++)
        {
            for (int y = 0; y < mapData[x].Length; y++)
            {
                // Cleared -> Uncleared
                if (mapData[x][y] == WorldMapCellType.Cleared)
                {
                    mapData[x][y] = WorldMapCellType.Uncleared;
                }
                // CurrentCleared -> CurrentUnCleared
                else if (mapData[x][y] == WorldMapCellType.CurrentCleared)
                {
                    mapData[x][y] = WorldMapCellType.CurrentUnCleared;
                }
            }
        }
    }

    public void ResetEntireProgress()
    {
        clearedPositions.Clear();
        visitedPositions.Clear();
        InitializeMapData();
        currentMapPosition = Vector2Int.zero;
    }

    public void UpdateStageStatus(Vector2Int position, bool isCurrent, bool isCleared)
    {
        // 1. クリア時の処理
        if (isCleared)
        {
            // クリアしたら初めて「訪問済み」「クリア済み」として登録
            visitedPositions.Add(position);
            clearedPositions.Add(position);

            // ★重要: クリアした時だけ、隣接する Unknown を Uncleared に変える
            UnlockNeighbors(position);
        }
        else
        {
            if (clearedPositions.Contains(position))
            {
                clearedPositions.Remove(position);
            }
        }

        // 2. 現在地の更新（古い現在地の Current 表示を消す）
        if (isCurrent && currentMapPosition != position)
        {
            // 古い場所のセルタイプを取得
            WorldMapCellType oldType = GetCellType(currentMapPosition.x, currentMapPosition.y);
            // Current系から通常系に戻す（例: CurrentUnknown -> Unknown, CurrentCleared -> Cleared）
            WorldMapCellType fixedOldType = ConvertToNormalType(oldType);
            SetCellType(currentMapPosition.x, currentMapPosition.y, fixedOldType);

            currentMapPosition = position;
        }

        // 3. 指定位置（新しい現在地）のセルタイプを決定

        // まず、現在のマップ上の状態を取得（まだ書き換えていないので古い状態）
        WorldMapCellType currentCellType = GetCellType(position.x, position.y);

        // ベースとなる状態を決定
        WorldMapCellType baseType;

        if (isCleared)
        {
            // クリア済みなら問答無用で Cleared
            baseType = WorldMapCellType.Cleared;
        }
        else
        {
            // 未クリアの場合
            // 「Unknown」なら「Unknown」のまま、「Uncleared」なら「Uncleared」のままにしたい。
            // まず現在の状態から Current要素 を取り除く
            baseType = ConvertToNormalType(currentCellType);

            // ※補足: もしUndoでクリア済みから戻った場合、baseTypeは Cleared になっているので
            // Uncleared に戻す必要がある。
            if (baseType == WorldMapCellType.Cleared)
            {
                baseType = WorldMapCellType.Uncleared;
            }
        }

        // Currentフラグを付与して決定
        WorldMapCellType finalType = isCurrent ? ConvertToCurrentType(baseType) : baseType;

        SetCellType(position.x, position.y, finalType);
    }

    // Current系 -> 通常系 (Currentフラグを外す)
    private WorldMapCellType ConvertToNormalType(WorldMapCellType type)
    {
        return type switch
        {
            WorldMapCellType.CurrentUnknown => WorldMapCellType.Unknown,
            WorldMapCellType.CurrentUnCleared => WorldMapCellType.Uncleared,
            WorldMapCellType.CurrentCleared => WorldMapCellType.Cleared,
            _ => type // 元々Currentじゃなければそのまま
        };
    }

    // 通常系 -> Current系 (Currentフラグを付ける)
    private WorldMapCellType ConvertToCurrentType(WorldMapCellType type)
    {
        return type switch
        {
            WorldMapCellType.Unknown => WorldMapCellType.CurrentUnknown,
            WorldMapCellType.Uncleared => WorldMapCellType.CurrentUnCleared,
            WorldMapCellType.Cleared => WorldMapCellType.CurrentCleared,
            _ => type
        };
    }

    // ★追加: 隣接マスを開放するメソッド
    private void UnlockNeighbors(Vector2Int center)
    {
        // 上下左右の座標オフセット
        Vector2Int[] directions = new Vector2Int[]
        {
            Vector2Int.up,    // (0, 1)
            Vector2Int.down,  // (0, -1)
            Vector2Int.left,  // (-1, 0)
            Vector2Int.right  // (1, 0)
        };

        foreach (var dir in directions)
        {
            Vector2Int neighborPos = center + dir;

            // マップ範囲内かチェック
            if (neighborPos.x >= 0 && neighborPos.x < mapLength &&
                neighborPos.y >= 0 && neighborPos.y < mapLength)
            {
                // 現在の状態を取得
                WorldMapCellType currentType = GetCellType(neighborPos.x, neighborPos.y);

                // 「Unknown（未発見）」の場合のみ、「Uncleared（発見済み・未クリア）」に変更する
                // ※既にClearedな場所や、Currentな場所を上書きしないように注意
                if (currentType == WorldMapCellType.Unknown)
                {
                    SetCellType(neighborPos.x, neighborPos.y, WorldMapCellType.Uncleared);
                    visitedPositions.Add(neighborPos);
                }
            }
        }
    }

    private void InitializeMapData()
    {
        int mapLength = 8;
        mapData = new WorldMapCellType[mapLength][];
        for (int x = 0; x < mapLength; x++)
        {
            mapData[x] = new WorldMapCellType[mapLength];
            for (int y = 0; y < mapLength; y++)
            {
                mapData[x][y] = WorldMapCellType.Unknown;
            }
        }
    }

    public WorldMapCellType[][] GetMapData()
    {
        return mapData;
    }

    public void SetCellType(int x, int y, WorldMapCellType cellType)
    {
        if (x >= 0 && x < mapData.Length && y >= 0 && y < mapData[x].Length)
        {
            mapData[x][y] = cellType;
        }
    }

    public WorldMapCellType GetCellType(int x, int y)
    {
        if (x >= 0 && x < mapData.Length && y >= 0 && y < mapData[x].Length)
        {
            return mapData[x][y];
        }
        return WorldMapCellType.Unknown;
    }

    public void MarkPositionAsCleared(Vector2Int position)
    {
        clearedPositions.Add(position);
        visitedPositions.Add(position);
        SetCellType(position.x, position.y, WorldMapCellType.Cleared);
    }

    public void MarkPositionAsUnCleared(Vector2Int position)
    {
        visitedPositions.Add(position);
        if (!clearedPositions.Contains(position))
        {
            SetCellType(position.x, position.y, WorldMapCellType.Uncleared);
        }
    }

    public void MarkPositionAsCurrentCleared(Vector2Int position)
    {
        if (!visitedPositions.Contains(position))
        {
            SetCellType(position.x, position.y, WorldMapCellType.CurrentCleared);
        }
    }

    public void MarkPositionAsCurrentUncleared(Vector2Int position)
    {
        if (!visitedPositions.Contains(position))
        {
            SetCellType(position.x, position.y, WorldMapCellType.CurrentUnCleared);
        }
    }

    public bool IsPositionCleared(Vector2Int position)
    {
        return clearedPositions.Contains(position);
    }

    public bool IsPositionVisited(Vector2Int position)
    {
        return visitedPositions.Contains(position);
    }

    public void SetCurrentMapPosition(Vector2Int position)
    {
        currentMapPosition = position;
    }

    public Vector2Int GetCurrentMapPosition()
    {
        return currentMapPosition;
    }

    public void ResetProgress()
    {
        clearedPositions.Clear();
        visitedPositions.Clear();
        InitializeMapData();
        currentMapPosition = Vector2Int.zero;
    }

    #region save
    public void DeleteSaveFile()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log("Deleted Save File: " + SaveFilePath);
        }
    }

    public void SaveProgress(Vector2Int playerGridPos, Direction playerDir, List<Enemy> enemies, Vector2Int resetMapPos, int playerHP, List<StageEnemyData> allStagesEnemies)
    {
        SaveData data = new SaveData();

        // 1. ワールドマップ情報の保存
        data.currentMapX = currentMapPosition.x;
        data.currentMapY = currentMapPosition.y;

        // 2. ステージ内情報の保存
        data.playerGridX = playerGridPos.x;
        data.playerGridY = playerGridPos.y;
        data.playerDirection = (int)playerDir; // Enumをintにキャスト

        // 3. リストの変換
        foreach (var pos in clearedPositions) data.clearedList.Add(new Vector2IntData(pos));
        foreach (var pos in visitedPositions) data.visitedList.Add(new Vector2IntData(pos));

        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            var enemyData = new EnemySaveData
            {
                index = i,
                gridX = enemy.CurrentPos.x,
                gridY = enemy.CurrentPos.y,
                direction = (int)enemy.CurrentDirection,
                isDead = enemy.IsDead,
                currentHP = 1 // もしHPの概念があるなら enemy.CurrentHP
            };
            data.enemyList.Add(enemyData);
        }

        // 4. 全ステージの敵情報
        data.allStagesEnemies = new List<StageEnemyData>(allStagesEnemies);

        data.resetMapX = resetMapPos.x;
        data.resetMapY = resetMapPos.y;
        data.playerHP = playerHP;

        // GameSettingsの内容を保存
        data.screenMode = GameSettings.screenMode;
        data.resolutionWidth = GameSettings.Resolution.x;
        data.resolutionHeight = GameSettings.Resolution.y;
        data.gameSpeed = GameSettings.GameSpeed;
        data.language = GameSettings.Language;

        // 書き込み
        string json = JsonUtility.ToJson(data);
        string encryptedJson = EncryptionUtils.Encrypt(json);
        File.WriteAllText(SaveFilePath, encryptedJson);

        Debug.Log($"Saved: Map({data.currentMapX},{data.currentMapY}) Player({data.playerGridX},{data.playerGridY})");
    }

    public void LoadProgress()
    {
        if (!File.Exists(SaveFilePath))
        {
            ResetEntireProgress();

            // ファイルがない場合はデフォルト設定を適用
            // GameSettingsクラスの初期値を使用
            GameSettings.ApplySettings();

            InitEntireProgress();
            HasLoadedData = false;
            return;
        }

        try
        {
            string encryptedJson = File.ReadAllText(SaveFilePath);
            string json = EncryptionUtils.Decrypt(encryptedJson);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // データの復元
            ResetEntireProgress();
            currentMapPosition = new Vector2Int(data.currentMapX, data.currentMapY);

            // ★追加: ステージ内プレイヤー情報の復元
            SavedPlayerGridPos = new Vector2Int(data.playerGridX, data.playerGridY);
            SavedPlayerDirection = (Direction)data.playerDirection;
            SavedPlayerDirection = (Direction)data.playerDirection;
            SavedEnemyList = data.enemyList;
            SavedAllStagesEnemies = data.allStagesEnemies;
            SavedResetMapPos = new Vector2Int(data.resetMapX, data.resetMapY);
            SavedResetMapPos = new Vector2Int(data.resetMapX, data.resetMapY);
            SavedPlayerHP = data.playerHP;

            // GameSettingsの復元
            GameSettings.screenMode = data.screenMode;
            GameSettings.Resolution = new Vector2Int(data.resolutionWidth, data.resolutionHeight);
            GameSettings.GameSpeed = data.gameSpeed;
            GameSettings.Language = data.language;
            GameSettings.ApplySettings();

            HasLoadedData = true;
            Debug.Log($"Loaded: Map({data.currentMapX},{data.currentMapY}) Player({data.playerGridX},{data.playerGridY})");

            foreach (var item in data.clearedList) clearedPositions.Add(item.ToVector2Int());
            foreach (var item in data.visitedList) visitedPositions.Add(item.ToVector2Int());

            RebuildMapData();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Load Error: " + e.Message);
            ResetEntireProgress();
            HasLoadedData = false;
        }
    }

    // ロード後に mapData 配列を正しい状態に戻すメソッド
    private void RebuildMapData()
    {
        // 全マスを走査して状態を決定
        for (int x = 0; x < mapData.Length; x++)
        {
            for (int y = 0; y < mapData[x].Length; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                WorldMapCellType type;

                if (clearedPositions.Contains(pos))
                {
                    type = WorldMapCellType.Cleared;
                }
                else if (visitedPositions.Contains(pos))
                {
                    type = WorldMapCellType.Uncleared;
                }
                else
                {
                    type = WorldMapCellType.Unknown;
                }

                // 現在地ならCurrent系にする
                if (pos == currentMapPosition)
                {
                    type = ConvertToCurrentType(type);
                }

                SetCellType(x, y, type);
            }
        }
    }
    #endregion
}
