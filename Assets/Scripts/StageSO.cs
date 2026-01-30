using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Stage", menuName = "Grid Puzzle/Stage")]
public class StageSO : ScriptableObject
{
    [Header("Stage Settings")]
    [SerializeField] private string stageName = "New Stage";
    [SerializeField] private int width = 16;
    [SerializeField] private int height = 9;
    [SerializeField] private int floorIndex = 0;  // フロアインデックス（0が最上階、大きいほど下）

    [Header("Grid Layout")]
    [SerializeField] private CellType[] cellData;
    [SerializeField] private Vector2Int syncMapOffset = Vector2Int.zero;
    public Vector2Int SyncMapOffset => syncMapOffset;

    [SerializeField] private Vector2Int playerStartPosition = Vector2Int.zero;
    [SerializeField] private Direction playerStartDirection = Direction.Right;
    [SerializeField] private List<EnemyStartData> enemyStartData = new List<EnemyStartData>();
    [SerializeField] private List<TutorialIndexData> tutorialIndexData = new List<TutorialIndexData>();

    [Header("Adjacent Stages (平面マップ用)")]
    [SerializeField] private StageSO upStage;
    [SerializeField] private StageSO downStage;
    [SerializeField] private StageSO rightStage;
    [SerializeField] private StageSO leftStage;

    [Header("Stair Destinations (階層マップ用)")]
    [SerializeField] private StageSO defaultUpStage;    // 上り階段で移動するステージ
    [SerializeField] private StageSO defaultDownStage;  // 下り階段で移動するステージ

    public StageSO DefaultUpStage => defaultUpStage;
    public StageSO DefaultDownStage => defaultDownStage;

    [Header("Stair Connections (位置ごとの階段接続)")]
    [SerializeField] private List<StairConnectionData> stairConnections = new List<StairConnectionData>();
    public List<StairConnectionData> StairConnections => stairConnections;

    public StageSO NorthStage => upStage;
    public StageSO SouthStage => downStage;
    public StageSO EastStage => rightStage;
    public StageSO WestStage => leftStage;
    // Setters for adjacency so WorldMapSO can configure neighbors
    public void SetAdjacentStage(Direction direction, StageSO stage)
    {
        switch (direction)
        {
            case Direction.Up:
                upStage = stage;
                break;
            case Direction.Down:
                downStage = stage;
                break;
            case Direction.Right:
                rightStage = stage;
                break;
            case Direction.Left:
                leftStage = stage;
                break;
        }
    }

    public void SetAdjacentStages(StageSO up, StageSO down, StageSO left, StageSO right)
    {
        upStage = up;
        downStage = down;
        leftStage = left;
        rightStage = right;
    }
    public StageSO GetAdjacentStage(Direction direction)
    {
        return direction switch
        {
            Direction.Up => upStage,
            Direction.Down => downStage,
            Direction.Right => rightStage,
            Direction.Left => leftStage,
            _ => null
        };
    }

    // Properties
    public string StageName => stageName;
    public int Width => width;
    public int Height => height;
    public int FloorIndex => floorIndex;  // フロアインデックス（0が最上階）
    public Vector2Int PlayerStartPosition => playerStartPosition;
    public int PlayerMaxHP = 2;
    public int PlayerStartOrder = 0;
    public List<EnemyStartData> EnemyStartData => enemyStartData;
    public List<TutorialIndexData> TutorialData => tutorialIndexData;
    public Direction PlayerStartDirection => playerStartDirection;

    // wallDataの初期化と取得・設定
    public void InitializeWallData()
    {
        if (cellData == null || cellData.Length != width * height)
        {
            cellData = new CellType[width * height];
        }
    }

    public CellType GetWallAt(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return CellType.Wall;
        int index = y * width + x;
        if (cellData == null || index >= cellData.Length) return CellType.Wall;
        return cellData[index];
    }

    public void SetWallAt(int x, int y, bool isWall)
    {
        SetCellTypeAt(x, y, isWall ? CellType.Wall : CellType.Floor);
    }

