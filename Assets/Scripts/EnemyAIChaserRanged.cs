using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIChaserRanged : EnemyAI
{
    [SerializeField] private Transform arrow;

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;

        Unit player = gridManager.PlayerScript;
        if (player == null) return;

        Vector2Int playerPos = player.CurrentPos;

        // 1. 軸が合っているか確認 (X or Y)
        bool alignedX = currentPos.x == playerPos.x;
        bool alignedY = currentPos.y == playerPos.y;

        if (alignedX || alignedY)
        {
            await PerformAlignedAttack(enemy, currentPos, playerPos, alignedX);
            return;
        }

        // 2. 軸が合っていない場合、軸を合わせようと移動する
        Direction moveDir = GetBestMoveDirection(currentPos, playerPos);
        Vector2Int nextPos = currentPos + Utility.GetVectorFromDirection(moveDir);

        if (gridManager.IsCellWalkable(nextPos) && !IsEnemyAtPosition(nextPos))
        {
            // 移動可能なら移動
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, nextPos, currentDirection, moveDir);
            commandManager.ExecuteCommand(moveCommand);
            if(GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
        else
        {
            // 移動できない（壁や敵がいる）なら、その方向へ攻撃
            enemy.SetDirection(moveDir); // 向き直り
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);

            // 目の前のマスの座標をターゲットにする
            List<Vector2Int> singleTarget = new List<Vector2Int> { nextPos };

            // 攻撃実行 (RangedAttackCommandではなく、通常のAttackで壁などを壊す挙動に近いイメージだが、
            // リクエストは「その方向に対し攻撃」なので、RangedAttackで目の前を撃つのか、通常攻撃か？
            // 「途中障害物があろうと攻撃」「進めない場合、その方向に対し攻撃」
            // RangedEnemyなのでRangedAttackを行うのが自然。

            await PerformRangedAttackLogic(enemy, currentPos, moveDir);
        }
    }

    private async UniTask PerformAlignedAttack(Enemy enemy, Vector2Int currentPos, Vector2Int playerPos, bool alignedX)
    {
        Direction attackDir = Direction.Up;
        if (alignedX)
        {
             if (playerPos.y > currentPos.y) attackDir = Direction.Up;
             else attackDir = Direction.Down;
        }
        else
        {
             if (playerPos.x > currentPos.x) attackDir = Direction.Right;
             else attackDir = Direction.Left;
        }

        if (enemy.CurrentDirection != attackDir)
        {
            enemy.SetDirection(attackDir);
            await UniTask.Delay(100);
        }

        await PerformRangedAttackLogic(enemy, currentPos, attackDir);
    }

    private async UniTask PerformRangedAttackLogic(Enemy enemy, Vector2Int startPos, Direction dir)
    {
        Vector2Int directionVec = Utility.GetVectorFromDirection(dir);

        // 射線チェック
        Vector2Int checkPos = startPos;
        Vector2Int hitPos = startPos + directionVec; // 最低でも目の前

        int maxDist = 30;
        for(int i=1; i<=maxDist; i++)
        {
            checkPos += directionVec;

            // 壁チェック
            if (!gridManager.IsCellWalkable(checkPos))
            {
                hitPos = checkPos;
                break;
            }

            // ユニットチェック
            Unit hitUnit = gridManager.GetUnitAtPosition(checkPos);
            if (hitUnit != null)
            {
                hitPos = checkPos;
                break;
            }
        }

        if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);

        RangedAttackCommand attackCmd = new RangedAttackCommand(enemy, hitPos, gridManager, arrow);
        commandManager.ExecuteCommand(attackCmd);

        await UniTask.WaitUntil(() => !enemy.IsMoving);
    }

    private Direction GetBestMoveDirection(Vector2Int current, Vector2Int target)
    {
        // 単純に差が大きい軸、あるいは近い軸？
        // 「軸を合わせようとする」 -> どちらかの軸に移動して、相手の座標と一致させたい
        // 例えば (0,0) -> (5,2) なら、X+5, Y+2 必要。
        // X移動して (1,0) になれば X軸合わせには近づくが、Y軸合わせには貢献しない
        // Y移動して (0,1) になれば Y軸合わせに近づく

        // どちらでも良いが、距離が近い方（早く軸が合う方）を優先するか、
        // 逆に遠い方を優先して近づくか。
        // 一般的には「軸合わせ」=「その軸の座標を同じにする」
        // X軸を合わせる = Y方向に移動して Y座標を同じにする
        // Y軸を合わせる = X方向に移動して X座標を同じにする

        int xDiff = Mathf.Abs(current.x - target.x);
        int yDiff = Mathf.Abs(current.y - target.y);

        // 差が小さい方（早く合う方）を優先して移動してみる
        // 例: (0,0) -> (10, 2)
        // Y差が2なので、Y方向に動けば2ターンでX軸が合う（一直線になる）
        // X方向に動くと10ターン必要

        if (xDiff < yDiff)
        {
            // X差の方が小さい -> X方向へ移動してY軸を合わせに行く？ 逆だ
            // 「X軸が合っている」状態とは x座標が同じこと。
            // つまり x方向に移動する必要がある。

            // 例: (0,0) enemy, (0,10) player -> x=0で同じ。AlignedX = true.

            // もう一度整理
            // AlignedX (縦に並ぶ) になりたい -> x座標を合わせたい -> x方向に動く
            // AlignedY (横に並ぶ) になりたい -> y座標を合わせたい -> y方向に動く

            // 早く軸を合わせたいなら、差が小さい(距離が近い)軸座標を合わせに行くべき

            if (xDiff > 0)
            {
                // X座標を合わせに行く
                return (target.x > current.x) ? Direction.Right : Direction.Left;
            }
            else
            {
                // X差が0なら既に合ってるはずだが、ここは念のため
                return (target.y > current.y) ? Direction.Up : Direction.Down;
            }
        }
        else
        {
            // Y差の方が小さい、または同じ -> Y座標を合わせに行く
             if (yDiff > 0)
            {
                return (target.y > current.y) ? Direction.Up : Direction.Down;
            }
            else
            {
                 return (target.x > current.x) ? Direction.Right : Direction.Left;
            }
        }
    }

    private bool IsEnemyAtPosition(Vector2Int pos)
    {
        Unit unit = gridManager.GetUnitAtPosition(pos);
        return unit != null && unit is Enemy;
    }
}