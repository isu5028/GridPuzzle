using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Stage", menuName = "Grid Puzzle/Stage")]
public class StageSO : ScriptableObject
{
    [Header("Stage Settings")]
    [SerializeField] private int width = 16;
    [SerializeField] private int height = 9;

    [Header("Hierarchical Map Position")]
    [Tooltip("階層マップでの位置 (x=列0-8, y=行0-8、0=ハブ)")]
    [SerializeField] private Vector2Int mapPosition = new Vector2Int(1, 0);
    public Vector2Int MapPosition => mapPosition;

    [Header("Grid Layout")]
    [SerializeField] private CellType[] cellData;
    [SerializeField] private Vector2Int syncMapOffset = Vector2Int.zero;
    public Vector2Int SyncMapOffset => syncMapOffset;

    [SerializeField] private Vector2Int playerStartPosition = Vector2Int.zero;
    [SerializeField] private Direction playerStartDirection = Direction.Right;
    [SerializeField] private List<EnemyStartData> enemyStartData = new List<EnemyStartData>();
    [SerializeField] private List<Vector2Int> crateStartPositions = new List<Vector2Int>();
    [SerializeField] private List<Vector2Int> shopItemPositions = new List<Vector2Int>();
    [SerializeField] private List<int> shopItemIds = new List<int>();
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
    public string StageName => this.name; // Use Asset Name
    public int Width => width;
    public int Height => height;

    private static readonly int[] ShopItemPrices = { 15, 20, 25, 30, 35 };
    public static int ShopItemTypeCount => ShopItemPrices.Length;

    // Derived from MapPosition
    public int FloorIndex => mapPosition.y;  // Y is Floor/Depth
    public int StairColumn => mapPosition.x; // X is Column

    public Vector2Int PlayerStartPosition => playerStartPosition;
    public int PlayerMaxHP = 2;
    public int PlayerStartOrder = 0;
    public List<EnemyStartData> EnemyStartData => enemyStartData;
    public List<TutorialIndexData> TutorialData => tutorialIndexData;
    public List<Vector2Int> CrateStartPositions => crateStartPositions;
    public List<Vector2Int> ShopItemPositions => shopItemPositions;
    public List<int> ShopItemIds => shopItemIds;
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

