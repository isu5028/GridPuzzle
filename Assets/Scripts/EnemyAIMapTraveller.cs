using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIMapTraveller : EnemyAI
{
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

    // Special target for testing map traversal
    // Ideally this would be determined by a higher level brain (e.g. "Escape" or "Patrol Global")
    // For now, let's make it chance-based or simple logic:
    // If Player is close, chase. If not, maybe wander to a neighbor map?
    // Or simpler: Just Chase Player. If Player is on another map (impossible logic-wise locally).
    // Let's implement: "Chase Player". But if Player is unreachably far, try to move to a random neighbor map to "search".
    // Or: "Patrol". Pick a random neighbor stage and go there.

    // Let's implement "Chase Player". AND if the best path to player involves going through a wall/obstacle, maybe flank via another map? (Too complex).

    // Simplest: 50% chance to chase player, 10% chance to wander to a neighbor map.

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction currentDirection = enemy.CurrentDirection;

        Unit player = gridManager.PlayerScript;
        if (player == null) return;
        Vector2Int playerPos = player.CurrentPos;

        // 1. Attack if in range
        if (IsPlayerInAttackRange(enemy, playerPos))
        {
            Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
            if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
            return;
        }

        // 2. Pathfinding
        // Extend bounds to include immediate neighbors (transition points)
        List<Vector2Int> path = FindPath(currentPos, playerPos);

        // If no path to player (e.g. different stage or blocked), check if we can warp from current position
        if (path == null)
        {
            var transition = gridManager.GetEnemyStairDestination(enemy, currentPos, currentDirection);
            if (transition != null)
            {
                if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
                await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, currentDirection);
            }
            return;
        }

        // 3. Move Execution
        if (path != null && path.Count > 0)
        {
            Vector2Int nextPos = path[0];
            Direction moveDirection = GetDirectionToTarget(currentPos, nextPos);

            // Check if nextPos is out of bounds
            if (!gridManager.IsPositionInBounds(nextPos))
            {
                return;
            }

            // Standard Move
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
            // Idle / Face Player
            Direction attackDirection = GetDirectionToTarget(currentPos, playerPos);
            if (attackDirection != currentDirection) enemy.SetDirection(attackDirection);
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);
            enemy.TryAttack();
            await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
    }

    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int target)
    {
        return AStar(start, target, false);
    }

    private List<Vector2Int> AStar(Vector2Int start, Vector2Int target, bool allowOutOfBoundsTarget)
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

            if (currentNode.Position == target)
                return ReconstructPath(currentNode);

            openList.Remove(currentNode);
            closedList.Add(currentNode.Position);

            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                Vector2Int neighborPos = currentNode.Position + Utility.GetVectorFromDirection(dir);
                if (closedList.Contains(neighborPos)) continue;

                // Check walkability
                bool isWalkable = gridManager.IsCellWalkable(neighborPos);

                // Special case: If we allow out of bounds target, and this IS the target (or close to it?)
                // IsCellWalkable returns false for out of bounds.
                // But if neighborPos IS the target and allowOutOfBoundsTarget is true, we consider it valid IF there is an adjacent stage
                if (!isWalkable && allowOutOfBoundsTarget && neighborPos == target)
                {
                     // This is our exit!
                     // But we must assume we can step into it.
                     isWalkable = true;
                }

                // Also check enemies
                if (isWalkable && IsEnemyAtPosition(neighborPos)) isWalkable = false;

                if (!isWalkable) continue;

                int newG = currentNode.G + 1;
                Node existingNode = openList.FirstOrDefault(n => n.Position == neighborPos);

                if (existingNode == null)
                    openList.Add(new Node(neighborPos, currentNode, newG, GetManhattanDistance(neighborPos, target)));
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

    private int GetManhattanDistance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private bool IsPlayerInAttackRange(Enemy enemy, Vector2Int playerPos)
    {
        Vector2Int currentPos = enemy.CurrentPos;
        Direction[] directions = new Direction[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        foreach (var dir in directions)
        {
            if (currentPos + Utility.GetVectorFromDirection(dir) == playerPos) return true;
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
