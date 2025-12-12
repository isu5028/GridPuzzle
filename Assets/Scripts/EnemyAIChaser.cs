using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIChaser : EnemyAI
{
    // A* Node
    private class Node
    {
        public Vector2Int Position;
        public Node Parent;
        public int G; // Cost from start
        public int H; // Heuristic to target
        public int F => G + H;

        public Node(Vector2Int pos, Node parent, int g, int h)
        {
            Position = pos;
            Parent = parent;
            G = g;
            H = h;
        }
    }

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
        List<Vector2Int> path = FindPath(currentPos, playerPos);

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

    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int target)
    {
        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();

        openList.Add(new Node(start, null, 0, GetManhattanDistance(start, target)));

        // 安全策としてのループ回数制限
        int loopCount = 0;
        int maxLoops = 500;

        while (openList.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;

            // F値が最小のノードを取得
            Node currentNode = openList.OrderBy(n => n.F).First();

            if (currentNode.Position == target)
            {
                // ゴール到達（基本的には手前で止まるはずだが、パスとしてはここまで計算）
                return ReconstructPath(currentNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.Position);

            // 上下左右の隣接マスをチェック
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                Vector2Int neighborPos = currentNode.Position + Utility.GetVectorFromDirection(dir);

                // 既に確定済みならスキップ
                if (closedList.Contains(neighborPos)) continue;

                // マップ外や壁ならスキップ
                if (!gridManager.IsCellWalkable(neighborPos)) continue;

                // 他の敵がいる場合は障害物とみなす
                // ただし、ターゲットそのもの（プレイヤー）の位置は「移動先」ではないが、
                // ターゲットへのパス探索上は「到達点」として扱うため、Enemy判定を除外する必要がある
                // IsEnemyAtPositionはPlayerに対してfalseを返すので、そのままでOK
                if (IsEnemyAtPosition(neighborPos))
                {
                    // もしターゲット自身が敵だった場合（Enemy vs Enemyのゲームなど）を考慮するなら条件が必要だが
                    // ここではPlayer追跡なのでOK
                    continue;
                }

                // 新しいスコア計算
                int newG = currentNode.G + 1;
                Node existingNode = openList.FirstOrDefault(n => n.Position == neighborPos);

                if (existingNode == null)
                {
                    // Openリストになければ追加
                    openList.Add(new Node(neighborPos, currentNode, newG, GetManhattanDistance(neighborPos, target)));
                }
                else if (newG < existingNode.G)
                {
                    // より良いパスが見つかったなら更新
                    existingNode.G = newG;
                    existingNode.Parent = currentNode;
                }
            }
        }

        // 経路が見つからなかった場合（到達不能）
        return null;
    }

    private List<Vector2Int> ReconstructPath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        while (node.Parent != null) // Startノード自体はリストに含めない（移動先が知りたいから）
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse(); // Startに近い順(次の移動先)先頭へ
        return path;
    }

    private int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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