using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class WorldMapView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform mapPanel = null!;
    [SerializeField] private Transform cursorTransform = null!;

    [Header("Assets")]
    [SerializeField] private GameObject mapTilePrefab = null!;
    [SerializeField] private Sprite unknownTileSprite = null!;
    [SerializeField] private Sprite clearedTileSprite = null!;
    [SerializeField] private Sprite unclearedTileSprite = null!;
    [SerializeField] private Sprite hubTileSprite = null!;
    [SerializeField] private Transform currentPlayerIcon = null!;

    [Header("Settings")]
    [SerializeField] private Vector2 tileSize = new Vector2(24, 16);
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private TextPopUpEach popUpText;

    // 新しいレイアウト: 3層構造 (Back/Mid/Front)
    private const int DUNGEON_COLUMNS = 9; // 3 layers * 3 columns
    private const int DUNGEON_ROWS = 6;
    private const int LAYERS = 3;

    // タイル配列: [column, row]
    private SpriteRenderer[,] mapTiles = null!;

    // Layer Containers
    private Transform[] layerContainers;
    private const float LAYER_BASE_X = 0f;
    private const float LAYER_OFFSET_X = 300f; // Distance between layers
    private const float LAYER_SCALE_STEP = 0.2f; // Scale difference

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;

    // セルのレイアウト位置を計算
    private Dictionary<Vector2Int, Vector3> cellPositions = new Dictionary<Vector2Int, Vector3>();

    public void Initialize()
    {
        visiblePosition = new Vector3(0, 0, 0);
        hiddenPosition = new Vector3(0, -Screen.height, 0);
        mapPanel.localPosition = hiddenPosition;

        GenerateMapLayout();
        UpdateViewPerspective(0); // Default to layer 0
    }

    private void GenerateMapLayout()
    {
        cellPositions.Clear();

        // Cleanup
        List<Transform> toDestroy = new List<Transform>();
        foreach (Transform child in mapPanel)
        {
            if (child != cursorTransform && child != currentPlayerIcon) toDestroy.Add(child);
        }
        foreach (var t in toDestroy) Destroy(t.gameObject);

        // Initialize Containers
        layerContainers = new Transform[LAYERS];
        for (int i = 0; i < LAYERS; i++)
        {
            GameObject container = new GameObject($"Layer_{i}_Container");
            container.transform.SetParent(mapPanel, false);
            layerContainers[i] = container.transform;
        }

        mapTiles = new SpriteRenderer[DUNGEON_COLUMNS, DUNGEON_ROWS + 1];

        // Layout within container (centered at 0,0)
        float startX = -tileSize.x; // Center of 3 columns
        float startY = (DUNGEON_ROWS * tileSize.y) * 0.4f;

        for (int layer = 0; layer < LAYERS; layer++)
        {
            int colOffset = layer * 3;
            Transform container = layerContainers[layer];

            // HUB (Row 0)
            int hubCol = colOffset + 1;
            Vector3 hubPos = new Vector3(0, startY + tileSize.y * 1.5f, 0);
            CreateTile(hubCol, 0, hubPos, true, container);

            // Dungeon Grid
            for (int r = 1; r <= DUNGEON_ROWS; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int col = colOffset + c;
                    float x = startX + (c * tileSize.x);
                    float y = startY - ((r - 1) * tileSize.y);
                    Vector3 pos = new Vector3(x, y, 0);
                    CreateTile(col, r, pos, false, container);
                }
            }
        }
    }

    private void CreateTile(int col, int row, Vector3 pos, bool isHub, Transform parent)
    {
        var tileObj = Instantiate(mapTilePrefab, parent);
        tileObj.transform.localPosition = pos;
        tileObj.name = $"Tile_{col}_{row}";

        var sr = tileObj.GetComponent<SpriteRenderer>();
        mapTiles[col, row] = sr;
        // Global position logic is tricky with containers moving.
        // We will update cursor strictly by parenting it to the active tile or calculating world pos.
        // For now, let's store local pos relative to container, but UpdateCursorPosition needs to handle containers.
        // Actually, easiest is: UpdateCursorPosition finds the tile and uses its position.
    }

    // Perspective Update
    public void UpdateViewPerspective(int activeLayer)
    {
        if (layerContainers == null) return;

        // Base width of a layer (3 columns * 24 + padding) ~ 100f
        float baseLayerWidth = 80f;
        float gap = 10f;
        float scaleStep = 0.2f;

        // 1. Calculate Scales
        float[] scales = new float[LAYERS];
        for (int i = 0; i < LAYERS; i++)
        {
            int diff = i - activeLayer;
            // Active=1.0. Right (>0) -> Larger. Left (<0) -> Smaller.
            float s = 1.0f + (diff * scaleStep);
            scales[i] = Mathf.Max(0.5f, s);
        }

        // 2. Calculate Positions (Center Active Layer at 0)
        // 2. Calculate Positions (Center Active Layer at 0)
        float[] positions = new float[LAYERS];
        positions[activeLayer] = 0f;

        // Spread to Right (Active+1 to End)
        for (int i = activeLayer + 1; i < LAYERS; i++)
        {
            float prevHalf = (baseLayerWidth * scales[i - 1]) * 0.5f;
            float currHalf = (baseLayerWidth * scales[i]) * 0.5f;
            positions[i] = positions[i - 1] + (prevHalf + currHalf + gap);
        }

        // Spread to Left (Active-1 to 0)
        for (int i = activeLayer - 1; i >= 0; i--)
        {
            float nextHalf = (baseLayerWidth * scales[i + 1]) * 0.5f;
            float currHalf = (baseLayerWidth * scales[i]) * 0.5f;
            positions[i] = positions[i + 1] - (nextHalf + currHalf + gap);
        }

        // 3. Apply Transforms
        float verticalStep = 30f; // How much up/down per layer step

        for (int i = 0; i < LAYERS; i++)
        {
            Transform container = layerContainers[i];

            // Y Position: Smaller (Left) -> Higher, Larger (Right) -> Lower
            int diff = i - activeLayer;
            float targetY = -diff * verticalStep;

            container.DOLocalMove(new Vector3(positions[i], targetY, 0), 0.4f).SetUpdate(true).SetEase(Ease.OutQuart);
            container.DOScale(scales[i], 0.4f).SetUpdate(true).SetEase(Ease.OutQuart);
        }
    }

    // ... UpdateCursorPosition needs update to find tile world/local pos correctly ...
    public void UpdateCursorPosition(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= DUNGEON_COLUMNS || pos.y > DUNGEON_ROWS) return;

        // Find the tile object
        var tile = mapTiles[pos.x, pos.y];
        if (tile != null)
        {
            // Parent cursor to the tile (or container) to follow its scale/move?
            // Or just copy world position.
            // If we parent to mapPanel, we need to calculate position influenced by container transform.
            // Easiest: Parent cursor to the Container of the layer.
            int layer = pos.x / 3;
            if (layer < layerContainers.Length)
            {
                cursorTransform.SetParent(layerContainers[layer]);
                cursorTransform.localPosition = tile.transform.localPosition;
                cursorTransform.localScale = Vector3.one; // Reset scale relative to container

                // Trigger Perspective Update
                UpdateViewPerspective(layer);
            }
        }
    }

    // マップ全体の再描画
    public void RefreshMap(WorldMapCellType[,] mapData)
    {
        for (int col = 0; col < DUNGEON_COLUMNS; col++)
        {
            for (int row = 0; row <= DUNGEON_ROWS; row++)
            {
                if (mapTiles[col, row] != null)
                {
                    // Check bounds for mapData input just in case
                    if (col < mapData.GetLength(0) && row < mapData.GetLength(1))
                    {
                        UpdateTileVisual(col, row, mapData[col, row]);
                    }
                }
            }
        }
    }

    private void UpdateTileVisual(int col, int row, WorldMapCellType cellType)
    {
        if (mapTiles[col, row] == null) return;

        bool isHub = (row == 0);

        Sprite targetSprite = unknownTileSprite;

        switch (cellType)
        {
            case WorldMapCellType.Unknown:
                targetSprite = isHub ? (hubTileSprite != null ? hubTileSprite : clearedTileSprite) : unknownTileSprite;
                break;
            case WorldMapCellType.Cleared:
            case WorldMapCellType.CurrentCleared:
                targetSprite = isHub ? (hubTileSprite != null ? hubTileSprite : clearedTileSprite) : clearedTileSprite;
                break;
            case WorldMapCellType.Uncleared:
            case WorldMapCellType.CurrentUnCleared:
                targetSprite = isHub ? (hubTileSprite != null ? hubTileSprite : unclearedTileSprite) : unclearedTileSprite;
                break;
            case WorldMapCellType.CurrentUnknown:
                targetSprite = unknownTileSprite;
                break;
        }

        mapTiles[col, row].sprite = targetSprite;

        // Highlight current position (could be an overlay or icon move)
        if (cellType == WorldMapCellType.CurrentUnknown ||
            cellType == WorldMapCellType.CurrentCleared ||
            cellType == WorldMapCellType.CurrentUnCleared)
        {
             if (currentPlayerIcon != null)
                 currentPlayerIcon.localPosition = mapTiles[col, row].transform.localPosition;
        }
    }

    public bool IsValidPosition(Vector2Int pos)
    {
        return cellPositions.ContainsKey(pos);
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;
        AudioManager.Instance.SetMasterHighPassFilter(400);
        popUpText.Hide();
        isAnimating = true;
        mapPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay((int)(slideDuration * 1000));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;
        AudioManager.Instance.SetMasterHighPassFilter(10);
        isAnimating = true;
        mapPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay((int)(slideDuration * 1000));
        isAnimating = false;
    }
}