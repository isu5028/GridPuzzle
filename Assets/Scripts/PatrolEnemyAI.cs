using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
public class PatrolEnemyAI : EnemyAI
{
    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int  currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;
        Vector2Int directionalVector = Utility.GetVectorFromDirection(currentDirection);
        Vector2Int frontPos = currentPos + directionalVector;

        // 前方に攻撃対象がいるか確認
        Unit frontUnit = gridManager.GetUnitAtPosition(frontPos);
        if(frontUnit != null)
        {
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // 前方に移動可能か確認
        if(gridManager.IsCellWalkable(frontPos))
        {
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, frontPos, currentDirection, currentDirection);
            commandManager.ExecuteCommand(moveCommand);

            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
        else
        {
            // 壁がある場合は方向転換
            Direction oppositeDirection = GetOppositeDirection(currentDirection);
            Vector2Int oppositeVector = Utility.GetVectorFromDirection(oppositeDirection);

            // 反対側に攻撃対象がいるか確認
            Vector2Int backPos = currentPos + oppositeVector;
            Unit backUnit = gridManager.GetUnitAtPosition(backPos);
            if(backUnit != null)
            {
                if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
                enemy.SetDirection(oppositeDirection);
                enemy.TryAttack();
                await UniTask.WaitUntil(() => !enemy.IsMoving);
                return;
            }

            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, backPos, currentDirection, oppositeDirection);
            commandManager.ExecuteCommand(moveCommand);
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
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