using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIChaserSlow : EnemyAI
{
    private int turnCount = 0;

    private class EnemyTurnStateCommand : ICommand
    {
        private EnemyAIChaserSlow ai;
        private Enemy enemy;
        private int prevTurnCount;
        private int nextTurnCount;
        private string prevText;
        private string nextText;

        public EnemyTurnStateCommand(EnemyAIChaserSlow ai, Enemy enemy, int currentTurnCount)
        {
            this.ai = ai;
            this.enemy = enemy;
            this.prevTurnCount = currentTurnCount;
            this.nextTurnCount = currentTurnCount + 1;

            // Next Text Logic:
            // nextTurnCount is the state AFTER this turn processes.
            // If nextTurnCount is Odd (e.g. 1), we just finished a Move Turn. Next turn (2) will be Rest. So display "✕".
            // If nextTurnCount is Even (e.g. 2), we just finished a Rest Turn. Next turn (3) will be Move. So display Number.
            this.nextText = (nextTurnCount % 2 != 0) ? "×" : (enemy.Order + 1).ToString();

            // Prev Text Logic:
            // Restore to what it was at start of turn.
            // If prevTurnCount was 0 (Even), it meant we were about to Move. So text was Number.
            // If prevTurnCount was 1 (Odd), it meant we were about to Rest. So text was "✕".
            this.prevText = (prevTurnCount % 2 != 0) ? "×" : (enemy.Order + 1).ToString();
        }

        public void Execute()
        {
            ai.turnCount = nextTurnCount;
            enemy.SetOrderText(nextText);
        }

        public void Undo()
        {
            ai.turnCount = prevTurnCount;
            enemy.SetOrderText(prevText);
        }
    }



    public override async UniTask ExecuteAI(Enemy enemy)
    {
        int nextTurn = turnCount + 1;
        bool isMoveTurn = (nextTurn % 2 != 0);

        if (isMoveTurn)
        {
            // Move Turn
            await ExecuteChaserAI(enemy);
        }
        else
        {
            // Rest Turn
        }

        // Update State (TurnCount and Text) via Command for Undo support
        var stateCmd = new EnemyTurnStateCommand(this, enemy, turnCount);
        commandManager.ExecuteCommand(stateCmd);
    }

    private async UniTask ExecuteChaserAI(Enemy enemy)
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
        }
        else
        {
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

    public override int GetState()
    {
        return turnCount;
    }

    public override void SetState(int state)
    {
        turnCount = state;

        Enemy enemy = GetComponent<Enemy>();
        if (enemy != null)
        {
            // Update visual based on loaded state
            // If state is even -> Next turn is Move (state+1 is Odd) -> Show Number
            // If state is odd -> Next turn is Rest (state+1 is Even) -> Show X
            // Wait, previous logic:
            // Turn 0 (Move) -> End -> State 1. Text X.
            // Turn 1 (Rest) -> End -> State 2. Text Number.
            // So if loaded state is 1 (Rest pending), Text should be X.
            // If loaded state is 0/2 (Move pending), Text should be Number.

            // State: Odd -> Rest -> X
            // State: Even -> Move -> Number

            string text = (state % 2 != 0) ? "×" : (enemy.Order + 1).ToString();
            enemy.SetOrderText(text);
        }
    }

    private bool IsEnemyAtPosition(Vector2Int pos)
    {
        Unit unit = gridManager.GetUnitAtPosition(pos);
        return unit != null && unit is Enemy;
    }
}
