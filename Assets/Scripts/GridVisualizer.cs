using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class GridVisualizer : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap tilemap = null!;
    [SerializeField] private Tilemap transitionTilemap = null!;
    [SerializeField] private TileBase wallTile = null!;
    [SerializeField] private TileBase floorTile = null!;
    [SerializeField] private TileBase stairUpTile = null!;    // 上階段タイル
    [SerializeField] private TileBase stairDownTile = null!;  // 下階段タイル
    [SerializeField] private TileBase brokenStairUpTile = null!; // 壊れた上階段タイルのプレハブ/タイル
    [SerializeField] private GameObject tileSyncMapPrefab = null!;
    [SerializeField] private List<GameObject> tutorialPrefabs = new List<GameObject>();
    [SerializeField] private Transform gridParent = null!;

    [Header("Settings")]
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private Vector2 tilemapOffset = new Vector2(-96f, -64f);

    [Header("Wall Break Effect")]
    [SerializeField] private Shader dissolveShader = null;

    public float CellSize => cellSize;
    public Vector2 TilemapOffset => tilemapOffset;
    public Transform GridParent => gridParent;
    public Tilemap MainTilemap => tilemap;
    public Tilemap TransitionTilemap => transitionTilemap;

    private List<GameObject> spawnedSyncMapObjects = new List<GameObject>();
    private List<GameObject> spawnedTutorialObjects = new List<GameObject>();
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

    public void ClearAllTiles(bool keepTutorial = false)
    {
        tilemap.ClearAllTiles();
        CleanupObjects(spawnedSyncMapObjects);
        ClearSyncMapEntries();
        if (!keepTutorial)
        {
            CleanupObjects(spawnedTutorialObjects);
        }
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
    public void RefreshTilemap(int width, int height, CellType[,] cellData, StageSO stage, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck, bool keepTutorial = false)
    {
        ClearAllTiles(keepTutorial);
        FillTilemapFromData(tilemap, width, height, cellData, stage, destroyedWalls, isClearedCheck, keepTutorial);
    }

    // StageSOから直接描画する（初期化時など、まだcellDataが空の場合用だが、
    // 基本的にはGridManagerがデータを作ってから呼ぶ方が良い。
    // ここでは便宜上、GridManagerから移設したロジックを流用しつつ整理する。

    public void SetTile(Vector2Int pos, TileBase tile)
    {
        var tilePos = GetTilePosFromGridPos(pos.x, pos.y);
        tilemap.SetTile(tilePos, tile);
    }

    public void SetTileAt(Tilemap targetMap, int x, int y, CellType type, StageSO stage, Func<Vector2Int, bool> isClearedCheck)
    {
        var pos = GetTilePosFromGridPos(x, y);

        switch (type)
        {
            case CellType.Wall:
                targetMap.SetTile(pos, wallTile);
                break;
            case CellType.Floor:
            case CellType.Warp:
                targetMap.SetTile(pos, floorTile);
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
            case CellType.StairUp:
                // 上階段タイル（設定されていなければ床タイルで代用）
                targetMap.SetTile(pos, stairUpTile != null ? stairUpTile : floorTile);
                break;
            case CellType.StairDown:
                // 下階段タイル（設定されていなければ床タイルで代用）
                targetMap.SetTile(pos, stairDownTile != null ? stairDownTile : floorTile);
                break;
            case CellType.BrokenStairUp:
                // 壊れた上階段タイル（設定されていなければ通常の上階段、それもなければ床タイルで代用）
                targetMap.SetTile(pos, brokenStairUpTile != null ? brokenStairUpTile : (stairUpTile != null ? stairUpTile : floorTile));
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
                    spawnedTutorialObjects.Add(obj);
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
            spawnedSyncMapObjects.Add(obj);
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

    public void SetupTransitionTilemap(StageSO stage, Vector3 transitionOffset, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // Offset is the amount the PARENT will move.
        // Transition Map should be at -Offset relative to parent, so that when parent moves by Offset, Map is at (0,0).
        transitionTilemap.transform.position = -transitionOffset;

        // Transition用のTilemapを埋める
        // cellDataは使わずStageSOから直接読む（遷移先なので）
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls (Fix: Allow boundaries to be broken)
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }

    private void FillTilemapFromData(Tilemap targetMap, int width, int height, CellType[,] cellData, StageSO stage, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck, bool skipTutorialSpawn = false)
    {
        for (int y = -1; y <= height; y++)
        {
            for (int x = -1; x <= width; x++)
            {
                // 初期描画時はWarpの生存判定をどうするか？
                // 呼び出し元で制御してもらうため、ここではFloor扱いにしておくか、あるいはnullにしておく
                // 既存ロジックでは FillTilemap で WarpはFloorにしていた
                CellType type;
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    type = cellData[x, y];
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }

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
                        targetMap.SetTile(pos, floorTile);
                        break;
                    case CellType.Tutorial:
                        targetMap.SetTile(pos, floorTile);
                        if (!skipTutorialSpawn && tutorialPrefabs != null && stage != null)
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
                    case CellType.StairUp:
                        // 上階段タイル（設定されていなければ床タイルで代用）
                        targetMap.SetTile(pos, stairUpTile != null ? stairUpTile : floorTile);
                        break;
                    case CellType.StairDown:
                        // 下階段タイル（設定されていなければ床タイルで代用）
                        targetMap.SetTile(pos, stairDownTile != null ? stairDownTile : floorTile);
                        break;
                    case CellType.BrokenStairUp:
                        // 壊れた上階段タイル
                        targetMap.SetTile(pos, brokenStairUpTile != null ? brokenStairUpTile : (stairUpTile != null ? stairUpTile : floorTile));
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
        gridParent.DOKill();
        gridParent.position = Vector3.zero;
    }

    /// <summary>
    /// 壁破壊時にDissolveアニメーションを再生する
    /// </summary>
    public void PlayWallBreakEffect(Vector2Int gridPos)
    {
        if (dissolveShader == null) return;

        // wallTileからスプライトを取得（最優先）
        Sprite wallSprite = null;
        if (wallTile is UnityEngine.Tilemaps.Tile tile)
        {
            wallSprite = tile.sprite;
        }

        // fallback: Tilemapからタイル位置のスプライトを取得
        if (wallSprite == null)
        {
            var tilePos = GetTilePosFromGridPos(gridPos.x, gridPos.y);
            wallSprite = tilemap.GetSprite(tilePos);
        }

        if (wallSprite == null) return;

        PlayWallBreakEffectAsync(gridPos, wallSprite).Forget();
    }

    private async UniTaskVoid PlayWallBreakEffectAsync(Vector2Int gridPos, Sprite wallSprite)
    {
        // 一時GOを生成
        GameObject go = new GameObject("WallBreakEffect");
        go.transform.SetParent(gridParent, false);
        go.transform.position = GridToWorldPosition(gridPos);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = wallSprite;
        sr.sortingLayerName = tilemap.GetComponent<TilemapRenderer>()?.sortingLayerName ?? "Default";
        sr.sortingOrder = (tilemap.GetComponent<TilemapRenderer>()?.sortingOrder ?? 0) + 1;

        // DitherDissolveマテリアルを動的生成
        Material mat = new Material(dissolveShader);
        mat.SetTexture("_MainTex", wallSprite.texture);
        // 命中直後から「すでに崩れ始めている」感を出すため先出し
        mat.SetFloat("_DissolveAmount", 0.0f);
        mat.SetFloat("_SweepDirection", 2f); // 右上から左下
        sr.material = mat;

        // カメラシェイク（壁破壊の衝撃）
        CameraManager.Instance.ShakeCamera(0.25f, 4.0f, 30f, Vector2.up);

        // Dissolveアニメーション：OutExpoで最初にガッと崩れ素早く消える
        float duration = 0.5f;
        await DOTween.To(
            () => mat.GetFloat("_DissolveAmount"),
            x => mat.SetFloat("_DissolveAmount", x),
            1f,
            duration)
            .SetEase(Ease.OutCubic)
            .AsyncWaitForCompletion();

        // クリーンアップ
        if (mat != null) UnityEngine.Object.Destroy(mat);
        if (go != null) UnityEngine.Object.Destroy(go);
    }

    /// <summary>
    /// 階段用の遷移タイルマップを設定（垂直方向）
    /// </summary>
    /// <param name="stage">移動先ステージ</param>
    /// <param name="goingDown">trueなら下の階へ（下から上へスライド）、falseなら上の階へ（上から下へスライド）</param>
    /// <param name="currentPlayerPos">現在のプレイヤー位置（階段の位置）</param>
    /// <param name="targetPlayerPos">移動先のプレイヤー位置</param>
    public void SetupStairTransitionTilemap(StageSO stage, bool goingDown, Vector2Int currentPlayerPos, Vector2Int targetPlayerPos, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // 座標の差分を計算（現在位置と遷移先位置を合わせるためのオフセット）
        // 現在(5,3)にいて、遷移先で(2,1)に出現するなら、次のステージを (5-2, 3-1) = (3, 2) だけずらす
        Vector2Int posDiff = currentPlayerPos - targetPlayerPos;
        float xPosOffset = posDiff.x * cellSize;
        float yPosOffset = posDiff.y * cellSize;

        // 下の階へ行く場合：新しいステージは画面上から入ってくる（現在ステージは下へ移動）
        // 上の階へ行く場合：新しいステージは画面下から入ってくる（現在ステージは上へ移動）
        float yTransitionOffset = goingDown ? tilemapOffset.y * 2 : -tilemapOffset.y * 2;
        transitionTilemap.transform.position = new Vector3(xPosOffset, yTransitionOffset + yPosOffset, 0);

        // Transition用のTilemapを埋める
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }

    /// <summary>
    /// 階段遷移のアニメーション用移動量を取得
    /// </summary>
    public Vector3 GetStairTransitionOffset(bool goingDown)
    {
        // 下の階へ行く場合：gridParentを下に移動（現在のステージが下へ消える、新しいステージが上から入る）
        // 上の階へ行く場合：gridParentを上に移動（現在のステージが上へ消える、新しいステージが下から入る）
        float yOffset = goingDown ? -tilemapOffset.y * 2 : tilemapOffset.y * 2;
        return new Vector3(0, yOffset, 0);
    }

    /// <summary>
    /// 階段用の遷移タイルマップを設定（ワープ方向を使用して斜め対応）
    /// </summary>
    /// <summary>
    /// 階段用の遷移タイルマップを設定（ワープ方向を使用して斜め対応）
    /// </summary>
    public void SetupStairTransitionTilemapWithDirection(StageSO stage, Vector3 initialOffset, Vector2Int currentPlayerPos, Vector2Int targetPlayerPos, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // 座標の差分を計算（現在位置と遷移先位置を合わせるためのオフセット）
        Vector2Int posDiff = currentPlayerPos - targetPlayerPos;
        float xPosOffset = posDiff.x * cellSize;
        float yPosOffset = posDiff.y * cellSize;

        // プレイヤー位置のオフセットを加算
        transitionTilemap.transform.position = new Vector3(initialOffset.x + xPosOffset, initialOffset.y + yPosOffset, 0);

        // Transition用のTilemapを埋める
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }

    /// <summary>
    /// ワープ遷移用のタイルマップを設定
    /// </summary>
    /// <summary>
    /// ワープ遷移用のタイルマップを設定
    /// </summary>
    public void SetupWarpTransitionTilemap(StageSO stage, Vector3 initialOffset, Vector2Int targetPlayerPos, HashSet<Vector2Int> destroyedWalls, Func<Vector2Int, bool> isClearedCheck)
    {
        ClearTransitionTiles();

        // 遷移タイルマップのオフセットを設定
        transitionTilemap.transform.position = initialOffset;

        // Transition用のTilemapを埋める
        int w = stage.Width;
        int h = stage.Height;

        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                CellType type;
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    type = stage.GetWallAt(x, y);
                    // 破壊された壁は床として扱う
                    if (type == CellType.Wall && destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                }
                else
                {
                    // OOB Check for Destroyed Walls
                    if (destroyedWalls != null && destroyedWalls.Contains(new Vector2Int(x, y)))
                    {
                        type = CellType.Floor;
                    }
                    else
                    {
                        type = CellType.Wall;
                    }
                }
                SetTileAt(transitionTilemap, x, y, type, stage, isClearedCheck);
            }
        }
    }
}
