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
            // プレイヤー自身の位置はパス探索の最終目的地なので、地形だけを確認する
            if (pos == playerPos)
            {
                return gridManager.IsWalkableAt(gridManager.CurrentStageSO, pos);
            }

            if (!gridManager.IsCellWalkable(pos)) return false;
            // IsCellWalkableでユニット判定はされているが念のため
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
            // 移動できない場合（経路がない、囲まれているなど）
            // 攻撃できるユニットがいれば攻撃し、いなければ何もしない
            bool attacked = false;
            foreach (var dir in Utility.SearchOrder)
            {
                Vector2Int attackTargetPos = currentPos + Utility.GetVectorFromDirection(dir);
                Unit targetUnit = gridManager.GetUnitAtPosition(attackTargetPos);

                // もし何かしらのユニット（他の敵や壁などは関係なくUnitであれば）がいれば攻撃
                if (targetUnit != null)
                {
                    if (dir != currentDirection) enemy.SetDirection(dir);
                    if (GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
                    enemy.TryAttack();
                    await UniTask.WaitUntil(() => !enemy.IsMoving);
                    attacked = true;
                    break;
                }
            }

            if (!attacked)
            {
                // 攻撃対象がいない場合は何もしない（待機）
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(100);
            }
        }
    }

    private bool IsPlayerInAttackRange(Enemy enemy, Vector2Int playerPos)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        foreach (var dir in Utility.SearchOrder)
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