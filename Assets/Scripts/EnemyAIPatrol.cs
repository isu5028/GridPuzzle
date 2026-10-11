using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
public class EnemyAIPatrol : EnemyAI
{
    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;
        Vector2Int directionalVector = Utility.GetVectorFromDirection(currentDirection);
        Vector2Int frontPos = currentPos + directionalVector;

        // 前方のユニットを確認
        Unit frontUnit = gridManager.GetUnitAtPosition(frontPos);

        // --- 木箱の押し出し判定 (最大2つ) ---
        if (frontUnit != null && frontUnit is Crate firstCrate)
        {
            Vector2Int pos2 = frontPos + directionalVector;
            Unit unit2 = gridManager.GetUnitAtPosition(pos2);

            if (unit2 != null && unit2 is Crate secondCrate)
            {
                // 2つ連続している場合、3つ目のマスが空いているかチェック
                Vector2Int pos3 = pos2 + directionalVector;
                if (gridManager.IsCellWalkable(pos3))
                {
                    // 遠い方から順番に移動コマンドを発行
                    commandManager.ExecuteCommand(new MoveCommand(secondCrate, pos2, pos3, secondCrate.CurrentDirection, currentDirection, false));
                    commandManager.ExecuteCommand(new MoveCommand(firstCrate, frontPos, pos2, firstCrate.CurrentDirection, currentDirection, false));
                    commandManager.ExecuteCommand(new MoveCommand(enemy, currentPos, frontPos, currentDirection, currentDirection));

                    if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                    await CheckTransitions(enemy, frontPos, currentDirection);
                    return;
                }
            }
            else if (gridManager.IsCellWalkable(pos2))
            {
                // 1つだけの場合
                commandManager.ExecuteCommand(new MoveCommand(firstCrate, frontPos, pos2, firstCrate.CurrentDirection, currentDirection, false));
                commandManager.ExecuteCommand(new MoveCommand(enemy, currentPos, frontPos, currentDirection, currentDirection));

                if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                await CheckTransitions(enemy, frontPos, currentDirection);
                return;
            }
        }
        // 反撃（通常攻撃）
        else if (frontUnit != null)
        {
            if (GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // 通常移動 (前方)
        bool canMoveForward = gridManager.IsCellWalkable(frontPos) || IsValidBoundaryMove(enemy, frontPos, currentDirection);
        if (canMoveForward && frontUnit == null)
        {
            commandManager.ExecuteCommand(new MoveCommand(enemy, currentPos, frontPos, currentDirection, currentDirection));
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
            if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy)) return;
            await CheckTransitions(enemy, frontPos, currentDirection);
        }
        else
        {
            // --- 反転ロジック ---
            Direction oppositeDirection = GetOppositeDirection(currentDirection);
            Vector2Int oppositeVector = Utility.GetVectorFromDirection(oppositeDirection);
            Vector2Int backPos = currentPos + oppositeVector;
            Unit backUnit = gridManager.GetUnitAtPosition(backPos);

            if (backUnit != null && backUnit is Crate backFirstCrate)
            {
                Vector2Int bPos2 = backPos + oppositeVector;
                Unit bUnit2 = gridManager.GetUnitAtPosition(bPos2);

                if (bUnit2 != null && bUnit2 is Crate backSecondCrate)
                {
                    Vector2Int bPos3 = bPos2 + oppositeVector;
                    if (gridManager.IsCellWalkable(bPos3))
                    {
                        commandManager.ExecuteCommand(new MoveCommand(backSecondCrate, bPos2, bPos3, backSecondCrate.CurrentDirection, oppositeDirection, false));
                        commandManager.ExecuteCommand(new MoveCommand(backFirstCrate, backPos, bPos2, backFirstCrate.CurrentDirection, oppositeDirection, false));
                        commandManager.ExecuteCommand(new MoveCommand(enemy, currentPos, backPos, currentDirection, oppositeDirection));

                        if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                        await CheckTransitions(enemy, backPos, oppositeDirection);
                        return;
                    }
                }
                else if (gridManager.IsCellWalkable(bPos2))
                {
                    commandManager.ExecuteCommand(new MoveCommand(backFirstCrate, backPos, bPos2, backFirstCrate.CurrentDirection, oppositeDirection, false));
                    commandManager.ExecuteCommand(new MoveCommand(enemy, currentPos, backPos, currentDirection, oppositeDirection));

                    if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                    await CheckTransitions(enemy, backPos, oppositeDirection);
                    return;
                }
            }
            else if (backUnit != null)
            {
                if (GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
                enemy.SetDirection(oppositeDirection);
                enemy.TryAttack();
                await UniTask.WaitUntil(() => !enemy.IsMoving);
                return;
            }

            bool canMoveBackward = gridManager.IsCellWalkable(backPos) || IsValidBoundaryMove(enemy, backPos, oppositeDirection);
            Vector2Int targetPos = canMoveBackward ? backPos : currentPos;
            commandManager.ExecuteCommand(new MoveCommand(enemy, currentPos, targetPos, currentDirection, oppositeDirection));
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
            if (canMoveBackward)
            {
                if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy)) return;
                await CheckTransitions(enemy, backPos, oppositeDirection);
            }
        }
    }

    /// <summary>
    /// 移動後の遷移（階段等）をチェックするヘルパー
    /// </summary>
    private async UniTask CheckTransitions(Enemy enemy, Vector2Int pos, Direction dir)
    {
        var transition = gridManager.GetEnemyStairDestination(enemy, pos, dir);
        if (transition != null)
        {
            if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
            await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, dir);
        }
    }

    private bool IsValidBoundaryMove(Enemy enemy, Vector2Int targetPos, Direction dir)
    {
        StageSO currentStage = gridManager.GetStageOfEnemy(enemy);
        if (currentStage == null) return false;

        // Check if targetPos is OOB for current stage
        if (targetPos.x >= 0 && targetPos.x < currentStage.Width && targetPos.y >= 0 && targetPos.y < currentStage.Height)
        {
            return false; // Not a boundary move (it's in-bounds, so walkability checked elsewhere)
        }

        // Get Neighbor
        StageSO neighbor = currentStage.GetAdjacentStage(dir);
        if (neighbor == null) return false;

        // Calculate mapped position
        Vector2Int mappedPos = Vector2Int.zero;
        if (dir == Direction.Left) mappedPos = new Vector2Int(neighbor.Width - 1, targetPos.y);
        else if (dir == Direction.Right) mappedPos = new Vector2Int(0, targetPos.y);
        else if (dir == Direction.Up) mappedPos = new Vector2Int(targetPos.x, 0); // Assuming Up is +y? Unity grid often Y-up. But wait, adjacent Up means y < 0 relative to visual?
        // Let's rely on standard mapping:
        // If moving UP (y decreasing?), neighbor is at Top.
        // Wait, `GetAdjacentStage` keys: Up, Down, Left, Right.
        // If I move UP (y++), I hit y=Height. Neighbor is Up.
        // Mapped pos in neighbor is y=0.
        else if (dir == Direction.Down) mappedPos = new Vector2Int(targetPos.x, neighbor.Height - 1);

        // Correcting UP Logic based on standard 2D grid:
        // Position (0,0) is bottom-left usually.
        // Up is +Y. Top edge is y=Height-1. Moving Up goes to y=Height.
        // Neighbor Up entry is y=0.
        if (dir == Direction.Up) mappedPos = new Vector2Int(targetPos.x, 0);
        // Down is -Y. Bottom edge is y=0. Moving Down goes to y=-1.
        // Neighbor Down entry is y=Height-1.
        if (dir == Direction.Down) mappedPos = new Vector2Int(targetPos.x, neighbor.Height - 1);

        // Check Walkability in Neighbor
        // User Requirement: Only cross if it was a Wall that is now Broken (Walkable).
        // If it is naturally Floor (Open path), Patrol should turn back.

        CellType originalType = neighbor.GetWallAt(mappedPos.x, mappedPos.y);
        bool isCurrentlyWalkable = gridManager.IsWalkableAt(neighbor, mappedPos);

        if (originalType == CellType.Wall && isCurrentlyWalkable)
        {
            return true; // It was a wall, but now broken -> Cross
        }

        return false; // Naturally Floor or Obstacle -> Turn Back
    }


    private Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => dir
        };
    }
}