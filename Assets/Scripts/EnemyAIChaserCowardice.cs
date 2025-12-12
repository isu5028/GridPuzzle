using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIChaserCowardice : EnemyAI
{
    // A* Node
    private class Node
    {
        public Vector2Int Position;
        public Node Parent;
        public int G;
        public int H;
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
        if (IsVisibleToPlayer(enemy))
        {
            return;
        }

        // 見られていないなら追跡ロジックを実行
        await ExecuteChaserAI(enemy);
    }

    private bool IsVisibleToPlayer(Enemy enemy)
    {
        Unit player = gridManager.PlayerScript;
        if (player == null) return false;

        Vector2Int playerPos = player.CurrentPos;
        Vector2Int enemyPos = enemy.CurrentPos;
        Direction playerDir = player.CurrentDirection;

        // 1. 前方(Half-Plane)判定
        Vector2Int dirVec = Utility.GetVectorFromDirection(playerDir);

        Vector2Int toEnemyVec = enemyPos - playerPos;

        if (toEnemyVec == Vector2Int.zero) return true;

        // 内積を使って判定 (Dot Product)
        float dot = dirVec.x * toEnemyVec.x + dirVec.y * toEnemyVec.y;

        if (dot <= 0) return false;

        // 2. 遮蔽物判定 (Raycast Check)
        if (!HasLineOfSight(playerPos, enemyPos))
        {
            return false; // 壁に隠れている
        }

        return true;
    }

    private bool HasLineOfSight(Vector2Int start, Vector2Int end)
    {
        float distance = Vector2Int.Distance(start, end);
        if (distance < 1.0f) return true;

        int steps = Mathf.CeilToInt(distance * 2.0f);

        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            Vector2 samplePoint = Vector2.Lerp(start, end, t);

            Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(samplePoint.x), Mathf.RoundToInt(samplePoint.y));

            if (gridPos == start || gridPos == end) continue;

            if (!gridManager.IsCellWalkable(gridPos))
            {
                return false;
            }

            // 他の敵が遮蔽となる
            if (IsEnemyAtPosition(gridPos)) return false;
        }

        return true;
    }

    private async UniTask ExecuteChaserAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;
        Unit player = gridManager.PlayerScript;
        if (player == null) return;
        Vector2Int playerPos = player.CurrentPos;

        if (IsPlayerInAttackRange(enemy, playerPos))
        {
            Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
            if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        List<Vector2Int> path = FindPath(currentPos, playerPos);

        if (path != null && path.Count > 0)
        {
            Vector2Int nextPos = path[0];
            Direction moveDirection = GetDirectionToTarget(currentPos, nextPos);
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, nextPos, currentDirection, moveDirection);
            commandManager.ExecuteCommand(moveCommand);
            if (GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
        else
        {
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

        int loopCount = 0;
        int maxLoops = 500;

        while (openList.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;
            Node currentNode = openList.OrderBy(n => n.F).First();

            if (currentNode.Position == target) return ReconstructPath(currentNode);

            openList.Remove(currentNode);
            closedList.Add(currentNode.Position);

            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                Vector2Int neighborPos = currentNode.Position + Utility.GetVectorFromDirection(dir);
                if (closedList.Contains(neighborPos)) continue;
                if (!gridManager.IsCellWalkable(neighborPos)) continue;
                if (IsEnemyAtPosition(neighborPos)) continue;

                int newG = currentNode.G + 1;
                Node existingNode = openList.FirstOrDefault(n => n.Position == neighborPos);

                if (existingNode == null)
                {
                    openList.Add(new Node(neighborPos, currentNode, newG, GetManhattanDistance(neighborPos, target)));
                }
                else if (newG < existingNode.G)
                {
                    existingNode.G = newG;
                    existingNode.Parent = currentNode;
                }
            }
        }
        return null;
    }

    private List<Vector2Int> ReconstructPath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        while (node.Parent != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
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
            if (checkPos == playerPos) return true;
        }
        return false;
    }

    private Direction GetDirectionToTarget(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)) return diff.x > 0 ? Direction.Right : Direction.Left;
        else return diff.y > 0 ? Direction.Up : Direction.Down;
    }

    private bool IsEnemyAtPosition(Vector2Int pos)
    {
        Unit unit = gridManager.GetUnitAtPosition(pos);
        return unit != null && unit is Enemy;
    }
}
