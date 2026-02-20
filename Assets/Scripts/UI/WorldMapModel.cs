using UnityEngine;
using System.Collections.Generic;

// 新しいマップレイアウト用のモデル
// ハブ(上中央) + 3列×6行のダンジョン
public class WorldMapModel
{
    private WorldMapSO worldMapSO;

    // レイアウト定数
    public const int DUNGEON_COLUMNS = 9; // 3 layers * 3 columns
    public const int DUNGEON_ROWS = 6;
    public const int TOTAL_ROWS = DUNGEON_ROWS + 1; // ハブ含む

    // マップデータ: [col, row] で row=0がハブ、row=1-6がダンジョン
    private WorldMapCellType[,] mapData;

    // 各セルに対応するStageSO
    private StageSO[,] stageMap;

    // 解放済みセル
    private HashSet<Vector2Int> unlockedCells = new HashSet<Vector2Int>();

    public WorldMapModel(WorldMapSO worldMapSO)
    {
        this.worldMapSO = worldMapSO;
        InitializeMapData();
    }

    private void InitializeMapData()
    {
        mapData = new WorldMapCellType[DUNGEON_COLUMNS, TOTAL_ROWS];
        stageMap = new StageSO[DUNGEON_COLUMNS, TOTAL_ROWS];

        // 全てUnknownで初期化
        for (int col = 0; col < DUNGEON_COLUMNS; col++)
        {
            for (int row = 0; row < TOTAL_ROWS; row++)
            {
                mapData[col, row] = WorldMapCellType.Unknown;
            }
        }

        // ハブ（Back: 1,0 / Mid: 4,0 / Front: 7,0）は即時アクセス可能
        List<Vector2Int> hubs = new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(4, 0), new Vector2Int(7, 0) };
        foreach(var h in hubs)
        {
            mapData[h.x, h.y] = WorldMapCellType.Cleared;
            unlockedCells.Add(h);
        }

        // WorldMapSOからステージ情報を読み込む
        LoadStageMapping();
    }

    private void LoadStageMapping()
    {
        if (worldMapSO == null) return;

        // ハブステージ
        stageMap[1, 0] = worldMapSO.RootStage;

        // 各ステージのマッピングはWorldMapSOのallStagesを使用
        var allStages = worldMapSO.GetAllStages();
        foreach (var stage in allStages)
        {
            if (stage == null) continue;

            // StageSOのMapPositionを使用してマッピング
            Vector2Int mapPos = stage.MapPosition;
            if (IsValidPosition(mapPos))
            {
                stageMap[mapPos.x, mapPos.y] = stage;
            }
        }
    }

    public void ReloadFromProgressManager()
    {
        // GameProgressManagerからデータを読み込む
        var progressData = GameProgressManager.Instance.GetHierarchicalMapData();
        if (progressData != null)
        {
            unlockedCells = new HashSet<Vector2Int>(progressData.unlockedCells);

            // ハブは常に解放
            unlockedCells.Add(new Vector2Int(1, 0));

            UpdateMapDataFromUnlocked();
        }
    }

    private void UpdateMapDataFromUnlocked()
    {
        // クリア済みセルを取得
        var progressData = GameProgressManager.Instance.GetHierarchicalMapData();
        HashSet<Vector2Int> clearedCells = new HashSet<Vector2Int>();
        if (progressData != null && progressData.clearedCells != null)
        {
            foreach (var cell in progressData.clearedCells)
            {
                clearedCells.Add(cell);
            }
        }

        for (int col = 0; col < DUNGEON_COLUMNS; col++)
        {
            for (int row = 0; row < TOTAL_ROWS; row++)
            {
                Vector2Int pos = new Vector2Int(col, row);
                if (unlockedCells.Contains(pos))
                {
                    // クリア状態は階層マップのクリア状態から取得
                    bool isCleared = clearedCells.Contains(pos);
                    mapData[col, row] = isCleared ? WorldMapCellType.Cleared : WorldMapCellType.Uncleared;
                }
                else
                {
                    mapData[col, row] = WorldMapCellType.Unknown;
                }
            }
        }

        // 現在地のマーク
        Vector2Int currentPos = GameProgressManager.Instance.GetCurrentMapPosition();
        if (IsValidPosition(currentPos))
        {
            mapData[currentPos.x, currentPos.y] = ConvertToCurrentType(mapData[currentPos.x, currentPos.y]);
        }
    }

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

    public StageSO GetStageAt(Vector2Int pos)
    {
        if (!IsValidPosition(pos)) return null;
        return stageMap[pos.x, pos.y];
    }

    public Vector2Int GetCurrentPlayerPosition()
    {
        return GameProgressManager.Instance.GetCurrentMapPosition();
    }

    public WorldMapCellType GetCellType(Vector2Int pos)
    {
        if (!IsValidPosition(pos)) return WorldMapCellType.Unknown;

        // 最新データを取得
        ReloadFromProgressManager();
        return mapData[pos.x, pos.y];
    }

    public WorldMapCellType[,] GetAllMapData()
    {
        ReloadFromProgressManager();
        return mapData;
    }

    public bool IsValidPosition(Vector2Int pos)
    {
        // ハブ: row=0, cols = 1 (Back), 4 (Mid), 7 (Front)
        if (pos.y == 0)
        {
            return pos.x == 1 || pos.x == 4 || pos.x == 7;
        }
        // ダンジョン: col=0-8, row=1-6 is handled by < DUNGEON_COLUMNS
        return pos.x >= 0 && pos.x < DUNGEON_COLUMNS && pos.y >= 1 && pos.y <= DUNGEON_ROWS;
    }

    public bool IsTraversable(Vector2Int pos)
    {
        if (!IsValidPosition(pos)) return false;

        // ハブは常にアクセス可能
        if (IsHubPosition(pos)) return true;

        // 最新のデータを取得
        var progressData = GameProgressManager.Instance.GetHierarchicalMapData();
        if (progressData != null)
        {
            // 解放済みセルのみアクセス可能
            foreach (var cell in progressData.unlockedCells)
            {
                if (cell == pos) return true;
            }
        }

        return false;
    }

    public bool IsHubPosition(Vector2Int pos)
    {
        return pos.y == 0 && (pos.x == 1 || pos.x == 4 || pos.x == 7);
    }

    /// <summary>
    /// 階段で移動した際に対応するセルを解放する
    /// </summary>
    /// <param name="column">階段の列（0, 1, 2）</param>
    /// <param name="depth">降りた深さ（1-6）</param>
    public void UnlockCellByStair(int column, int depth)
    {
        if (column < 0 || column >= DUNGEON_COLUMNS) return;
        if (depth < 1 || depth > DUNGEON_ROWS) return;

        Vector2Int cellPos = new Vector2Int(column, depth);
        if (!unlockedCells.Contains(cellPos))
        {
            unlockedCells.Add(cellPos);
            mapData[column, depth] = WorldMapCellType.Uncleared;
            GameProgressManager.Instance.UnlockHierarchicalCell(cellPos);
        }
    }

    public void MarkCurrentAsCleared()
    {
        var pos = GetCurrentPlayerPosition();
        if (IsValidPosition(pos))
        {
            GameProgressManager.Instance.MarkPositionAsCleared(pos);
            mapData[pos.x, pos.y] = WorldMapCellType.CurrentCleared;
        }
    }
}