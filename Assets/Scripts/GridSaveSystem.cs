using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

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

        // ===== 1. Collect enemy data for ALL stages =====
        List<StageEnemyData> allStageEnemyData = new List<StageEnemyData>();

        // A) Enemies in active UnitManager (current stage)
        List<EnemySaveData> currentStageEnemyData = new List<EnemySaveData>();
        for (int i = 0; i < gm.UnitManager.Enemies.Count; i++)
        {
            var e = gm.UnitManager.Enemies[i];
            if (e != null)
            {
                currentStageEnemyData.Add(new EnemySaveData
                {
                    gridX = e.CurrentPos.x,
                    gridY = e.CurrentPos.y,
                    direction = (int)e.CurrentDirection,
                    isDead = e.IsDead,
                    currentHP = e.HP.Value,
                    aiState = e.EnemyAI != null ? e.EnemyAI.GetState() : 0,
                    enemyType = (int)e.Type,
                    order = e.Order,
                    maxHP = e.MaxHP,
                    index = i,
                    ammo = e.CurrentAmmo.Value
                });
            }
        }
        allStageEnemyData.Add(new StageEnemyData
        {
            stageX = gm.CurrentStageSOInternal.MapPosition.x,
            stageY = gm.CurrentStageSOInternal.MapPosition.y,
            enemies = currentStageEnemyData
        });

        // B) Enemies in stageEnemiesMap (other stages, already instantiated)
        foreach (var kvp in gm.StageEnemiesMap)
        {
            if (kvp.Key == gm.CurrentStageSOInternal) continue; // Already handled

            List<EnemySaveData> stageData = new List<EnemySaveData>();
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                var e = kvp.Value[i];
                if (e != null)
                {
                    stageData.Add(new EnemySaveData
                    {
                        gridX = e.CurrentPos.x,
                        gridY = e.CurrentPos.y,
                        direction = (int)e.CurrentDirection,
                        isDead = e.IsDead,
                        currentHP = e.HP.Value,
                        aiState = e.EnemyAI != null ? e.EnemyAI.GetState() : 0,
                        enemyType = (int)e.Type,
                        order = e.Order,
                        maxHP = e.MaxHP,
                        index = i,
                        ammo = e.CurrentAmmo.Value
                    });
                }
            }
            allStageEnemyData.Add(new StageEnemyData
            {
                stageX = kvp.Key.MapPosition.x,
                stageY = kvp.Key.MapPosition.y,
                enemies = stageData
            });
        }

        // C) Enemies only in loadedStageEnemyData (not yet instantiated)
        foreach (var kvp in gm.LoadedStageEnemyData)
        {
            // Skip if already saved from stageEnemiesMap
            bool alreadySaved = false;
            foreach (var saved in allStageEnemyData)
            {
                if (saved.stageX == kvp.Key.MapPosition.x && saved.stageY == kvp.Key.MapPosition.y)
                {
                    alreadySaved = true;
                    break;
                }
            }
            if (alreadySaved) continue;

            allStageEnemyData.Add(new StageEnemyData
            {
                stageX = kvp.Key.MapPosition.x,
                stageY = kvp.Key.MapPosition.y,
                enemies = kvp.Value
            });
        }

        // ===== 2. Collect destroyed wall data =====
        List<StageWallData> wallData = new List<StageWallData>();
        foreach (var kvp in gm.DestroyedWalls)
        {
            var wd = new StageWallData();
            wd.stageX = kvp.Key.MapPosition.x;
            wd.stageY = kvp.Key.MapPosition.y;
            wd.destroyedWalls = new List<Vector2IntData>();
            foreach (var pos in kvp.Value)
            {
                wd.destroyedWalls.Add(new Vector2IntData(pos));
            }
            wallData.Add(wd);
        }

        // ===== 3. Save =====
        GameProgressManager.Instance.SaveProgress(
            gm.UnitManager.Player.CurrentPos,
            gm.UnitManager.Player.CurrentDirection,
            gm.UnitManager.Enemies,
            gm.CurrentResetMapPos,
            gm.UnitManager.Player.HP.Value,
            allStageEnemyData,
            wallData,
            saveType
        );
        if (saveType == GameProgressManager.SaveType.Manual && gm.SaveFeedback != null)
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

        // Restore Destroyed Walls
        gm.DestroyedWalls.Clear();
        var savedWalls = GameProgressManager.Instance.SavedAllStagesWalls;
        if (savedWalls != null)
        {
            foreach (var wd in savedWalls)
            {
                StageSO wStage = FindStageByMapPos(wd.stageX, wd.stageY);
                if (wStage != null)
                {
                    var posSet = new HashSet<Vector2Int>();
                    foreach (var posData in wd.destroyedWalls)
                    {
                        posSet.Add(posData.ToVector2Int());
                    }
                    gm.DestroyedWalls[wStage] = posSet;
                }
            }
        }

        // Restore enemy data for non-current stages
        gm.LoadedStageEnemyData.Clear();
        var savedEnemies = GameProgressManager.Instance.SavedAllStagesEnemies;
        if (savedEnemies != null)
        {
            foreach (var stageData in savedEnemies)
            {
                StageSO stage = FindStageByMapPos(stageData.stageX, stageData.stageY);
                if (stage != null && stage != gm.CurrentStageSOInternal)
                {
                    gm.LoadedStageEnemyData[stage] = new List<EnemySaveData>(stageData.enemies);
                }
            }
        }

        // Find the current stage from saved reset map position
        var savedResetPos = GameProgressManager.Instance.SavedResetMapPos;
        StageSO targetStage = null;
        if (gm.WorldMapSO != null)
        {
            targetStage = gm.WorldMapSO.GetStageAt(savedResetPos.x, savedResetPos.y);
        }
        if (targetStage == null) targetStage = gm.CurrentStageSOInternal;

        gm.CurrentStageSOInternal = targetStage;
        gm.InitialStageSO = targetStage;
        gm.CurrentResetMapPos = savedResetPos;

        Vector2Int startPos = GameProgressManager.Instance.SavedPlayerGridPos;
        Direction startDir = GameProgressManager.Instance.SavedPlayerDirection;

        // Apply saved enemy data for the CURRENT stage
        if (savedEnemies != null)
        {
            foreach (var stageData in savedEnemies)
            {
                StageSO stage = FindStageByMapPos(stageData.stageX, stageData.stageY);
                if (stage == targetStage)
                {
                    gm.LoadedStageEnemyData[targetStage] = new List<EnemySaveData>(stageData.enemies);
                    break;
                }
            }
        }

        gm.LoadStage(gm.CurrentStageSOInternal, false, startPos, startDir);

        // Restore HP
        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.SetHP(GameProgressManager.Instance.SavedPlayerHP);
        }

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

    public void ResetAndStartNewGame()
    {
        GameProgressManager.Instance.ResetEntireProgress();
        gm.ResetAllEnemies();
        gm.DestroyedWalls.Clear();
        gm.CurrentStageSOInternal = gm.StageSOs[0];
        gm.InitialStageSO = gm.StageSOs[0];
        gm.LoadStage(gm.CurrentStageSOInternal, true, gm.CurrentStageSOInternal.PlayerStartPosition);
        gm.SyncWorldMapStatus();
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
