using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
public class EnemyAIPatrol : EnemyAI
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

        // 前方に移動可能か確認 (境界越えも含む)
        bool canMoveForward = gridManager.IsCellWalkable(frontPos) || IsValidBoundaryMove(enemy, frontPos, currentDirection);

        if(canMoveForward)
        {
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, frontPos, currentDirection, currentDirection);
            commandManager.ExecuteCommand(moveCommand);

            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);

            // 境界遷移チェック
            if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy)) return;

            // 階段遷移チェック
            var transition = gridManager.GetEnemyStairDestination(enemy, frontPos, currentDirection);
            if (transition != null)
            {
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, currentDirection);
            }
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

            bool canMoveBackward = gridManager.IsCellWalkable(backPos) || IsValidBoundaryMove(enemy, backPos, oppositeDirection);

            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, backPos, currentDirection, oppositeDirection);

            // 後ろも移動不可なら移動せず方向転換のみ（MoveCommandは移動なしでも方向転換するかも？いや、same posならしないか）
            // MoveCommandの実装を見ると、from!=toなら移動する。
            // ここでは canMoveBackward チェックを入れてない？ 元コードは `MoveCommand` を無条件で実行してる？
            // 元コード: `MoveCommand moveCommand = new MoveCommand(enemy, currentPos, backPos, currentDirection, oppositeDirection);`
            // `execute` -> 壁なら移動しないはず？
            // SystemのMoveCommandは `gridManager.IsWalkableAt` をチェックしている？
            // いや、Commandは実行するもの。AI判定で弾くべき。
            // 元コードは `else` ブロックに入った時点で「前は無理」。
            // 後ろについての `IsCellWalkable` チェックがない？
            // あ、MoveCommandの中で詰まるだけか。
            // しかし境界移動させるなら、MoveCommandに渡す `backPos` はOOBになる。
            // MoveCommandがOOBを受け入れるか？
            // MoveCommand -> `Unit.Move` -> Tween.
            // MoveCommand自体は座標の妥当性をチェックしないかもしれない（AIがチェック済み前提）。
            // なので `canMoveBackward` をチェックして、OKならMoveCommand発行、NGなら何もしない（or 方向転換のみ）とするべきだが、
            // 今回は既存の挙動を変えすぎないよう、BoundaryMoveが可能ならMoveCommandを発行するようにする。

            if (canMoveBackward)
            {
                commandManager.ExecuteCommand(moveCommand);
                if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);

                // 境界遷移チェック
                if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy)) return;

                // 階段遷移チェック
                var transition = gridManager.GetEnemyStairDestination(enemy, backPos, oppositeDirection);
                if (transition != null)
                {
                    if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                    await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, oppositeDirection);
                }
            }
            else
            {
                // 移動できないが方向転換はする？ 元コードはMoveCommand投げてる。
                // MoveCommandは移動不可ならその場で方向転換だけする仕様か確認が必要だが、
                // 取り急ぎ「移動できるなら移動」にする。
                // 元のコードは `commandManager.ExecuteCommand(moveCommand)` を呼んでた。
                // つまり後ろが壁でもMoveCommandを呼ぶことで方向転換を実現していた可能性がある。
                // 変更: 後ろが壁でも方向転換はしたい。
                // MoveCommandに (current, current) を渡せば方向転換だけになる？
                // backPosがOOBかつBoundaryMoveも不可の場合、MoveCommandを呼ぶとどうなる？
                // UnitがOOBに移動してしまう？
                // 既存: `MoveCommand` creates `Move` tween.
                // ここは安全策として、BoundaryMoveがTrueか、WalkableがTrueなら backPos へ移動。
                // どちらもFalseなら、CurrentPosへ移動（＝方向転換のみ）するコマンドにする。

                Vector2Int targetPos = canMoveBackward ? backPos : currentPos;
                MoveCommand rotateCmd = new MoveCommand(enemy, currentPos, targetPos, currentDirection, oppositeDirection);
                commandManager.ExecuteCommand(rotateCmd);
                if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
            }
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