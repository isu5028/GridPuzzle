using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIGoblin : EnemyAI
{
    private bool isInitialized = false;
    private Vector2Int initialPos;
    private List<Unit> targetQueue = new List<Unit>();

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        EnsureInitialized(enemy);

        if (enemy.IsDead) return;

        // Goblinは1ターンに2回行動する
        for (int i = 0; i < 2; i++)
        {
            if (enemy.IsDead) break;
            
            UpdateTargetQueue(enemy); // アクション間にターゲットが死んだ場合などを考慮して更新

            Vector2Int currentPos = enemy.CurrentPos;
            Direction currentDirection = enemy.CurrentDirection;

            if (targetQueue.Count > 0)
            {
                Unit target = targetQueue[0];

                // 1. 攻撃範囲にターゲットがいるか確認
                if (IsTargetInAttackRange(currentPos, target.CurrentPos))
                {
                    Direction attackDirection = GetDirectionToTarget(currentPos, target.CurrentPos);
                    if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
                    if (GameSettings.GameSpeed == 1) await UniTask.Delay(100);
                    enemy.TryAttack();
                    await UniTask.WaitUntil(() => !enemy.IsMoving && !enemy.IsAnimating);
                    continue; // 攻撃したら次の行動へ
                }

                // 2. A* アルゴリズムでターゲットに向かって経路探索
                List<Vector2Int> path = AStarPathfinder.FindPath(currentPos, target.CurrentPos, (pos) =>
                {
                    if (pos == target.CurrentPos) return gridManager.IsWalkableAt(gridManager.CurrentStageSO, pos);
                    if (!gridManager.IsCellWalkable(pos)) return false;
                    Unit u = gridManager.GetUnitAtPosition(pos);
                    if (u != null && u != target) return false;
                    return true;
                });

                // 3. 移動実行
                if (path != null && path.Count > 0)
                {
                    Vector2Int nextPos = path[0];
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
                        continue;
                    }

                    // 移動できない場合、隣接しているターゲット以外のCrate以外のユニットがいれば攻撃
                    bool attacked = await TryAttackAdjacent(enemy, currentPos, currentDirection);

                    if (!attacked)
                    {
                        if (GameSettings.GameSpeed == 0) await UniTask.Delay(100);
                    }
                }
            }
            else
            {
                // ターゲットがいない場合、他にすることがなければ隣接ユニット（Crate以外）を攻撃
                bool attackedAdjacent = await TryAttackAdjacent(enemy, currentPos, currentDirection);

                if (!attackedAdjacent)
                {
                    break; // 隣接ユニットもいなければ行動終了（初期地点にも戻らない）
                }
            }
        }
    }

    private async UniTask<bool> TryAttackAdjacent(Enemy enemy, Vector2Int currentPos, Direction currentDirection)
    {
        foreach (var dir in Utility.SearchOrder)
        {
            Vector2Int attackTargetPos = currentPos + Utility.GetVectorFromDirection(dir);
            Unit targetUnit = gridManager.GetUnitAtPosition(attackTargetPos);

            if (targetUnit != null && !(targetUnit is Crate) && targetUnit != enemy && !targetUnit.IsDead)
            {
                if (dir != currentDirection) enemy.SetDirection(dir);
                if (GameSettings.GameSpeed == 1) await UniTask.Delay(100);
                enemy.TryAttack();
                await UniTask.WaitUntil(() => !enemy.IsMoving && !enemy.IsAnimating);
                return true;
            }
        }
        return false;
    }

    private void EnsureInitialized(Enemy enemy)
    {
        if (isInitialized) return;

        StageSO stage = gridManager.GetStageOfEnemy(enemy);
        if (stage != null && !string.IsNullOrEmpty(enemy.EnemyID))
        {
            string[] parts = enemy.EnemyID.Split('_');
            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int index))
            {
                if (index >= 0 && index < stage.EnemyStartData.Count)
                {
                    initialPos = stage.EnemyStartData[index].Position;
                }
                else initialPos = enemy.CurrentPos;
            }
            else initialPos = enemy.CurrentPos;
        }
        else initialPos = enemy.CurrentPos;

        isInitialized = true;
    }

    private void UpdateTargetQueue(Enemy enemy)
    {
        List<Unit> currentInTerritory = new List<Unit>();

        // 5x5マスの縄張りをチェック
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                Vector2Int pos = initialPos + new Vector2Int(x, y);
                Unit unit = gridManager.GetUnitAtPosition(pos);
                
                // Crate以外のユニット（Playerや他のEnemyなど）をターゲットとして認識
                if (unit != null && !unit.IsDead && !(unit is Crate) && unit != enemy)
                {
                    currentInTerritory.Add(unit);
                }
            }
        }

        // 縄張りから離れた、または死んだユニットをリストから削除
        targetQueue.RemoveAll(u => !currentInTerritory.Contains(u) || u.IsDead);

        // 新しく侵入したユニットをリストの最後に追加（これにより侵入順が維持される）
        foreach (var u in currentInTerritory)
        {
            if (!targetQueue.Contains(u))
            {
                targetQueue.Add(u);
            }
        }
    }

    private bool IsTargetInAttackRange(Vector2Int currentPos, Vector2Int targetPos)
    {
        Vector2Int diff = targetPos - currentPos;
        return Mathf.Abs(diff.x) + Mathf.Abs(diff.y) == 1;
    }

    private Direction GetDirectionToTarget(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            return diff.x > 0 ? Direction.Right : Direction.Left;
        else
            return diff.y > 0 ? Direction.Up : Direction.Down;
    }
}
