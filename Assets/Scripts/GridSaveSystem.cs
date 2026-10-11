using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GridManagerから分離されたセーブ・ロードシステム
/// </summary>
public class GridSaveSystem : ISaveSystem
{
    private GridManager gm;

    public GridSaveSystem(GridManager gridManager)
    {
        this.gm = gridManager;
    }

    #region Save
    public void SaveGame(GameProgressManager.SaveType saveType)
    {
        if (gm.CurrentStageSOInternal == null) return;

        GameProgressManager.Instance.SaveProgress(
            Vector2Int.zero,
            Direction.Right,
            new List<Enemy>(),
            gm.CurrentStageSOInternal.MapPosition,
            0,
            new List<StageEnemyData>(),
            BuildPermanentWallSaveData(),
            gm.ShopSystem != null ? gm.ShopSystem.BuildShopPurchaseSaveData() : new List<StageShopData>(),
            saveType
        );
        if (gm.SaveFeedback != null)
        {
            gm.SaveFeedback.PlayFeedback();
        }
    }

    public void ContinueGame(GameProgressManager.SaveType saveType)
    {
        GameProgressManager.Instance.LoadProgress(saveType);

        if (!GameProgressManager.Instance.HasLoadedData)
        {
            gm.LoadStage(gm.CurrentStageSOInternal, true, gm.CurrentStageSOInternal.PlayerStartPosition);
            gm.SyncWorldMapStatus();
            return;
        }

        gm.ResetAllEnemies();
        gm.DestroyedWalls.Clear();
        gm.PermanentDestroyedWalls.Clear();
        gm.LoadedStageEnemyData.Clear();
        ApplySavedPermanentWalls(GameProgressManager.Instance.SavedAllStagesWalls);

        // ショップアイテム購入状況の復元
        if (gm.ShopSystem != null)
        {
            gm.ShopSystem.RestoreShopPurchasesFromSaveData(GameProgressManager.Instance.SavedAllStagesShopPurchases);
        }

        // Find current stage from save data
        var savedResetPos = GameProgressManager.Instance.GetCurrentMapPosition();
        StageSO targetStage = null;

        targetStage = FindStageByMapPos(savedResetPos.x, savedResetPos.y);
        if (gm.WorldMapSO != null)
        {
            targetStage ??= gm.WorldMapSO.GetStageByMapPosition(savedResetPos);
        }
        if (targetStage == null) targetStage = gm.CurrentStageSOInternal;
        if (targetStage == null && gm.StageSOs != null && gm.StageSOs.Length > 0)
        {
            targetStage = gm.StageSOs[0];
        }

        if (targetStage == null) return;

        gm.CurrentStageSOInternal = targetStage;
        gm.InitialStageSO = targetStage;
        gm.CurrentResetMapPos = savedResetPos;

        Vector2Int startPos = targetStage.PlayerStartPosition;
        Direction startDir = targetStage.PlayerStartDirection;

        if (targetStage.MapPosition.y > 0)
        {
            Vector2Int? stairUpPos = targetStage.GetStairUpPosition();
            if (stairUpPos.HasValue)
            {
                startPos = stairUpPos.Value;
            }
        }

        gm.LoadStage(gm.CurrentStageSOInternal, false, startPos, startDir);

        gm.SyncWorldMapStatus();
    }

    private StageSO FindStageByMapPos(int x, int y)
    {
        foreach (var s in gm.StageSOs)
        {
            if (s.MapPosition.x == x && s.MapPosition.y == y) return s;
        }
        return null;
    }

    private List<StageWallData> BuildPermanentWallSaveData()
    {
        List<StageWallData> result = new List<StageWallData>();

        foreach (var kvp in gm.PermanentDestroyedWalls)
        {
            StageSO stage = kvp.Key;
            HashSet<Vector2Int> walls = kvp.Value;

            if (stage == null || walls == null || walls.Count == 0) continue;

            StageWallData stageWallData = new StageWallData
            {
                stageX = stage.MapPosition.x,
                stageY = stage.MapPosition.y,
                destroyedWalls = new List<Vector2IntData>()
            };

            foreach (var pos in walls)
            {
                stageWallData.destroyedWalls.Add(new Vector2IntData(pos));
            }

            result.Add(stageWallData);
        }

        return result;
    }

    private void ApplySavedPermanentWalls(List<StageWallData> savedWalls)
    {
        if (savedWalls == null) return;

        foreach (var wallData in savedWalls)
        {
            if (wallData == null || wallData.destroyedWalls == null) continue;

            StageSO stage = FindStageByMapPos(wallData.stageX, wallData.stageY);
            if (stage == null && gm.WorldMapSO != null)
            {
                stage = gm.WorldMapSO.GetStageByMapPosition(new Vector2Int(wallData.stageX, wallData.stageY));
            }

            if (stage == null) continue;

            if (!gm.PermanentDestroyedWalls.ContainsKey(stage))
            {
                gm.PermanentDestroyedWalls[stage] = new HashSet<Vector2Int>();
            }

            foreach (var posData in wallData.destroyedWalls)
            {
                gm.PermanentDestroyedWalls[stage].Add(posData.ToVector2Int());
            }
        }

        gm.ReapplyPermanentDestroyedWalls();
    }

    public void ResetAndStartNewGame()
    {
        GameProgressManager.Instance.ResetEntireProgress();
        gm.ResetAllEnemies();
        gm.DestroyedWalls.Clear();
        gm.PermanentDestroyedWalls.Clear();
        if (gm.StageSOs == null || gm.StageSOs.Length == 0) return;

        gm.CurrentStageSOInternal = gm.StageSOs[0];
        gm.InitialStageSO = gm.StageSOs[0];
        gm.LoadStage(gm.CurrentStageSOInternal, true, gm.CurrentStageSOInternal.PlayerStartPosition);
        gm.SyncWorldMapStatus();

        SaveGame(GameProgressManager.SaveType.Auto);
    }

    public void LoadStageWithCustomStart(StageSO stage, Vector2Int startPos, Direction dir, bool resetEnemy = false)
    {
        if (stage == null) return;
        if (resetEnemy)
        {
            gm.ResetAllEnemies();
        }
        gm.CurrentStageSOInternal = stage;
        gm.Visualizer.ResetGridParentPosition();
        gm.LoadStage(stage, false, startPos, dir);

        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.WarpToNewStage(startPos, dir);
        }

        gm.SyncWorldMapStatus();
    }

    public void DeleteSaveFile(GameProgressManager.SaveType saveType)
    {
        GameProgressManager.Instance.DeleteSaveFile(saveType);
    }
    #endregion
}
