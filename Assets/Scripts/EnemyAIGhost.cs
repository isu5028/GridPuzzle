using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Ghost AI: プレイヤーから3マス以内にいるとき、
/// BFS距離が最大の隣接マス（プレイヤーから最も遠い方向）に移動する。
/// 同じ優先度のマスが複数ある場合は Utility.SearchOrder 順（Right > Left > Up > Down）で選択する。
/// </summary>
public class EnemyAIGhost : EnemyAI
{
    /// <summary>Ghostが逃げ始める距離の閾値（マンハッタン距離）</summary>
    private const int FleeRange = 2;

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;

        Unit player = gridManager.PlayerScript;
        if (player == null) return;

        // プレイヤーが自分と同じステージにいるか確認
        StageSO currentStage = gridManager.GetStageOfEnemy(enemy);
        StageSO playerStage = gridManager.CurrentStageSO; // PlayerScript.CurrentPos は常に currentStage 内の座標

        // 違うステージにいる場合は逃げる必要がない（ただし例外的にプレイヤーがロード中などの場合は無視）
        if (currentStage != playerStage)
        {
            return;
        }

        Vector2Int playerPos = player.CurrentPos;
        int distToPlayer = GetManhattanDistance(currentPos, playerPos);

        // --- 押し出し判定 ---
        // 同じステージの同じマスにいる場合（プレイヤーが移動してきて重なった場合）、
        // 距離判定を無視して即座に隣接マスへ逃げる。
        bool isOverlapping = (currentPos == playerPos);

        // プレイヤーが FleeRange マス以内にいない、かつ重なってもいない場合は何もしない
        if (distToPlayer > FleeRange && !isOverlapping)
        {
            return;
        }

        // BFS で現在のマスからプレイヤーまでの距離を取得
        // 重なっている場合は距離0として扱う
        int currentDist = isOverlapping ? 0 : BfsDistanceFromPlayer(currentPos, playerPos);

        // 移動候補マスを Utility.SearchOrder 順で列挙
        // 重なっている場合は強制移動、そうでなければ現在より遠いマスが見つかった場合のみ更新する
        Vector2Int bestPos = currentPos;
        int bestDist = currentDist;
        Direction bestDir = currentDirection;

        foreach (Direction dir in Utility.SearchOrder)
        {
            Vector2Int candidatePos = currentPos + Utility.GetVectorFromDirection(dir);

            // ステージ内移動、または境界を越えた隣接ステージへの移動が可能かチェック
            bool isBoundaryMove = IsValidBoundaryMove(enemy, candidatePos, dir);
            if (!isBoundaryMove && !IsCandidateWalkable(candidatePos)) continue;

            // プレイヤーの移動先（現在位置）へは逃げないようにする
            if (candidatePos == playerPos) continue;

            int bfsDist;
            if (isBoundaryMove)
            {
                bfsDist = int.MaxValue - 1;
            }
            else
            {
                bfsDist = BfsDistanceFromPlayer(candidatePos, playerPos);
            }

            // 重なっている場合は「現在地(0)」より遠いマスがあれば必ず更新される。
            // 重なっていない場合は「現在地」より遠いマスがある場合のみ更新される。
            if (bfsDist > bestDist)
            {
                bestDist = bfsDist;
                bestPos = candidatePos;
                bestDir = dir;
            }
        }

