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

    // 新しいレイアウト: ハブ(上) + 3列×6行のダンジョン
    private const int DUNGEON_COLUMNS = 3;
    private const int DUNGEON_ROWS = 6;

    // タイル配列: [column, row] でrow=0がハブ、row=1-6がダンジョン
    private SpriteRenderer[,] mapTiles = null!;
    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;

    // セルのレイアウト位置を計算
    private Dictionary<Vector2Int, Vector3> cellPositions = new Dictionary<Vector2Int, Vector3>();

    public void Initialize()
    {
        visiblePosition = new Vector3(0, 0, mapPanel.localPosition.z);
        hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
        mapPanel.localPosition = hiddenPosition;

        GenerateMapLayout();
    }

    private void GenerateMapLayout()
    {
        cellPositions.Clear();

        // 既存のタイルを削除
        foreach (Transform child in mapPanel)
        {
            if (child != cursorTransform && child != currentPlayerIcon)
            {
                Destroy(child.gameObject);
            }
        }

        // タイル配列: 3列 × 7行 (row=0がハブ、row=1-6がダンジョン)
        mapTiles = new SpriteRenderer[DUNGEON_COLUMNS, DUNGEON_ROWS + 1];

        // ハブの位置（上中央）
        float hubY = DUNGEON_ROWS * tileSize.y * 0.5f;
        Vector3 hubPos = new Vector3(0, hubY, 0);

        // ハブタイル (x=1, y=0 として扱う - 中央列)
        var hubTile = Instantiate(mapTilePrefab, mapPanel);
        hubTile.transform.localPosition = hubPos;
        mapTiles[1, 0] = hubTile.GetComponent<SpriteRenderer>();
        cellPositions[new Vector2Int(1, 0)] = hubPos;

        // ダンジョンタイル（3列×6行）
        float startX = -tileSize.x; // 左端
        float startY = hubY - tileSize.y; // ハブの下から開始

        for (int col = 0; col < DUNGEON_COLUMNS; col++)
        {
            for (int row = 0; row < DUNGEON_ROWS; row++)
            {
                float x = startX + col * tileSize.x;
                float y = startY - row * tileSize.y;
                Vector3 pos = new Vector3(x, y, 0);

                var tileObj = Instantiate(mapTilePrefab, mapPanel);
                tileObj.transform.localPosition = pos;

                // row+1 でダンジョン行を表す（row=1がB1F, row=2がB2F...）
                mapTiles[col, row + 1] = tileObj.GetComponent<SpriteRenderer>();
                cellPositions[new Vector2Int(col, row + 1)] = pos;
            }
        }
    }

    // マップ全体の再描画
    public void RefreshMap(WorldMapCellType[,] mapData)
    {
        // ハブ (1, 0)
        if (mapTiles[1, 0] != null)
        {
            UpdateTileVisual(1, 0, mapData[1, 0]);
        }

        // ダンジョン
        for (int col = 0; col < DUNGEON_COLUMNS; col++)
        {
            for (int row = 1; row <= DUNGEON_ROWS; row++)
            {
                if (mapTiles[col, row] != null)
                {
                    UpdateTileVisual(col, row, mapData[col, row]);
                }
            }
        }
    }

    private void UpdateTileVisual(int col, int row, WorldMapCellType cellType)
    {
        if (mapTiles[col, row] == null) return;

        // ハブは特別なスプライトを使用
        if (row == 0)
        {
            mapTiles[col, row].sprite = hubTileSprite != null ? hubTileSprite : clearedTileSprite;
        }
        else
        {
            mapTiles[col, row].sprite = cellType switch
            {
                WorldMapCellType.Unknown => unknownTileSprite,
                WorldMapCellType.Cleared => clearedTileSprite,
                WorldMapCellType.Uncleared => unclearedTileSprite,
                WorldMapCellType.CurrentUnknown => unknownTileSprite,
                WorldMapCellType.CurrentCleared => clearedTileSprite,
                WorldMapCellType.CurrentUnCleared => unclearedTileSprite,
                _ => unknownTileSprite
            };
        }

        // 現在地アイコンの表示制御
        if (cellType == WorldMapCellType.CurrentUnknown ||
            cellType == WorldMapCellType.CurrentCleared ||
            cellType == WorldMapCellType.CurrentUnCleared)
        {
            currentPlayerIcon.localPosition = mapTiles[col, row].transform.localPosition;
        }
    }

    public void UpdateCursorPosition(Vector2Int pos)
    {
        if (cellPositions.TryGetValue(pos, out Vector3 localPos))
        {
            cursorTransform.localPosition = localPos;
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