using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class StairTraversalSystem : IStairTraversalSystem
{
    private GridManager gm;
    private const float TransitionDuration = 0.2f;
    private const int TransitionDelayMs = 200;

    public StairTraversalSystem(GridManager gridManager)
    {
        this.gm = gridManager;
    }

    public bool CheckAndShowStairDialog()
    {
        if (gm.CurrentStageSOInternal == null || gm.UnitManager.Player == null || gm.WorldMapSO == null)
        {
            return false;
        }

        Vector2Int playerPos = gm.UnitManager.Player.CurrentPos;
        CellType cellType = gm.GetCellTypeAt(gm.CurrentStageSOInternal, playerPos);

        if (cellType != CellType.StairUp && cellType != CellType.StairDown)
        {
            return false;
        }

        StageSO targetStage = null;
        targetStage = gm.CurrentStageSOInternal.GetConnectedStage(playerPos);

        if (targetStage == null)
        {
            if (cellType == CellType.StairUp)
            {
                targetStage = gm.CurrentStageSOInternal.DefaultUpStage;
            }
            else if (cellType == CellType.StairDown)
            {
                targetStage = gm.CurrentStageSOInternal.DefaultDownStage;
            }
        }

        if (targetStage == null && gm.WorldMapSO != null)
        {
            int currentFloor = gm.CurrentStageSOInternal.FloorIndex;
            int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;
            targetStage = gm.WorldMapSO.GetStageByFloorIndex(targetFloor);

            if (targetStage == null)
            {
                Debug.Log($"[Stair] No stage found for {cellType}");
                return false;
            }
        }

        if (IsPositionOccupiedInStage(targetStage, playerPos))
        {
            Debug.Log($"[Stair] Cannot take stairs: destination is occupied");
            return false;
        }

        ShowStairConfirmationDialog(cellType, targetStage);
        return true;
    }

    private void ShowStairConfirmationDialog(CellType stairType, StageSO targetStage)
    {
        if (gm.StairConfirmationPresenter == null)
        {
            Debug.LogError("[Stair] stairConfirmationPresenter is NULL!");
            return;
        }

        string displayName = GetFloorDisplayName(targetStage.FloorIndex);
        bool goingDown = stairType == CellType.StairDown;

        gm.StairConfirmationPresenter.ShowAsync(stairType, targetStage, displayName, (confirmed) =>
        {
            if (confirmed)
            {
                TakeStairAsync(targetStage, goingDown).Forget();
            }
            else
            {
                if (gm.UnitManager.Player != null)
                {
                    gm.UnitManager.Player.ForceActionComplete();
                }
            }
        }).Forget();
    }

    private string GetFloorDisplayName(int floorIndex)
    {
        if (floorIndex == 0) return "1F";
        if (floorIndex > 0) return $"B{floorIndex}F";
        return $"{-floorIndex + 1}F";
    }

    public async UniTask TakeStairAsync(Vector2Int pos, CellType cellType)
    {
        // Interface implementation if needed, but logic is internal mostly
        // This method signature in interface is slightly different from original check logic.
        // Let's implement full logic from original TakeStairAsync(targetStage, goingDown)
        // We might need to find targetStage again if called from external
        return;
    }

    // Internal use
    private async UniTask TakeStairAsync(StageSO targetStage, bool goingDown)
    {
        if (targetStage == null) return;

        gm.IsTransitioning = true;

        Vector2Int currentPlayerPos = gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentPos : Vector2Int.zero;
        Direction currentDirection = gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentDirection : Direction.Right;
        Vector2Int targetPlayerPos = currentPlayerPos;

        GridCalculation.WarpDirection stairWarpDir = GridCalculation.DetermineStairWarpDirection(gm.CurrentStageSOInternal, targetStage, goingDown);

        HashSet<Vector2Int> destroyed = gm.DestroyedWalls.ContainsKey(targetStage) ? gm.DestroyedWalls[targetStage] : null;

        // 階段先が占有されている場合は CheckAndShowStairDialog で既にブロック済み

        if (gm.UnitManager.Player != null)
        {
            await gm.UnitManager.Player.FadeOutAsync(0.15f, goingDown);
        }

        gm.Visualizer.SetupStairTransitionTilemapWithDirection(targetStage, stairWarpDir, currentPlayerPos, targetPlayerPos, destroyed, GameProgressManager.Instance.IsPositionCleared);

        Vector3 transitionOffset = gm.Visualizer.GetWarpTransitionOffset(stairWarpDir);
        gm.PrepareEnemiesForTransition(targetStage, transitionOffset);

        gm.UnitManager.SetAllEnemiesActive(false);

        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.gameObject.transform.SetParent(gm.Visualizer.GridParent);
        }

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
        gm.Visualizer.MoveGridParent(transitionOffset, TransitionDuration);
        await UniTask.Delay(TransitionDelayMs);

        var cmd = new PlayerStairTransitionCommand(gm, this, gm.CurrentStageSOInternal, targetStage, currentPlayerPos, currentDirection, targetPlayerPos, currentDirection);
        gm.CommandManager.ExecuteCommand(cmd);

        if (gm.UnitManager.Player != null)
        {
            await gm.UnitManager.Player.FadeInAsync(0.2f, goingDown);
        }

        if (goingDown && targetStage != null)
        {
            int column = targetStage.StairColumn;
            int depth = targetStage.MapPosition.y;
            GameProgressManager.Instance.UnlockCellByStairDescent(column, depth);
        }

        gm.SyncWorldMapStatus();
        await gm.TurnManager.OnPlayerActionComplete(true);

        gm.IsTransitioning = false;
    }

    public async UniTask ShowStairConfirmationDialog(Vector2Int pos, CellType cellType)
    {
         // Interface impl if needed
         CheckAndShowStairDialog();
    }

    // Explicitly implementing interface method if logic differs, but here CheckAndShow handles it.

    public class PlayerStairTransitionCommand : ICommand, IAsyncUndoCommand
    {
        private GridManager gm;
        private StageSO sourceStage;
        private StageSO targetStage;
        private Vector2Int sourcePos;
        private Direction sourceDir;
        private Vector2Int targetPos;
        private Direction targetDir;

        public PlayerStairTransitionCommand(GridManager gm, StairTraversalSystem system, StageSO source, StageSO target,
            Vector2Int sPos, Direction sDir, Vector2Int tPos, Direction tDir)
        {
            this.gm = gm;
            this.sourceStage = source;
            this.targetStage = target;
            this.sourcePos = sPos;
            this.sourceDir = sDir;
            this.targetPos = tPos;
            this.targetDir = tDir;
        }

        public void Execute()
        {
            PerformTransition(targetStage, targetPos, targetDir);
            gm.SaveGame(GameProgressManager.SaveType.Auto);
        }

        public void Undo() { UndoAsync().Forget(); }

        public async UniTask UndoAsync()
        {
            PerformTransition(sourceStage, sourcePos, sourceDir);
        }

        private void PerformTransition(StageSO stage, Vector2Int p, Direction d)
        {
            if (gm.UnitManager.Player != null)
            {
                gm.UnitManager.Player.gameObject.transform.SetParent(null);
            }

            gm.CurrentStageSOInternal = stage;

            if (gm.UnitManager.Player != null)
            {
                gm.UnitManager.Player.WarpToNewStage(p, d);
            }

            gm.LoadStage(stage, false, p, d, false);
        }
    }

    // ==========================================
    // Enemy Stair Logic
    // ==========================================

    public (StageSO targetStage, Vector2Int targetPos)? GetEnemyStairDestination(Enemy enemy, Vector2Int stairPos, Direction approachDirection)
    {
        if (gm.CurrentStageSOInternal == null) return null;

        CellType cellType = gm.GetCellTypeAt(gm.CurrentStageSOInternal, stairPos);
        if (cellType != CellType.StairUp && cellType != CellType.StairDown)
        {
            return null;
        }

        StageSO targetStage = null;
        Vector2Int targetStairPos = Vector2Int.zero;

        StairConnectionData? connectionData = gm.CurrentStageSOInternal.GetStairConnectionData(stairPos);
        if (connectionData != null)
        {
            targetStage = connectionData.Value.TargetStage;
            targetStairPos = connectionData.Value.TargetPosition;
            if (targetStairPos == Vector2Int.zero && connectionData.Value.Position != Vector2Int.zero)
            {
                targetStairPos = stairPos;
            }
        }
        else
        {
            if (cellType == CellType.StairUp)
            {
                targetStage = gm.CurrentStageSOInternal.DefaultUpStage;
                targetStairPos = stairPos;
            }
            else if (cellType == CellType.StairDown)
            {
                targetStage = gm.CurrentStageSOInternal.DefaultDownStage;
                targetStairPos = stairPos;
            }

            if (targetStage == null && gm.WorldMapSO != null)
            {
                int currentFloor = gm.CurrentStageSOInternal.FloorIndex;
                int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;
                targetStage = gm.WorldMapSO.GetStageByFloorIndex(targetFloor);
                targetStairPos = stairPos;
            }
        }

        if (targetStage == null) return null;

        Vector2Int targetPos = targetStairPos;
        if (!gm.IsWalkableAt(targetStage, targetPos)) return null;

        // 階段先にUnitがいる場合は移動不可
        if (IsPositionOccupiedInStage(targetStage, targetPos))
        {
            return null;
        }

        return (targetStage, targetPos);
    }

    public bool IsPositionOccupiedInStage(StageSO stage, Vector2Int pos)
    {
        if (stage == gm.CurrentStageSOInternal)
        {
            return gm.UnitManager.GetUnitAtPosition(pos) != null;
        }

        bool hasStageEnemies = gm.StageEnemiesMap.ContainsKey(stage);

        if (hasStageEnemies)
        {
            foreach (var enemy in gm.StageEnemiesMap[stage])
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos) return true;
            }
        }

        if (gm.PendingStageEnemies.TryGetValue(stage, out var pendingList))
        {
            foreach (var enemy in pendingList)
            {
                if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos) return true;
            }
        }

        if (hasStageEnemies)
        {
            if (gm.LoadedStageEnemyData.TryGetValue(stage, out var dataList))
            {
                foreach (var data in dataList)
                {
                    if (!data.isDead && data.index == -1 && data.gridX == pos.x && data.gridY == pos.y) return true;
                }
            }
        }
        else if (gm.LoadedStageEnemyData.TryGetValue(stage, out var enemyDataList))
        {
            foreach (var data in enemyDataList)
            {
                if (!data.isDead && data.gridX == pos.x && data.gridY == pos.y) return true;
            }
        }
        else if (stage.EnemyStartData != null)
        {
            foreach (var startData in stage.EnemyStartData)
            {
                if (startData.Position == pos) return true;
            }
        }

        return false;
    }

    internal void EnsureLoadedStageEnemyDataInitialized(StageSO stage)
    {
        if (!gm.LoadedStageEnemyData.ContainsKey(stage))
        {
            gm.LoadedStageEnemyData[stage] = new List<EnemySaveData>();
            if (!gm.StageEnemiesMap.ContainsKey(stage) && stage.EnemyStartData != null)
            {
                int idx = 0;
                foreach (var esd in stage.EnemyStartData)
                {
                    gm.LoadedStageEnemyData[stage].Add(new EnemySaveData
                    {
                        gridX = esd.Position.x,
                        gridY = esd.Position.y,
                        direction = (int)esd.Direction,
                        isDead = false,
                        currentHP = esd.MaxHP,
                        maxHP = esd.MaxHP,
                        enemyType = (int)esd.EnemyType,
                        order = esd.SetActionOrder,
                        index = idx++,
                        ammo = esd.Ammo
                    });
                }
            }
        }
    }

    public async UniTask MoveEnemyToStage(Enemy enemy, StageSO targetStage, Vector2Int targetPos, Direction direction)
    {
        if (enemy == null || targetStage == null) return;

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);

        bool goingDown = false;
        if (gm.CurrentStageSOInternal != null)
        {
            CellType cell = gm.GetCellTypeAt(gm.CurrentStageSOInternal, enemy.CurrentPos);
            if (cell == CellType.StairDown) goingDown = true;
        }

        await enemy.FadeOutAsync(0.15f, goingDown);

        var cmd = new EnemyStairTransitionCommand(gm, this, enemy, gm.CurrentStageSOInternal, targetStage, targetPos, direction);
        gm.CommandManager.ExecuteCommand(cmd);
    }

    private class EnemyStairTransitionCommand : ICommand, IAsyncUndoCommand
    {
        private GridManager gm;
        private StairTraversalSystem system;
        private Enemy targetEnemy;
        private StageSO sourceStage;
        private StageSO targetStage;
        private EnemySaveData movedData;
        private EnemySaveData restoreData;

        public EnemyStairTransitionCommand(GridManager gm, StairTraversalSystem system, Enemy enemy, StageSO source, StageSO target, Vector2Int targetPos, Direction dir)
        {
            this.gm = gm;
            this.system = system;
            this.targetEnemy = enemy;
            this.sourceStage = source ?? gm.CurrentStageSOInternal;
            this.targetStage = target;

            this.movedData = new EnemySaveData
            {
                gridX = targetPos.x,
                gridY = targetPos.y,
                direction = (int)dir,
                isDead = false,
                currentHP = enemy.HP.Value,
                aiState = enemy.EnemyAI != null ? enemy.EnemyAI.GetState() : 0,
                enemyType = (int)enemy.Type,
                order = enemy.Order,
                maxHP = enemy.MaxHP,
                index = -1,
                ammo = enemy.CurrentAmmo.Value
            };

            this.restoreData = new EnemySaveData
            {
                gridX = enemy.CurrentPos.x,
                gridY = enemy.CurrentPos.y,
                direction = (int)enemy.CurrentDirection,
                isDead = false,
                currentHP = enemy.HP.Value,
                aiState = enemy.EnemyAI != null ? enemy.EnemyAI.GetState() : 0,
                enemyType = (int)enemy.Type,
                order = enemy.Order,
                maxHP = enemy.MaxHP,
                index = -1,
                ammo = enemy.CurrentAmmo.Value
            };
        }

        public void Execute()
        {
            if (targetEnemy != null)
            {
                if (gm.StageEnemiesMap.ContainsKey(sourceStage))
                {
                    gm.StageEnemiesMap[sourceStage].Remove(targetEnemy);
                }

                gm.UnitManager.RemoveEnemy(targetEnemy);

                targetEnemy.WarpTo(new Vector2Int(movedData.gridX, movedData.gridY), (Direction)movedData.direction);
                targetEnemy.gameObject.SetActive(false);
                targetEnemy.transform.SetParent(gm.transform);
            }

            // EnemyStartData から既存の敵データを正しく初期化する
            system.EnsureLoadedStageEnemyDataInitialized(targetStage);

            gm.LoadedStageEnemyData[targetStage].Add(movedData);

            // Add to pending (so if we visit that stage, it spawns)
            gm.AddPendingEnemy(targetStage, targetEnemy);

            gm.SaveGame(GameProgressManager.SaveType.Auto);
        }

        public void Undo() { UndoAsync().Forget(); }
        public async UniTask UndoAsync()
        {
            // Remove from target stage data
            if (gm.LoadedStageEnemyData.TryGetValue(targetStage, out var list))
            {
                var targetIndex = -1;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].index == -1 &&
                        list[i].gridX == movedData.gridX &&
                        list[i].gridY == movedData.gridY &&
                        list[i].enemyType == movedData.enemyType)
                    {
                        targetIndex = i;
                        break;
                    }
                }
                if (targetIndex != -1)
                {
                    list.RemoveAt(targetIndex);
                }

                // 空の LoadedStageEnemyData を残すと IsPositionOccupiedInStage が
                // EnemyStartData をチェックできなくなるため削除する
                if (list.Count == 0)
                {
                    gm.LoadedStageEnemyData.Remove(targetStage);
                }
            }

            // Restore to source stage
            if (targetEnemy != null)
            {
                if (gm.PendingStageEnemies.ContainsKey(targetStage))
                {
                    gm.PendingStageEnemies[targetStage].Remove(targetEnemy);
                }

                if (gm.StageEnemiesMap.ContainsKey(targetStage))
                {
                    gm.StageEnemiesMap[targetStage].Remove(targetEnemy);
                }

                targetEnemy.transform.SetParent(gm.Visualizer.GridParent);
                targetEnemy.gameObject.SetActive(true);
                targetEnemy.WarpTo(new Vector2Int(restoreData.gridX, restoreData.gridY), (Direction)restoreData.direction);

                gm.UnitManager.AddEnemy(targetEnemy);

                if (!gm.StageEnemiesMap.ContainsKey(sourceStage))
                {
                    gm.StageEnemiesMap[sourceStage] = new List<Enemy>();
                }
                gm.StageEnemiesMap[sourceStage].Add(targetEnemy);

                 await targetEnemy.FadeInAsync(0.15f, false);
            }
        }
    }
}
