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

    public (StageSO targetStage, Vector2Int targetPos)? GetPlayerStairDestination(Player player, Vector2Int stairPos)
    {
        StageSO sourceStage = gm.CurrentStageSOInternal;
        if (sourceStage == null) return null;

        CellType cellType = gm.GetCellTypeAt(sourceStage, stairPos);
        if (cellType != CellType.StairUp && cellType != CellType.StairDown) return null;

        StageSO targetStage = null;
        Vector2Int targetStairPos = Vector2Int.zero;

        StairConnectionData? connectionData = sourceStage.GetStairConnectionData(stairPos);
        if (connectionData != null)
        {
            targetStage = connectionData.Value.TargetStage;
            targetStairPos = connectionData.Value.TargetPosition;
            if (targetStairPos == Vector2Int.zero && connectionData.Value.Position != Vector2Int.zero)
                targetStairPos = stairPos;
        }
        else
        {
            if (cellType == CellType.StairUp) targetStage = sourceStage.DefaultUpStage;
            else if (cellType == CellType.StairDown) targetStage = sourceStage.DefaultDownStage;

            if (targetStage == null && gm.WorldMapSO != null)
            {
                int currentFloor = sourceStage.FloorIndex;
                int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;
                targetStage = gm.WorldMapSO.GetStageByFloorIndex(targetFloor);
            }
            targetStairPos = new Vector2Int(-1, -1);
        }

        if (targetStage != null && (targetStairPos == new Vector2Int(-1, -1) || targetStairPos == Vector2Int.zero))
        {
            CellType targetType = (cellType == CellType.StairUp) ? CellType.StairDown : CellType.StairUp;
            bool found = false;

            if (gm.GetCellTypeAt(targetStage, stairPos) == targetType)
            {
                targetStairPos = stairPos;
                found = true;
            }
            else
            {
                Vector2Int? foundStair = FindStairPosition(targetStage, targetType);
                if (foundStair.HasValue)
                {
                    targetStairPos = foundStair.Value;
                    found = true;
                }
            }

            if (!found) targetStairPos = targetStage.PlayerStartPosition;
        }

        if (targetStage == null) return null;
        return (targetStage, targetStairPos);
    }

    public bool CheckAndShowStairDialog()
    {
        if (gm.CurrentStageSOInternal == null || gm.UnitManager.Player == null || gm.WorldMapSO == null)
            return false;

        Vector2Int playerPos = gm.UnitManager.Player.CurrentPos;
        CellType cellType = gm.GetCellTypeAt(gm.CurrentStageSOInternal, playerPos);

        if (cellType == CellType.BrokenStairUp)
        {
            if (gm.BlockedPrefab != null) Object.Instantiate(gm.BlockedPrefab, gm.GridToWorldPosition(playerPos), Quaternion.identity, gm.Visualizer.GridParent);
            return false;
        }

        if (cellType != CellType.StairUp && cellType != CellType.StairDown) return false;

        var destination = GetPlayerStairDestination(gm.UnitManager.Player, playerPos);
        if (destination == null)
        {
            if (gm.BlockedPrefab != null) Object.Instantiate(gm.BlockedPrefab, gm.GridToWorldPosition(playerPos), Quaternion.identity, gm.Visualizer.GridParent);
            return false;
        }

        StageSO targetStage = destination.Value.targetStage;
        Vector2Int targetPos = destination.Value.targetPos;

        if (IsPositionOccupiedInStage(targetStage, targetPos))
        {
            Debug.Log($"[Stair] Cannot take stairs: destination {targetPos} is occupied in {targetStage.name}");
            if (gm.BlockedPrefab != null)
            {
                Object.Instantiate(gm.BlockedPrefab, gm.GridToWorldPosition(playerPos), Quaternion.identity, gm.Visualizer.GridParent);
            }
            return false;
        }

        bool goingDown = cellType == CellType.StairDown;
        TakeStairAsync(targetStage, goingDown, targetPos).Forget();
        return true;
    }

    private void ShowStairConfirmationDialog(CellType stairType, StageSO targetStage, Vector2Int targetPos)
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
                TakeStairAsync(targetStage, goingDown, targetPos).Forget();
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
    private async UniTask TakeStairAsync(StageSO targetStage, bool goingDown, Vector2Int targetPlayerPos)
    {
        if (targetStage == null) return;

        gm.IsTransitioning = true;

        Vector2Int currentPlayerPos = gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentPos : Vector2Int.zero;
        Direction currentDirection = gm.UnitManager.Player != null ? gm.UnitManager.Player.CurrentDirection : Direction.Right;

        GridCalculation.WarpDirection stairWarpDir = GridCalculation.DetermineStairWarpDirection(gm.CurrentStageSOInternal, targetStage, goingDown);

        HashSet<Vector2Int> destroyed = gm.DestroyedWalls.ContainsKey(targetStage) ? gm.DestroyedWalls[targetStage] : null;

        // 階段先が占有されている場合は CheckAndShowStairDialog で既にブロック済み

        if (gm.UnitManager.Player != null)
        {
            await gm.UnitManager.Player.FadeOutAsync(0.15f, goingDown);
        }

        Vector3 transitionOffset = GridCalculation.CalculateWarpTransitionOffset(stairWarpDir, gm.Visualizer.TilemapOffset, gm.CurrentStageSOInternal, targetStage, gm.Visualizer.CellSize);
        Vector3 initialOffset = -transitionOffset;

        gm.Visualizer.SetupStairTransitionTilemapWithDirection(targetStage, initialOffset, currentPlayerPos, targetPlayerPos, destroyed, GameProgressManager.Instance.IsPositionCleared);

        // Vector3 transitionOffset = GridCalculation.CalculateWarpTransitionOffset(stairWarpDir, gm.Visualizer.TilemapOffset); // Already calculated above
        gm.PrepareEnemiesForTransition(targetStage, -transitionOffset);

        gm.UnitManager.SetAllEnemiesActive(false);

        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.gameObject.transform.SetParent(gm.Visualizer.GridParent);
        }

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);
        gm.Visualizer.MoveGridParent(transitionOffset, TransitionDuration);
        await UniTask.Delay(TransitionDelayMs);

        if (gm.UnitManager.Player != null)
        {
            gm.UnitManager.Player.gameObject.transform.SetParent(null);
        }
        gm.Visualizer.ResetGridParentPosition();

        var cmd = new PlayerStairTransitionCommand(gm, this, gm.CurrentStageSOInternal, targetStage, currentPlayerPos, currentDirection, targetPlayerPos, currentDirection);
        gm.CommandManager.ExecuteCommand(cmd);

        if (gm.UnitManager.Player != null)
        {
            await gm.UnitManager.Player.FadeInAsync(0.2f, goingDown);
        }

        if (targetStage != null)
        {
            int column = targetStage.StairColumn;
            int depth = targetStage.MapPosition.y;
            GameProgressManager.Instance.UnlockCellByStairDescent(column, depth);
        }

        gm.Visualizer.ClearTransitionTiles();

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
        StageSO sourceStage = gm.GetStageOfEnemy(enemy);
        if (sourceStage == null) sourceStage = gm.CurrentStageSOInternal; // Fallback
        if (sourceStage == null) return null;

        CellType cellType = gm.GetCellTypeAt(sourceStage, stairPos);
        if (cellType != CellType.StairUp && cellType != CellType.StairDown)
        {
            return null;
        }

        StageSO targetStage = null;
        Vector2Int targetStairPos = Vector2Int.zero;

        StairConnectionData? connectionData = sourceStage.GetStairConnectionData(stairPos);
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
                targetStage = sourceStage.DefaultUpStage;
            }
            else if (cellType == CellType.StairDown)
            {
                targetStage = sourceStage.DefaultDownStage;
            }

            if (targetStage == null && gm.WorldMapSO != null)
            {
                int currentFloor = sourceStage.FloorIndex;
                int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;
                targetStage = gm.WorldMapSO.GetStageByFloorIndex(targetFloor);
            }
            targetStairPos = new Vector2Int(-1, -1);
        }

        // Attempt to find matching stair in target stage if explicit position is unknown
        if (targetStage != null && (targetStairPos == new Vector2Int(-1, -1) || targetStairPos == Vector2Int.zero))
        {
            CellType targetType = (cellType == CellType.StairUp) ? CellType.StairDown : CellType.StairUp;
            bool found = false;

            // Try to find the matching stair at the SAME position first
            if (gm.GetCellTypeAt(targetStage, stairPos) == targetType)
            {
                targetStairPos = stairPos;
                found = true;
            }
            else
            {
                // Search for ANY matching stair
                Vector2Int? foundStair = FindStairPosition(targetStage, targetType);
                if (foundStair.HasValue)
                {
                    targetStairPos = foundStair.Value;
                    found = true;
                }
            }

            if (!found)
            {
                // Fallback to Player Start Position if no stair found
                targetStairPos = targetStage.PlayerStartPosition;
            }
        }

        if (targetStage == null) return null;

        Vector2Int targetPos = targetStairPos;
        if (!gm.IsWalkableAt(targetStage, targetPos)) return null;

        // 階段先にUnitがいる場合は移動不可
        if (IsPositionOccupiedInStage(targetStage, targetPos))
        {
            if (gm.BlockedPrefab != null)
            {
                Object.Instantiate(gm.BlockedPrefab, gm.GridToWorldPosition(stairPos), Quaternion.identity, gm.Visualizer.GridParent);
            }
            return null;
        }

        return (targetStage, targetPos);
    }

    private Vector2Int? FindStairPosition(StageSO stage, CellType targetType)
    {
        for (int y = 0; y < stage.Height; y++)
        {
            for (int x = 0; x < stage.Width; x++)
            {
                if (gm.GetCellTypeAt(stage, new Vector2Int(x, y)) == targetType)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return null; // Not found
    }

    public bool IsPositionOccupiedInStage(StageSO stage, Vector2Int pos)
    {
        if (stage == gm.CurrentStageSOInternal)
        {
            return gm.UnitManager.GetUnitAtPosition(pos) != null;
        }

        if (gm.StageCratesMap.TryGetValue(stage, out var crates))
        {
            foreach (var crate in crates)
            {
                if (crate != null && !crate.IsDead && crate.CurrentPos == pos) return true;
            }
        }
        else if (stage.CrateStartPositions != null)
        {
            foreach (var cratePos in stage.CrateStartPositions)
            {
                if (cratePos == pos) return true;
            }
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

            if (gm.StageEnemiesMap.ContainsKey(stage))
            {
                // Snapshot current existing enemies
                int idx = 0;
                foreach (var enemy in gm.StageEnemiesMap[stage])
                {
                    if (enemy == null) continue;
                    gm.LoadedStageEnemyData[stage].Add(new EnemySaveData
                    {
                        gridX = enemy.CurrentPos.x,
                        gridY = enemy.CurrentPos.y,
                        direction = (int)enemy.CurrentDirection,
                        isDead = enemy.IsDead,
                        currentHP = enemy.HP.Value,
                        maxHP = enemy.MaxHP,
                        enemyType = (int)enemy.Type,
                        order = enemy.Order,
                        index = -1, // Dynamically loaded, no fixed index
                        aiState = enemy.EnemyAI != null ? enemy.EnemyAI.GetState() : 0,
                        ammo = enemy.CurrentAmmo.Value
                    });
                }
            }
            else if (stage.EnemyStartData != null)
            {
                // Load defaults
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

        StageSO sourceStage = gm.GetStageOfEnemy(enemy);
        if (sourceStage == null) sourceStage = gm.CurrentStageSOInternal;
        Vector2Int sourcePos = enemy.CurrentPos;

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);

        bool goingDown = false;
        if (sourceStage != null)
        {
            CellType cell = gm.GetCellTypeAt(sourceStage, enemy.CurrentPos);
            if (cell == CellType.StairDown) goingDown = true;
        }

        await enemy.FadeOutAsync(0.15f, goingDown);

        if (enemy == null || enemy.IsDead) return;
        if (gm.CommandManager.IsUndoing || gm.CommandManager.IsRedoing) return;

        StageSO currentStage = gm.GetStageOfEnemy(enemy) ?? gm.CurrentStageSOInternal;
        if (currentStage != sourceStage || enemy.CurrentPos != sourcePos) return;

        var cmd = new EnemyStairTransitionCommand(gm, this, enemy, sourceStage, targetStage, targetPos, direction);
        gm.CommandManager.ExecuteCommandWithTurnFallback(cmd);
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
            // 移動先のステージの最大Order+1を計算して設定
            movedData.order = gm.GetMaxEnemyOrderInStage(targetStage) + 1;

            if (targetEnemy != null)
            {
                if (gm.StageEnemiesMap.ContainsKey(sourceStage))
                {
                    gm.StageEnemiesMap[sourceStage].Remove(targetEnemy);
                }

                gm.UnitManager.RemoveEnemy(targetEnemy);

                targetEnemy.WarpTo(new Vector2Int(movedData.gridX, movedData.gridY), (Direction)movedData.direction);
                targetEnemy.SetOrder(movedData.order);
                targetEnemy.gameObject.SetActive(false);
                targetEnemy.transform.SetParent(gm.transform);
            }

            // EnemyStartData から既存の敵データを正しく初期化する
            system.EnsureLoadedStageEnemyDataInitialized(targetStage);

            gm.LoadedStageEnemyData[targetStage].Add(movedData);

            // Add to pending (so if we visit that stage, it spawns)
            gm.AddPendingEnemy(targetStage, targetEnemy);

            gm.UnitManager.SortEnemiesByOrder();
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
                targetEnemy.SetOrder(restoreData.order);

                gm.UnitManager.AddEnemy(targetEnemy);

                if (!gm.StageEnemiesMap.ContainsKey(sourceStage))
                {
                    gm.StageEnemiesMap[sourceStage] = new List<Enemy>();
                }
                gm.StageEnemiesMap[sourceStage].Add(targetEnemy);

                gm.UnitManager.SortEnemiesByOrder();
                 await targetEnemy.FadeInAsync(0.15f, false);
            }
        }
    }

    public (StageSO targetStage, Vector2Int targetPos)? GetCrateStairDestination(Crate crate, Vector2Int stairPos, Direction approachDirection)
    {
        StageSO sourceStage = gm.GetStageOfCrate(crate);
        if (sourceStage == null) sourceStage = gm.CurrentStageSOInternal; // Fallback
        if (sourceStage == null) return null;

        CellType cellType = gm.GetCellTypeAt(sourceStage, stairPos);
        if (cellType != CellType.StairUp && cellType != CellType.StairDown)
        {
            return null;
        }

        StageSO targetStage = null;
        Vector2Int targetStairPos = Vector2Int.zero;

        StairConnectionData? connectionData = sourceStage.GetStairConnectionData(stairPos);
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
            if (cellType == CellType.StairUp) targetStage = sourceStage.DefaultUpStage;
            else if (cellType == CellType.StairDown) targetStage = sourceStage.DefaultDownStage;

            if (targetStage == null && gm.WorldMapSO != null)
            {
                int currentFloor = sourceStage.FloorIndex;
                int targetFloor = cellType == CellType.StairUp ? currentFloor - 1 : currentFloor + 1;
                targetStage = gm.WorldMapSO.GetStageByFloorIndex(targetFloor);
            }
            targetStairPos = new Vector2Int(-1, -1);
        }

        if (targetStage != null && (targetStairPos == new Vector2Int(-1, -1) || targetStairPos == Vector2Int.zero))
        {
            CellType targetType = (cellType == CellType.StairUp) ? CellType.StairDown : CellType.StairUp;
            bool found = false;

            if (gm.GetCellTypeAt(targetStage, stairPos) == targetType)
            {
                targetStairPos = stairPos;
                found = true;
            }
            else
            {
                Vector2Int? foundStair = FindStairPosition(targetStage, targetType);
                if (foundStair.HasValue)
                {
                    targetStairPos = foundStair.Value;
                    found = true;
                }
            }

            if (!found) targetStairPos = targetStage.PlayerStartPosition;
        }

        if (targetStage == null) return null;

        Vector2Int targetPos = targetStairPos;
        if (!gm.IsWalkableAt(targetStage, targetPos)) return null;

        if (IsPositionOccupiedInStage(targetStage, targetPos))
        {
            if (gm.BlockedPrefab != null)
            {
                Object.Instantiate(gm.BlockedPrefab, gm.GridToWorldPosition(stairPos), Quaternion.identity, gm.Visualizer.GridParent);
            }
            return null;
        }

        return (targetStage, targetPos);
    }

    public async UniTask MoveCrateToStage(Crate crate, StageSO targetStage, Vector2Int targetPos, Direction direction)
    {
        if (crate == null || targetStage == null) return;

        StageSO sourceStage = gm.GetStageOfCrate(crate);
        if (sourceStage == null) sourceStage = gm.CurrentStageSOInternal;

        AudioManager.Instance.PlaySE(AudioManager.SEType.warp);

        bool goingDown = false;
        if (sourceStage != null)
        {
            CellType cell = gm.GetCellTypeAt(sourceStage, crate.CurrentPos);
            if (cell == CellType.StairDown) goingDown = true;
        }

        await crate.FadeOutAsync(0.15f, goingDown);

        var cmd = new CrateStairTransitionCommand(gm, crate, sourceStage, targetStage, targetPos, direction);
        gm.CommandManager.ExecuteCommand(cmd);
    }

    private class CrateStairTransitionCommand : ICommand, IAsyncUndoCommand
    {
        private GridManager gm;
        private Crate targetCrate;
        private StageSO sourceStage;
        private StageSO targetStage;
        private Vector2Int targetPos;
        private Direction targetDir;
        private Vector2Int sourcePos;
        private Direction sourceDir;

        public CrateStairTransitionCommand(GridManager gm, Crate crate, StageSO source, StageSO target, Vector2Int tPos, Direction dir)
        {
            this.gm = gm;
            this.targetCrate = crate;
            this.sourceStage = source ?? gm.CurrentStageSOInternal;
            this.targetStage = target;
            this.targetPos = tPos;
            this.targetDir = dir;
            this.sourcePos = crate.CurrentPos;
            this.sourceDir = crate.CurrentDirection;
        }

        public void Execute()
        {
            if (targetCrate != null)
            {
                if (gm.StageCratesMap.ContainsKey(sourceStage)) gm.StageCratesMap[sourceStage].Remove(targetCrate);
                gm.UnitManager.RemoveCrate(targetCrate);

                targetCrate.WarpTo(targetPos, targetDir);
                targetCrate.gameObject.SetActive(false);
                targetCrate.transform.SetParent(gm.transform);

                if (!gm.StageCratesMap.ContainsKey(targetStage)) gm.StageCratesMap[targetStage] = new List<Crate>();
                gm.StageCratesMap[targetStage].Add(targetCrate);
            }
        }

        public void Undo() { UndoAsync().Forget(); }
        public async UniTask UndoAsync()
        {
            if (targetCrate != null)
            {
                if (gm.StageCratesMap.ContainsKey(targetStage)) gm.StageCratesMap[targetStage].Remove(targetCrate);

                targetCrate.transform.SetParent(gm.Visualizer.GridParent);
                targetCrate.gameObject.SetActive(true);
                targetCrate.WarpTo(sourcePos, sourceDir);

                gm.UnitManager.AddCrate(targetCrate);

                if (!gm.StageCratesMap.ContainsKey(sourceStage)) gm.StageCratesMap[sourceStage] = new List<Crate>();
                gm.StageCratesMap[sourceStage].Add(targetCrate);

                await targetCrate.FadeInAsync(0.15f, false);
            }
        }
    }

    // ステージ境界通過でCrateを隣接ステージへ即座にワープ
    public async UniTask MoveCrateThroughBoundary(Crate crate, StageSO sourceStage, StageSO targetStage, Vector2Int targetPos, Direction direction)
    {
        if (crate == null || targetStage == null) return;

        var cmd = new CrateBoundaryTransitionCommand(gm, crate, sourceStage, targetStage, targetPos, direction);
        // 着地先に押し出せないCrateがある場合はコマンドを追加しない（Crateはステージを越えない）
        if (!cmd.IsValid) return;
        // CompleteTurn()後に呼ばれるため、AppendToLastTurnで直前のターンに追記する
        gm.CommandManager.AppendToLastTurn(cmd);
        await UniTask.Yield();
    }

    private class CrateBoundaryTransitionCommand : ICommand, IAsyncUndoCommand
    {
        private GridManager gm;
        private Crate targetCrate;
        private StageSO sourceStage;
        private StageSO targetStage;
        private Vector2Int sourcePos;
        private Direction sourceDir;
        private Vector2Int targetPos;
        private Direction targetDir;

        // 着地先の隣接ステージにすでに存在するCrateの連鎖押し出し情報
        private List<Crate> chainCrates;
        private List<Vector2Int> chainSourcePositions;
        private List<Vector2Int> chainTargetPositions;

        /// <summary>着地先にスペースがあり、Crateをステージ越えさせてよい場合 true</summary>
        public bool IsValid { get; private set; } = true;

        public CrateBoundaryTransitionCommand(GridManager gm, Crate crate, StageSO source, StageSO target, Vector2Int tPos, Direction dir)
        {
            this.gm = gm;
            this.targetCrate = crate;
            this.sourceStage = source ?? gm.CurrentStageSOInternal;
            this.targetStage = target;
            this.targetPos = tPos;
            this.targetDir = dir;
            this.sourcePos = crate.CurrentPos;
            this.sourceDir = crate.CurrentDirection;

            BuildChainPush(tPos, dir);
        }

        /// <summary>
        /// 着地先 entryPos から進行方向に連続するCrateを収集し、
        /// まとめて1マスずつ押し出せるか検証する。
        /// </summary>
        private void BuildChainPush(Vector2Int entryPos, Direction dir)
        {
            chainCrates = new List<Crate>();
            chainSourcePositions = new List<Vector2Int>();
            chainTargetPositions = new List<Vector2Int>();

            if (targetStage == null) return;

            gm.EnsureStageCratesLoaded(targetStage);

            Vector2Int dirVec = Utility.GetVectorFromDirection(dir);
            Vector2Int scanPos = entryPos;

            // 進行方向に並ぶCrateを連続収集
            while (true)
            {
                Crate found = FindCrateInStage(targetStage, scanPos);
                if (found == null) break;
                chainCrates.Add(found);
                chainSourcePositions.Add(scanPos);
                scanPos += dirVec;
            }

            if (chainCrates.Count == 0) return;

            // 連鎖の末尾の次のマスが空きであることを確認
            Vector2Int finalDest = entryPos + dirVec * chainCrates.Count;
            bool finalOk = IsInBounds(targetStage, finalDest)
                        && gm.IsWalkableAt(targetStage, finalDest)
                        && gm.GetEnemyAt(targetStage, finalDest) == null
                        && FindCrateInStage(targetStage, finalDest) == null;

            if (!finalOk)
            {
                // 押し出せない → コマンド自体を無効化して入ってくるCrateは境界を越えない
                chainCrates.Clear();
                chainSourcePositions.Clear();
                IsValid = false;
                return;
            }

            // 各Crateの移動先を計算（着地マスから1マスずつずれる）
            for (int i = 0; i < chainCrates.Count; i++)
            {
                chainTargetPositions.Add(entryPos + dirVec * (i + 1));
            }
        }

        private Crate FindCrateInStage(StageSO stage, Vector2Int pos)
        {
            if (!gm.StageCratesMap.TryGetValue(stage, out var crates)) return null;
            foreach (var c in crates)
            {
                if (c != null && !c.IsDead && c.CurrentPos == pos) return c;
            }
            return null;
        }

        private static bool IsInBounds(StageSO stage, Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < stage.Width
                && pos.y >= 0 && pos.y < stage.Height;
        }

        public void Execute()
        {
            if (targetCrate == null) return;

            // 1. 着地先にいる連鎖Crateを奥から順に1マス押し出す
            if (chainCrates != null && chainCrates.Count > 0)
            {
                for (int i = chainCrates.Count - 1; i >= 0; i--)
                {
                    chainCrates[i].WarpTo(chainTargetPositions[i], targetDir);
                }
            }

            // 2. 元のステージから登録解除
            if (gm.StageCratesMap.ContainsKey(sourceStage)) gm.StageCratesMap[sourceStage].Remove(targetCrate);
            gm.UnitManager.RemoveCrate(targetCrate);

            // 3. 隣接ステージの対応マスへ配置
            targetCrate.WarpTo(targetPos, targetDir);

            if (!gm.StageCratesMap.ContainsKey(targetStage)) gm.StageCratesMap[targetStage] = new List<Crate>();
            gm.StageCratesMap[targetStage].Add(targetCrate);

            // 現在のステージが遷移先なら表示、そうでなければ非表示
            if (targetStage == gm.CurrentStageSOInternal)
            {
                targetCrate.gameObject.SetActive(true);
                targetCrate.transform.SetParent(gm.Visualizer.GridParent);
                gm.UnitManager.AddCrate(targetCrate);
            }
            else
            {
                targetCrate.gameObject.SetActive(false);
                targetCrate.transform.SetParent(gm.transform);
            }
        }

        public void Undo() { UndoAsync().Forget(); }
        public async UniTask UndoAsync()
        {
            if (targetCrate == null) return;

            // 1. 遷移先から入ってきたCrateを解除
            if (gm.StageCratesMap.ContainsKey(targetStage)) gm.StageCratesMap[targetStage].Remove(targetCrate);
            if (targetStage == gm.CurrentStageSOInternal)
            {
                gm.UnitManager.RemoveCrate(targetCrate);
            }

            // 2. 元の位置に戻す
            targetCrate.WarpTo(sourcePos, sourceDir);

            if (sourceStage == gm.CurrentStageSOInternal)
            {
                targetCrate.gameObject.SetActive(true);
                targetCrate.transform.SetParent(gm.Visualizer.GridParent);
                gm.UnitManager.AddCrate(targetCrate);
            }
            else
            {
                targetCrate.gameObject.SetActive(false);
                targetCrate.transform.SetParent(gm.transform);
            }

            if (!gm.StageCratesMap.ContainsKey(sourceStage)) gm.StageCratesMap[sourceStage] = new List<Crate>();
            gm.StageCratesMap[sourceStage].Add(targetCrate);

            // 3. 連鎖Crateを元の位置に戻す（手前から順に）
            if (chainCrates != null && chainCrates.Count > 0)
            {
                for (int i = 0; i < chainCrates.Count; i++)
                {
                    chainCrates[i].WarpTo(chainSourcePositions[i], targetDir);
                }
            }

            await UniTask.Yield();
        }
    }
}
