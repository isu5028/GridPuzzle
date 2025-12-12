using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;

public class GridVisualizer : MonoBehaviour
{
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

    [Header("Settings")]
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private Vector2 tilemapOffset = new Vector2(-96f, -64f);

    public float CellSize => cellSize;
    public Vector2 TilemapOffset => tilemapOffset;
    public Transform GridParent => gridParent;
    public Tilemap MainTilemap => tilemap;
    public Tilemap TransitionTilemap => transitionTilemap;

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

    public void ClearAllTiles()
    {
        tilemap.ClearAllTiles();
    }

    public void ClearTransitionTiles()
    {
        transitionTilemap.ClearAllTiles();
    }

    // 論理データ(cellData)に基づいてTilemapを描画する
    public void RefreshTilemap(int width, int height, CellType[,] cellData)
    {
        tilemap.ClearAllTiles();
        FillTilemapFromData(tilemap, width, height, cellData);
        // Warpタイルは状態によって変わるので、呼び出し元で別途UpdateWarpTilesを呼ぶか、ここで行う
        // 基本的な床と壁を配置
    }

    // StageSOから直接描画する（初期化時など、まだcellDataが空の場合用だが、
    // 基本的にはGridManagerがデータを作ってから呼ぶ方が良い。
    // ここでは便宜上、GridManagerから移設したロジックを流用しつつ整理する。

    public void SetTile(Vector2Int pos, TileBase tile)
    {
        var tilePos = GetTilePosFromGridPos(pos.x, pos.y);
        tilemap.SetTile(tilePos, tile);
    }

    public void SetTileAt(Tilemap targetMap, int x, int y, CellType type, bool hasAliveEnemy)
    {
        var pos = GetTilePosFromGridPos(x, y);

        switch (type)
        {
            case CellType.Wall:
                targetMap.SetTile(pos, wallTile);
                break;
            case CellType.Floor:
                targetMap.SetTile(pos, floorTile);
                break;
            case CellType.Warp:
                // 初期状態としてのWarp設定
                TileBase tileToSet = hasAliveEnemy ? enemyWarpTile : playerWarpTile;
                targetMap.SetTile(pos, tileToSet);
                break;
            default:
                targetMap.SetTile(pos, null);
                break;
        }
    }

    private Vector3Int GetTilePosFromGridPos(int x, int y)
    {
        return new Vector3Int(x, y, 0) + new Vector3Int((int)tilemapOffset.x, (int)tilemapOffset.y, 0) / (int)cellSize;
    }

    public void UpdateWarpTiles(int width, int height, CellType[,] cellData, bool hasAliveEnemy, bool isTransitioning, bool stateChanged)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (cellData[x, y] == CellType.Warp)
                {
                    var pos = GetTilePosFromGridPos(x, y);
                    TileBase tileToSet = hasAliveEnemy ? enemyWarpTile : playerWarpTile;
                    tilemap.SetTile(pos, tileToSet);

                    if (!isTransitioning && stateChanged && !hasAliveEnemy)
                    {
                        Vector3 worldPos = GridToWorldPosition(new Vector2Int(x, y));
                        Instantiate(warpChangeEffect, worldPos, Quaternion.identity);
                        AudioManager.Instance.PlaySE(AudioManager.SEType.enabledWarp);
                    }
                }
            }
        }
    }

    public void SetupTransitionTilemap(StageSO stage, Direction entryDirection, bool nextStageHasEnemy)
    {
        transitionTilemap.ClearAllTiles();
        Vector2Int dir = Utility.GetVectorFromDirection(entryDirection);
        transitionTilemap.transform.position = -new Vector3(dir.x * tilemapOffset.x * 2, dir.y * tilemapOffset.y * 2, 0);

        // Transition用のTilemapを埋める
        // cellDataは使わずStageSOから直接読む（遷移先なので）
        int w = stage.Width;
        int h = stage.Height;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                CellType type = stage.GetWallAt(x, y);
                SetTileAt(transitionTilemap, x, y, type, nextStageHasEnemy);
            }
        }
    }

    private void FillTilemapFromData(Tilemap targetMap, int width, int height, CellType[,] cellData)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 初期描画時はWarpの生存判定をどうするか？
                // 呼び出し元で制御してもらうため、ここではFloor扱いにしておくか、あるいはnullにしておく
                // 既存ロジックでは FillTilemap で WarpはFloorにしていた
                CellType type = cellData[x, y];
                var pos = GetTilePosFromGridPos(x, y);

                switch (type)
                {
                    case CellType.Wall:
                        targetMap.SetTile(pos, wallTile);
                        break;
                    case CellType.Floor:
                        targetMap.SetTile(pos, floorTile);
                        break;
                    case CellType.Warp:
                        targetMap.SetTile(pos, floorTile); // 暫定Floor
                        break;
                    default:
                        targetMap.SetTile(pos, null);
                        break;
                }
            }
        }
    }

    public Tween MoveGridParent(Vector3 endValue, float duration)
    {
        return gridParent.DOMove(endValue, duration).SetEase(Ease.OutQuad);
    }

    public void ResetGridParentPosition()
    {
        gridParent.position = Vector3.zero;
    }
}
