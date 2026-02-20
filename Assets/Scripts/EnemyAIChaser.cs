using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIChaser : EnemyAI
{


    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;

        // プレイヤーの位置を取得
        Unit player = gridManager.PlayerScript;
        if (player == null)
        {
            return;
        }

        Vector2Int playerPos = player.CurrentPos;

        // 1. 攻撃範囲にプレイヤーがいるか確認
        if (IsPlayerInAttackRange(enemy, playerPos))
        {
            Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
            if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // 2. A* アルゴリズムで経路探索
        // isTraversable条件: マップ内通行可能 かつ 他の敵がいない
        List<Vector2Int> path = AStarPathfinder.FindPath(currentPos, playerPos, (pos) =>
        {
            if (!gridManager.IsCellWalkable(pos)) return false;
            // 他の敵がいる場合は障害物とみなす
            // IsEnemyAtPositionはPlayerに対してfalseを返すので、ターゲット（Player）地点は通行可能と判定される
            if (IsEnemyAtPosition(pos)) return false;
            return true;
        });

        // 3. 移動実行
        if (path != null && path.Count > 0)
        {
            Vector2Int nextPos = path[0]; // 次に移動すべきマス
            Direction moveDirection = GetDirectionToTarget(currentPos, nextPos);

            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, nextPos, currentDirection, moveDirection);
            commandManager.ExecuteCommand(moveCommand);
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);

            // 階段遷移チェック
            var transition = gridManager.GetEnemyStairDestination(enemy, nextPos, moveDirection);
            if (transition != null)
            {
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, moveDirection);
            }
        }
        else
        {
            // 経路探索失敗時、足元が階段ならワープを試行
            var transition = gridManager.GetEnemyStairDestination(enemy, currentPos, currentDirection);
            if (transition != null)
            {
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, currentDirection);
                return;
            }
            // 移動できない場合（経路がない、囲まれているなど）は待機または空振り
            // プレイヤーの方を向いて威嚇（攻撃モーション）
            Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
            if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
    }

    private bool IsPlayerInAttackRange(Enemy enemy, Vector2Int playerPos)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction[] directions = new Direction[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        foreach (var dir in directions)
        {
            Vector2Int checkPos = currentPos + Utility.GetVectorFromDirection(dir);
            if (checkPos == playerPos)
                return true;
        }
        return false;
    }

    private Direction GetDirectionToTarget(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            return diff.x > 0 ? Direction.Right : Direction.Left;
        else
            return diff.y > 0 ? Direction.Up : Direction.Down;
    }

    private bool IsEnemyAtPosition(Vector2Int pos)
    {
        Unit unit = gridManager.GetUnitAtPosition(pos);
        return unit != null && unit is Enemy;
    }
}