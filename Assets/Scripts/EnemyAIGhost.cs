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
    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Direction actionDir = turnManager.LastPlayerActionDirection;
        PlayerActionType actionType = turnManager.LastPlayerAction;

        if (actionType == PlayerActionType.Attack)
        {
            // 攻撃を模倣
            if (enemy.CurrentDirection != actionDir) enemy.SetDirection(actionDir);

            if (GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // 移動を模倣
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDir = enemy.CurrentDirection;
        StageSO enemyStage = gridManager.GetStageOfEnemy(enemy);
        if (enemyStage == null) return;

        // 次の座標を計算
        Vector2Int directionVector = Utility.GetVectorFromDirection(actionDir);
        Vector2Int targetPos = currentPos + directionVector;
        Unit targetOccupant = gridManager.GetUnitAtPosition(targetPos);

        // プレイヤーが移動入力でも、Ghost前方にUnitがいるなら攻撃を優先
        // （Crateは押し出し対象なのでここでは除外）
        if (actionType == PlayerActionType.Move && targetOccupant != null && !(targetOccupant is Crate))
        {
            if (enemy.CurrentDirection != actionDir) enemy.SetDirection(actionDir);

            if (GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // ステージ内移動、または境界を越えた隣接ステージへの移動が可能かチェック
        bool isBoundaryMove = IsValidBoundaryMove(enemy, targetPos, actionDir);
        bool isWalkable = gridManager.IsWalkableAt(enemyStage, targetPos) && targetOccupant == null;
        bool didMove = false;

        if (targetOccupant is Crate)
        {
            didMove = TryPushCratesAndMove(enemy, enemyStage, currentPos, currentDir, actionDir, directionVector, targetPos);
        }
        else if (isBoundaryMove || isWalkable)
        {
            // 移動実行
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, targetPos, currentDir, actionDir);
            commandManager.ExecuteCommand(moveCommand);
            didMove = true;
        }

        if (didMove)
        {
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);

            // --- 境界遷移／階段遷移チェック ---

            // 1. 境界遷移（ステージ端を越えて隣接ステージへ）
            if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy)) return;

            // 2. 階段遷移
            var transition = gridManager.GetEnemyStairDestination(enemy, targetPos, actionDir);
            if (transition != null)
            {
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, actionDir);
            }
        }
        else
        {
            // 移動できない場合は向きだけ変える
            if (currentDir != actionDir)
            {
                enemy.SetDirection(actionDir);
            }
            if (GameSettings.GameSpeed == 0) await UniTask.Delay(100);
        }
    }

    private bool TryPushCratesAndMove(
        Enemy enemy,
        StageSO enemyStage,
        Vector2Int currentPos,
        Direction currentDir,
        Direction actionDir,
        Vector2Int directionVector,
        Vector2Int targetPos)
    {
        List<Crate> cratesToPush = new List<Crate>();
        Vector2Int checkPos = targetPos;

        while (gridManager.GetUnitAtPosition(checkPos) is Crate nextCrate)
        {
            cratesToPush.Add(nextCrate);
            checkPos += directionVector;
        }

        bool canPush = false;

        if (gridManager.IsWalkableAt(enemyStage, checkPos) && gridManager.GetUnitAtPosition(checkPos) == null)
        {
            canPush = true;
        }
        else if (!IsInBounds(enemyStage, checkPos))
        {
            // OOBへの押し出しは、現在ステージ側の境界壁が破壊されている場合のみ許可
            if (gridManager.IsWalkableAt(enemyStage, checkPos))
            {
                StageSO neighbor = gridManager.GetAdjacentStageFromBoundary(enemyStage, checkPos);
                if (neighbor != null)
                {
                    Vector2Int neighborEntry = GridCalculation.CalculateEntryPosition(neighbor, actionDir, checkPos);
                    if (gridManager.IsWalkableAt(neighbor, neighborEntry) && gridManager.GetUnitAtPosition(neighborEntry) == null)
                    {
                        canPush = true;
                    }
                }
            }
        }

        if (!canPush)
        {
            return false;
        }

        for (int i = cratesToPush.Count - 1; i >= 0; i--)
        {
            Vector2Int crateCurrentPos = targetPos + directionVector * i;
            Vector2Int crateTargetPos = crateCurrentPos + directionVector;
            MoveCommand pushCommand = new MoveCommand(cratesToPush[i], crateCurrentPos, crateTargetPos, cratesToPush[i].CurrentDirection, actionDir, false);
            commandManager.ExecuteCommand(pushCommand);
        }

        MoveCommand selfMoveCommand = new MoveCommand(enemy, currentPos, targetPos, currentDir, actionDir);
        commandManager.ExecuteCommand(selfMoveCommand);
        return true;
    }

    private static bool IsInBounds(StageSO stage, Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < stage.Width && pos.y >= 0 && pos.y < stage.Height;
    }

    /// <summary>
    /// ステージ境界を越えた移動が有効か判定する
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

        // 現在ステージ側の境界壁が壊れていない限り、外壁方向へは進めない
        // （外壁方向に行動することになってもその場で停止する）
        if (!gridManager.IsWalkableAt(currentStage, targetPos))
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
        bool isTerrainWalkable = gridManager.IsWalkableAt(neighbor, mappedPos);

        // ユニットの存在チェック
        Unit occupant = gridManager.StageEnemySystem.GetEnemyAt(neighbor, mappedPos);
        if (occupant == null)
        {
            if (gridManager.PlayerScript != null && gridManager.CurrentStageSO == neighbor)
            {
                if (gridManager.PlayerScript.CurrentPos == mappedPos) occupant = gridManager.PlayerScript;
            }
        }

        // 通行可能条件: 地形が通行可能で、ユニットに塞がれていないこと
        return isTerrainWalkable && occupant == null;
    }
}
