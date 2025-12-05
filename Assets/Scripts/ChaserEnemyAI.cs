using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
public class ChaserEnemyAI : EnemyAI
{
    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;

        // プレイヤーの位置を取得
        Unit player = gridManager.PlayerScript;
        if (player == null)
        {
            Debug.LogWarning("プレイヤーが見つかりません");
            return;
        }

        Vector2Int playerPos = player.CurrentPos;

        // 攻撃範囲にプレイヤーがいるか確認
        if (IsPlayerInAttackRange(enemy, playerPos))
        {
            // プレイヤーの方向を向いて攻撃
            Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
            if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // まずX座標を合わせる
        if (currentPos.x != playerPos.x)
        {
            Direction xDirection = currentPos.x < playerPos.x ? Direction.Right : Direction.Left;
            Vector2Int xTargetPos = currentPos + Utility.GetVectorFromDirection(xDirection);

            if (gridManager.IsCellWalkable(xTargetPos) && !IsEnemyAtPosition(xTargetPos))
            {
                // X方向に移動可能
                MoveCommand moveCommand = new MoveCommand(enemy, currentPos, xTargetPos, currentDirection, xDirection);
                commandManager.ExecuteCommand(moveCommand);
                if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                return;
            }
            else
            {
                // X方向に移動できない場合、Y座標を合わせに行く
                if (currentPos.y != playerPos.y)
                {
                    Direction yDirection = currentPos.y < playerPos.y ? Direction.Up : Direction.Down;
                    Vector2Int yTargetPos = currentPos + Utility.GetVectorFromDirection(yDirection);

                    if (gridManager.IsCellWalkable(yTargetPos) && !IsEnemyAtPosition(yTargetPos))
                    {
                        // Y方向に移動可能
                        MoveCommand moveCommand = new MoveCommand(enemy, currentPos, yTargetPos, currentDirection, yDirection);
                        commandManager.ExecuteCommand(moveCommand);
                        if(GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                        return;
                    }
                }

                // どちらにも移動できない場合は空振り攻撃
                Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
                if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
                if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
                enemy.TryAttack();
                await UniTask.WaitUntil(() => !enemy.IsMoving);
                return;
            }
        }

        // X座標が同じ場合、Y座標を合わせる
        if (currentPos.y != playerPos.y)
        {
            Direction yDirection = currentPos.y < playerPos.y ? Direction.Up : Direction.Down;
            Vector2Int yTargetPos = currentPos + Utility.GetVectorFromDirection(yDirection);

            if (gridManager.IsCellWalkable(yTargetPos) && !IsEnemyAtPosition(yTargetPos))
            {
                // Y方向に移動可能
                MoveCommand moveCommand = new MoveCommand(enemy, currentPos, yTargetPos, currentDirection, yDirection);
                commandManager.ExecuteCommand(moveCommand);
                if(GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
                return;
            }
            else
            {
                // Y方向にも移動できない場合は空振り攻撃
                Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
                if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
                if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
                enemy.TryAttack();
                await UniTask.WaitUntil(() => !enemy.IsMoving);
                return;
            }
        }

        // プレイヤーと同じ位置（通常ありえない）
        enemy.TryAttack();
        await UniTask.WaitUntil(() => !enemy.IsMoving);
    }

    private bool IsPlayerInAttackRange(Enemy enemy, Vector2Int playerPos)
    {
        // 敵の攻撃範囲を取得（Unitクラスで定義されているattackTargetPosを使用）
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;

        // 4方向チェック（攻撃範囲が隣接マスの場合）
        Direction[] directions = new Direction[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        foreach (var dir in directions)
        {
            Vector2Int checkPos = currentPos + Utility.GetVectorFromDirection(dir);
            if (checkPos == playerPos)
            {
                return true;
            }
        }
        return false;
    }

    private Direction GetDirectionToTarget(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;

        // X方向の差が大きい場合
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
        {
            return diff.x > 0 ? Direction.Right : Direction.Left;
        }
        // Y方向の差が大きい場合
        else
        {
            return diff.y > 0 ? Direction.Up : Direction.Down;
        }
    }

    private bool IsEnemyAtPosition(Vector2Int pos)
    {
        Unit unit = gridManager.GetUnitAtPosition(pos);
        return unit != null && unit is Enemy;
    }
}