        // プレイヤーの初期位置をFloorタイルに設定
        if (cellData != null)
        {
            int index = position.y * width + position.x;
            if (index >= 0 && index < cellData.Length)
            {
                cellData[index] = CellType.Floor;
            }
        }
    }

    public void SetPlayerDirection(Direction direction)
    {
        playerStartDirection = direction;
    }

    public void AddEnemy(Vector2Int position, EnemyType type, Direction direction, int maxHP, int SetActionOrder, int ammo = -1, bool dropsCoin = false)
    {
        enemyStartData.Add(new EnemyStartData
        {
            Position = position,
            Direction = direction,
            EnemyType = type,
            MaxHP = maxHP,
            SetActionOrder = SetActionOrder,
            Ammo = ammo,
            DropsCoin = dropsCoin
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

    public void AddCrate(Vector2Int position)
    {
        if(!crateStartPositions.Contains(position))
        {
            crateStartPositions.Add(position);
        }
    }

    public void RemoveCrateAt(Vector2Int position)
    {
        crateStartPositions.Remove(position);
    }

    public void ClearCrates()
    {
        crateStartPositions.Clear();
    }

    public bool HasShopItemSlotAt(Vector2Int position)
    {
        return shopItemPositions.Contains(position);
    }

    public static int NormalizeShopItemId(int itemId)
    {
        return Mathf.Clamp(itemId, 0, ShopItemTypeCount - 1);
    }

    public static int GetShopItemPriceById(int itemId)
    {
        return ShopItemPrices[NormalizeShopItemId(itemId)];
    }

    public int GetShopItemIdAt(Vector2Int position)
    {
        int idx = shopItemPositions.IndexOf(position);
        if (idx < 0 || idx >= shopItemIds.Count) return -1;
        return NormalizeShopItemId(shopItemIds[idx]);
    }

    public int GetShopItemPriceAt(Vector2Int position)
    {
        int itemId = GetShopItemIdAt(position);
        return itemId < 0 ? -1 : GetShopItemPriceById(itemId);
    }

    public void AddShopItemSlot(Vector2Int position)
    {
        AddShopItemSlot(position, 0);
    }

    public void AddShopItemSlot(Vector2Int position, int itemId)
    {
        itemId = NormalizeShopItemId(itemId);

        int idx = shopItemPositions.IndexOf(position);
        if (idx != -1)
        {
            while (shopItemIds.Count <= idx)
            {
                shopItemIds.Add(0);
            }
            shopItemIds[idx] = itemId;
            return;
        }

        if (!shopItemPositions.Contains(position))
        {
            shopItemPositions.Add(position);
            shopItemIds.Add(itemId);
        }
    }

    public void RemoveShopItemSlotAt(Vector2Int position)
    {
        int idx = shopItemPositions.IndexOf(position);
        if (idx == -1) return;

        shopItemPositions.RemoveAt(idx);
        if (idx >= 0 && idx < shopItemIds.Count)
        {
            shopItemIds.RemoveAt(idx);
        }
    }

    public void ClearShopItemSlots()
    {
        shopItemPositions.Clear();
        shopItemIds.Clear();
    }

    private void EnsureShopItemDataConsistency()
    {
        if (shopItemPositions == null) shopItemPositions = new List<Vector2Int>();
        if (shopItemIds == null) shopItemIds = new List<int>();

        while (shopItemIds.Count < shopItemPositions.Count)
        {
            int fallbackId = Mathf.Clamp(shopItemIds.Count, 0, ShopItemTypeCount - 1);
            shopItemIds.Add(fallbackId);
        }

        if (shopItemIds.Count > shopItemPositions.Count)
        {
            shopItemIds.RemoveRange(shopItemPositions.Count, shopItemIds.Count - shopItemPositions.Count);
        }

        for (int i = 0; i < shopItemIds.Count; i++)
        {
            shopItemIds[i] = NormalizeShopItemId(shopItemIds[i]);
        }
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

        // プレイヤーの初期位置をFloorタイルに設定
        if (cellData != null && playerStartPosition.x >= 0 && playerStartPosition.x < width &&
            playerStartPosition.y >= 0 && playerStartPosition.y < height)
        {
            int index = playerStartPosition.y * width + playerStartPosition.x;
            if (index >= 0 && index < cellData.Length)
            {
                cellData[index] = CellType.Floor;
            }
        }

        // EnemyListのIndex順をSetActionOrderに反映
        UpdateEnemyActionIndices();

        // Shop Itemの位置リストとIDリストの整合性を維持
        EnsureShopItemDataConsistency();
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
    /// 設定がない場合はDefaultのUp/Downステージを使用する
    /// </summary>
    public StageSO GetConnectedStage(Vector2Int pos)
    {
        // 1. Explicit connection in list (overrides default)
        int idx = stairConnections.FindIndex(d => d.Position == pos);
        if (idx != -1 && stairConnections[idx].TargetStage != null)
        {
            return stairConnections[idx].TargetStage;
        }

        // 2. Default connections based on tile type
        CellType cell = GetWallAt(pos.x, pos.y);
        if (cell == CellType.StairUp)
        {
            return defaultUpStage;
        }
        else if (cell == CellType.StairDown)
        {
            return defaultDownStage;
        }

        return null; // Not a stair or no connection
    }

    /// <summary>
    /// 特定の座標の階段接続データを取得する
    /// 明示的な設定がない場合、デフォルト設定から仮想的なデータを作成して返す
    /// </summary>
    public StairConnectionData? GetStairConnectionData(Vector2Int pos)
    {
        // 1. Explicit
        int idx = stairConnections.FindIndex(d => d.Position == pos);
        if (idx != -1) return stairConnections[idx];

        // 2. Implicit (Default)
        StageSO target = GetConnectedStage(pos);
        if (target != null)
        {
            // Create a virtual connection data
            // Target position is assumed to be "the corresponding stair in the target stage"
            // However, StairTraversalSystem usually handles finding the matching stair.
            // Here we just return the target stage. TargetPosition can be -1,-1 to imply "auto-find".
            return new StairConnectionData
            {
                Position = pos,
                TargetStage = target,
                TargetPosition = new Vector2Int(-1, -1)
            };
        }

        return null;
    }

    /// <summary>
    /// ステージ内の上り階段の位置を取得する（見つからない場合はnull）
    /// </summary>
    public Vector2Int? GetStairUpPosition()
    {
        if (cellData == null) return null;
        for (int i = 0; i < cellData.Length; i++)
        {
            if (cellData[i] == CellType.StairUp || cellData[i] == CellType.BrokenStairUp)
            {
                return new Vector2Int(i % width, i / width);
            }
        }
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
    public bool DropsCoin; // 初回撃破時にコインを落とすかどうか
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