        // 移動先が見つかった場合のみ移動
        if (bestPos != currentPos)
        {
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, bestPos, currentDirection, bestDir);
            commandManager.ExecuteCommand(moveCommand);
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);

            // --- 境界遷移／階段遷移チェック ---

            // 1. 境界遷移（ステージ端を越えて隣接ステージへ）
            if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy)) return;

            // 2. 階段遷移
            var transition = gridManager.GetEnemyStairDestination(enemy, bestPos, bestDir);
            if (transition != null)
            {
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, bestDir);
            }
        }
        else
        {
            // 逃げ場がない、または現在地が最善の場合は、隣接するユニットを攻撃
            bool attacked = false;
            foreach (var dir in Utility.SearchOrder)
            {
                Vector2Int attackTargetPos = currentPos + Utility.GetVectorFromDirection(dir);
                Unit targetUnit = gridManager.GetUnitAtPosition(attackTargetPos);

                // 隣接マスに何らかのユニットがいる場合、その方向を向いて攻撃
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
                // 攻撃対象もいない場合は待機
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(100);
            }
        }
    }

    /// <summary>
    /// 指定座標が Ghost の移動先として有効か判定する。
    /// 地形が通行可能 かつ 他のユニット（敵含む）が存在しないことが条件。
    /// </summary>
    private bool IsCandidateWalkable(Vector2Int pos)
    {
        if (!gridManager.IsCellWalkable(pos)) return false;
        return true;
    }

    /// <summary>
    /// BFS で playerPos から target までの最短歩数を返す。
    /// 壁・他敵の占有は無視してマップの地形のみで計算する（プレイヤーが来られる最短距離）。
    /// 到達不能の場合は int.MaxValue を返す。
    /// </summary>
    private int BfsDistanceFromPlayer(Vector2Int target, Vector2Int playerPos)
    {
        if (target == playerPos) return 0;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> visited = new Dictionary<Vector2Int, int>();

        queue.Enqueue(playerPos);
        visited[playerPos] = 0;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDist = visited[current];

            foreach (Direction dir in Utility.SearchOrder)
            {
                Vector2Int next = current + Utility.GetVectorFromDirection(dir);

                if (visited.ContainsKey(next)) continue;

                // 地形のみで通行可否を判定（他ユニットの存在は無視）
                if (!gridManager.IsWalkableAt(gridManager.CurrentStageSO, next)) continue;

                int nextDist = currentDist + 1;
                visited[next] = nextDist;

                if (next == target) return nextDist;

                queue.Enqueue(next);
            }
        }

        // 到達不能 → 非常に遠いとみなす
        return int.MaxValue;
    }

    private int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    /// <summary>
    /// ステージ境界を越えた移動が有効か判定する（EnemyAIPatrol と同様のロジック）
    /// </summary>
    private bool IsValidBoundaryMove(Enemy enemy, Vector2Int targetPos, Direction dir)
    {
        StageSO currentStage = gridManager.GetStageOfEnemy(enemy);
        if (currentStage == null) return false;

        // ターゲットが現在のステージ内であれば境界移動ではない
        if (targetPos.x >= 0 && targetPos.x < currentStage.Width && targetPos.y >= 0 && targetPos.y < currentStage.Height)
        {
            return false;
        }

        // 隣接ステージを取得
        StageSO neighbor = currentStage.GetAdjacentStage(dir);
        if (neighbor == null) return false;

        // 隣接ステージでの進入位置を計算
        Vector2Int mappedPos = Vector2Int.zero;
        if (dir == Direction.Left) mappedPos = new Vector2Int(neighbor.Width - 1, targetPos.y);
        else if (dir == Direction.Right) mappedPos = new Vector2Int(0, targetPos.y);
        else if (dir == Direction.Up) mappedPos = new Vector2Int(targetPos.x, 0);
        else if (dir == Direction.Down) mappedPos = new Vector2Int(targetPos.x, neighbor.Height - 1);

        // 隣接ステージの進入先が通行可能かチェック
        // 地形だけでなく、既にユニット（他ステージの敵など）がいないかも確認する
        CellType originalType = neighbor.GetWallAt(mappedPos.x, mappedPos.y);
        bool isTerrainWalkable = gridManager.IsWalkableAt(neighbor, mappedPos);

        // ユニットの存在チェック（隣接ステージの敵管理システムから取得）
        Unit occupant = gridManager.StageEnemySystem.GetEnemyAt(neighbor, mappedPos);
        if (occupant == null)
        {
            // プレイヤーが隣接ステージにいる可能性も考慮（稀なケースだが）
            if (gridManager.PlayerScript != null && gridManager.CurrentStageSO == neighbor)
            {
                if (gridManager.PlayerScript.CurrentPos == mappedPos) occupant = gridManager.PlayerScript;
            }
        }

        // 通行可能条件:
        // 1. 地形が通行可能である
        // 2. ユニットによって塞がれていない
        // 3. 元々壁だった場所が壊れて道になっている（他の敵の挙動に合わせる）
        bool isOccupied = occupant != null;
        return (originalType == CellType.Wall && isTerrainWalkable && !isOccupied);
    }
}