    public void SetCellTypeAt(int x, int y, CellType type)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        int index = y * width + x;
        if (cellData == null) InitializeWallData();
        if (cellData != null && index < cellData.Length)
        {
            cellData[index] = type;
        }
    }

    public void SetPlayerPosition(Vector2Int position)
    {
        playerStartPosition = position;

        // プレイヤーの初期位置をWarpタイルに設定
        if (cellData != null)
        {
            int index = position.y * width + position.x;
            if (index >= 0 && index < cellData.Length)
            {
                cellData[index] = CellType.Warp;
            }
        }
    }

    public void SetPlayerDirection(Direction direction)
    {
        playerStartDirection = direction;
    }

    public void AddEnemy(Vector2Int position, EnemyType type, Direction direction, int maxHP, int SetActionOrder, int ammo = -1)
    {
        enemyStartData.Add(new EnemyStartData
        {
            Position = position,
            Direction = direction,
            EnemyType = type,
            MaxHP = maxHP,
            SetActionOrder = SetActionOrder,
            Ammo = ammo
        });
    }

    public void RemoveEnemyAt(Vector2Int position)
    {
        enemyStartData.RemoveAll(e => e.Position == position);
    }

    public void ClearEnemies()
    {
        enemyStartData.Clear();
    }

    private void OnValidate()
    {
        // widthやheightが変更された時にwallDataのサイズを調整
        if (cellData == null || cellData.Length != width * height)
        {
            CellType[] newData = new CellType[width * height];
            if (cellData != null)
            {
                int copyLength = Mathf.Min(cellData.Length, newData.Length);
                System.Array.Copy(cellData, newData, copyLength);
            }
            cellData = newData;
        }

        // プレイヤーの初期位置をWarpタイルに設定
        if (cellData != null && playerStartPosition.x >= 0 && playerStartPosition.x < width &&
            playerStartPosition.y >= 0 && playerStartPosition.y < height)
        {
            int index = playerStartPosition.y * width + playerStartPosition.x;
            if (index >= 0 && index < cellData.Length)
            {
                cellData[index] = CellType.Warp;
            }
        }

        // EnemyListのIndex順をSetActionOrderに反映
        UpdateEnemyActionIndices();
    }

    public void UpdateEnemyActionIndices()
    {
        for (int i = 0; i < enemyStartData.Count; i++)
        {
            var enemy = enemyStartData[i];
            enemy.SetActionOrder = i + 1;
            enemyStartData[i] = enemy;
        }
    }

    public void SetTutorialData(Vector2Int pos, int index)
    {
        tutorialIndexData.RemoveAll(d => d.Position == pos);
        tutorialIndexData.Add(new TutorialIndexData { Position = pos, Index = index });
    }

    public void RemoveTutorialData(Vector2Int pos)
    {
        tutorialIndexData.RemoveAll(d => d.Position == pos);
    }

    public int GetTutorialIndex(Vector2Int pos)
    {
        int idx = tutorialIndexData.FindIndex(d => d.Position == pos);
        if (idx != -1) return tutorialIndexData[idx].Index;
        return 0;
    }

    /// <summary>
    /// 特定の座標の階段に接続されたステージを取得する
    /// 設定がない場合はnullを返す
    /// </summary>
    public StageSO GetConnectedStage(Vector2Int pos)
    {
        int idx = stairConnections.FindIndex(d => d.Position == pos);
        if (idx != -1) return stairConnections[idx].TargetStage;
        return null;
    }

    /// <summary>
    /// 特定の座標の階段接続データを取得する
    /// </summary>
    public StairConnectionData? GetStairConnectionData(Vector2Int pos)
    {
        int idx = stairConnections.FindIndex(d => d.Position == pos);
        if (idx != -1) return stairConnections[idx];
        return null;
    }
}

[System.Serializable]
public struct EnemyStartData
{
    public Vector2Int Position;
    public Direction Direction;
    public EnemyType EnemyType;
    public int MaxHP;
    public int SetActionOrder;
    public int Ammo; // Added for Ranged Enemy Ammo config
}

[System.Serializable]
public struct TutorialIndexData
{
    public Vector2Int Position;
    public int Index;
}

[System.Serializable]
public struct StairConnectionData
{
    public Vector2Int Position;       // 階段の位置（現在のステージ）
    public StageSO TargetStage;       // 遷移先ステージ
    public Vector2Int TargetPosition; // 遷移先ステージでの階段位置（未設定の場合は同一座標を使用）
}