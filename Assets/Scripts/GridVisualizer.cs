using System;
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
    [SerializeField] private GameObject tileSyncMapPrefab = null!;
    [SerializeField] private List<GameObject> tutorialPrefabs = new List<GameObject>();
    [SerializeField] private Transform gridParent = null!;

    [Header("Settings")]
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private Vector2 tilemapOffset = new Vector2(-96f, -64f);

    public float CellSize => cellSize;
    public Vector2 TilemapOffset => tilemapOffset;
    public Transform GridParent => gridParent;
    public Tilemap MainTilemap => tilemap;
    public Tilemap TransitionTilemap => transitionTilemap;

    private List<GameObject> spawnedMainObjects = new List<GameObject>();
    private List<GameObject> spawnedTransitionObjects = new List<GameObject>();

    private struct SyncMapEntry
    {
        public Vector2Int coords;
        public TileSyncMapController controller;
    }
    private List<SyncMapEntry> syncMapEntries = new List<SyncMapEntry>();

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
        CleanupObjects(spawnedMainObjects);
    }

    public void ClearTransitionTiles()
    {
        transitionTilemap.ClearAllTiles();
        CleanupObjects(spawnedTransitionObjects);
    }

    public void ClearSyncMapEntries()
    {
        syncMapEntries.Clear();
    }

    private void CleanupObjects(List<GameObject> list)
    {
        foreach (var obj in list)
        {
            if (obj != null) Destroy(obj);
        }
        list.Clear();
    }

    // 論理データ(cellData)に基づいてTilemapを描画する
    public void RefreshTilemap(int width, int height, CellType[,] cellData, StageSO stage, Func<Vector2Int, bool> isClearedCheck)
    {
        tilemap.ClearAllTiles();
        ClearSyncMapEntries();
        FillTilemapFromData(tilemap, width, height, cellData, stage, isClearedCheck);
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

    public void SetTileAt(Tilemap targetMap, int x, int y, CellType type, bool hasAliveEnemy, StageSO stage, Func<Vector2Int, bool> isClearedCheck)
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
            case CellType.Tutorial:
                targetMap.SetTile(pos, floorTile);
                if (tutorialPrefabs != null && stage != null)
                {
                    InstantiateTutorialPrefab(targetMap, pos, new Vector2Int(x, y), stage);
                }
                break;
            case CellType.Warp:
                // 初期状態としてのWarp設定
                TileBase tileToSet = hasAliveEnemy ? enemyWarpTile : playerWarpTile;
                targetMap.SetTile(pos, tileToSet);
                break;
            case CellType.SyncMap:
                targetMap.SetTile(pos, floorTile);
                if (tileSyncMapPrefab != null)
                {
                    InstantiateSyncMapPrefab(targetMap, pos, new Vector2Int(x, y), stage.SyncMapOffset, isClearedCheck);
                }
                break;
            default:
                targetMap.SetTile(pos, null);
                break;
        }
    }

    private void InstantiateTutorialPrefab(Tilemap targetMap, Vector3Int tilePos, Vector2Int gridPos, StageSO stage)
    {
        int index = stage.GetTutorialIndex(gridPos);
        // Index is 1-based (usually 1, 2, 3...)
        // List is 0-based.
        // Assuming tutorialPrefabs[0] matches index 1? Or user sets index 0 for 1st tutorial?
        // Editor default index was 1.
        // Let's adjust: Prefab lookup = index - 1?
        // Or just use index if user manages it.
        // "iはTutorialのIndex". "TutorialPrefab[i]"
        // If index=1, usually `tutorialPrefabs[1]` if array is large enough.
        // BUT safer is `index - 1` if Tutorial 1 means first prefab.
        // Let's assume 1-based index in data, 0-based in list. So use `index - 1`.

        int prefabIndex = index - 1;

        if (prefabIndex >= 0 && prefabIndex < tutorialPrefabs.Count)
        {
            GameObject prefab = tutorialPrefabs[prefabIndex];
            if (prefab != null)
            {
                // Align to tile center
                Vector3 worldPos = targetMap.GetCellCenterWorld(tilePos);
                GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, targetMap.transform);

                if (targetMap == tilemap)
                {
                    spawnedMainObjects.Add(obj);
                }
                else if (targetMap == transitionTilemap)
                {
                    spawnedTransitionObjects.Add(obj);
                }
            }
        }
    }

    private void InstantiateSyncMapPrefab(Tilemap targetMap, Vector3Int tilePos, Vector2Int gridPos, Vector2Int offset, Func<Vector2Int, bool> isClearedCheck)
    {
        // タイルの中心位置を取得
        Vector3 worldPos = targetMap.GetCellCenterWorld(tilePos);
        GameObject obj = Instantiate(tileSyncMapPrefab, worldPos, Quaternion.identity, targetMap.transform);

        if (obj != null)
        {
            var controller = obj.GetComponent<TileSyncMapController>();
            if (controller != null)
            {
                // Calculate World Map Coordinates
                Vector2Int worldMapCoords = gridPos + offset;
                bool isCleared = isClearedCheck != null ? isClearedCheck(worldMapCoords) : false;

                obj.name = $"TileSyncMap_({worldMapCoords.x},{worldMapCoords.y})";

                if (isCleared)
                {
                    controller.Show(true);
                }
                else
                {
                    controller.Hide();
                }

                if (targetMap == tilemap)
                {
                    syncMapEntries.Add(new SyncMapEntry { coords = worldMapCoords, controller = controller });
                }
            }
        }

        if (targetMap == tilemap)
        {
            spawnedMainObjects.Add(obj);
        }
        else if (targetMap == transitionTilemap)
        {
            spawnedTransitionObjects.Add(obj);
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

    private Tween activationTween;
    public event Action OnSyncMapActivated;

    public void UpdateSyncMapVisuals(Func<Vector2Int, bool> isClearedCheck, float delay = 0f, bool triggerEvent = false, bool allowActivation = true)
    {
        if (isClearedCheck == null) return;

        // Cancel any pending activation
        activationTween?.Kill();

        bool allCleared = true;
        // First pass: Check if all are cleared and update show/hide state
        foreach (var entry in syncMapEntries)
        {
            if (entry.controller != null)
            {
                if (isClearedCheck(entry.coords))
                {
                    entry.controller.Show();
                }
                else
                {
                    entry.controller.Hide();
                    allCleared = false;
                }
            }
        }

        // Second pass: If all are cleared, play activated animation
        if (allCleared && syncMapEntries.Count > 0)
        {
            if (allowActivation)
            {
                if (delay > 0f)
                {
                    activationTween = DOVirtual.DelayedCall(delay, () =>
                    {
                        foreach (var entry in syncMapEntries)
                        {
                            if (entry.controller != null)
                            {
                                entry.controller.PlayActivated();
                            }
                        }
                        if (triggerEvent) OnSyncMapActivated?.Invoke();
                    });
                }
                else
                {
                    foreach (var entry in syncMapEntries)
                    {
                        if (entry.controller != null)
                        {
                            entry.controller.PlayActivated();
                        }
                    }
                    if (triggerEvent) OnSyncMapActivated?.Invoke();
                }
            }
        }
        else
        {
             foreach (var entry in syncMapEntries)
            {
                if (entry.controller != null)
                {
                    entry.controller.ResetActivated();
                }
            }
        }
    }

    public void SetupTransitionTilemap(StageSO stage, Direction entryDirection, bool nextStageHasEnemy, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();
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
                // 破壊された壁は床として扱う
                if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                {
                    type = CellType.Floor;
                }
                SetTileAt(transitionTilemap, x, y, type, nextStageHasEnemy, stage, isClearedCheck);
            }
        }
    }

    private void FillTilemapFromData(Tilemap targetMap, int width, int height, CellType[,] cellData, StageSO stage, Func<Vector2Int, bool> isClearedCheck)
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
                    case CellType.Tutorial:
                        targetMap.SetTile(pos, floorTile);
                        if (tutorialPrefabs != null && stage != null)
                        {
                            InstantiateTutorialPrefab(targetMap, pos, new Vector2Int(x, y), stage);
                        }
                        break;
                    case CellType.SyncMap:
                        targetMap.SetTile(pos, floorTile);
                        if (tileSyncMapPrefab != null)
                        {
                            InstantiateSyncMapPrefab(targetMap, pos, new Vector2Int(x, y), stage.SyncMapOffset, isClearedCheck);
                        }
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
