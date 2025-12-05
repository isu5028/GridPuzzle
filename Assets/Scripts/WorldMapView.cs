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
    [SerializeField] private Transform currentPlayerIcon = null!;

    [Header("Settings")]
    [SerializeField] private Vector2 tileSize = new Vector2(24, 16);
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private int mapLength = 8;

    private SpriteRenderer[,] mapTiles = null!;
    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private Vector2 cellOffset;
    private bool isAnimating = false;

    public void Initialize()
    {
        visiblePosition = new Vector3(0, 0, mapPanel.localPosition.z);
        hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
        mapPanel.localPosition = hiddenPosition;

        cellOffset = tileSize * (mapLength - 1) / 2;

        // タイルの生成
        mapTiles = new SpriteRenderer[mapLength, mapLength];
        for (int x = 0; x < mapLength; x++)
        {
            for (int y = 0; y < mapLength; y++)
            {
                var tileObj = Instantiate(mapTilePrefab, mapPanel);
                tileObj.transform.localPosition = GetLocalPosition(new Vector2Int(x, y));
                mapTiles[x, y] = tileObj.GetComponent<SpriteRenderer>();
            }
        }
    }

    // マップ全体の再描画
    public void RefreshMap(WorldMapCellType[][] mapData)
    {
        for (int x = 0; x < mapLength; x++)
        {
            for (int y = 0; y < mapLength; y++)
            {
                UpdateTileVisual(x, y, mapData[x][y]);
            }
        }
    }

    private void UpdateTileVisual(int x, int y, WorldMapCellType cellType)
    {
        if (mapTiles[x, y] == null) return;

        mapTiles[x, y].sprite = cellType switch
        {
            WorldMapCellType.Unknown => unknownTileSprite,
            WorldMapCellType.Cleared => clearedTileSprite,
            WorldMapCellType.Uncleared => unclearedTileSprite,
            WorldMapCellType.CurrentUnknown => unknownTileSprite,
            WorldMapCellType.CurrentCleared => clearedTileSprite,
            WorldMapCellType.CurrentUnCleared => unclearedTileSprite,
            _ => unknownTileSprite
        };

        // 現在地アイコンの表示制御
        if (cellType == WorldMapCellType.CurrentUnknown ||
            cellType == WorldMapCellType.CurrentCleared ||
            cellType == WorldMapCellType.CurrentUnCleared)
        {
            currentPlayerIcon.localPosition = mapTiles[x, y].transform.localPosition;
        }
    }

    public void UpdateCursorPosition(Vector2Int pos)
    {
        cursorTransform.localPosition = GetLocalPosition(pos);
    }

    private Vector3 GetLocalPosition(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x * tileSize.x, gridPos.y * tileSize.y, 0) - (Vector3)cellOffset;
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;
        isAnimating = true;
        mapPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay((int)(slideDuration * 1000));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;
        isAnimating = true;
        mapPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay((int)(slideDuration * 1000));
        isAnimating = false;
    }
}