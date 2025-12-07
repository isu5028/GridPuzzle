using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class EnemyAIChaserRanged : EnemyAI
{
    // A* Node logic
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
            // 軸が合っている場合、その方向にレイキャスト的にチェック
            Vector2Int directionVec = Vector2Int.zero;
            Direction attackDir = Direction.Up;

            if (alignedX)
            {
                // 上か下か
                if (playerPos.y > currentPos.y) { directionVec = Vector2Int.up; attackDir = Direction.Up; }
                else { directionVec = Vector2Int.down; attackDir = Direction.Down; }
            }
            else // alignedY
            {
                // 右か左か
                if (playerPos.x > currentPos.x) { directionVec = Vector2Int.right; attackDir = Direction.Right; }
                else { directionVec = Vector2Int.left; attackDir = Direction.Left; }
            }

            // 向きを変更
            if (currentDirection != attackDir)
            {
                enemy.SetDirection(attackDir);
                await UniTask.Delay(100);
            }

            // 射線チェック
            Vector2Int checkPos = currentPos;
            bool hitWall = false;
            Vector2Int hitPos = currentPos; // 何もなければ無限だが、ここでは壁まで

            // 無限ループ防止のためマップサイズ程度で制限
            int maxDist = 30;
            for(int i=1; i<=maxDist; i++)
            {
                checkPos += directionVec;

                // 壁チェック
                if (!gridManager.IsCellWalkable(checkPos))
                {
                    hitWall = true;
                    hitPos = checkPos;
                    break;
                }

                // ユニットチェック
                Unit hitUnit = gridManager.GetUnitAtPosition(checkPos);
                if (hitUnit != null)
                {
                    // 誰かに当たった（プレイヤーか、他の敵）
                    hitPos = checkPos;
                    break;
                }
            }

            // 攻撃実行（壁またはユニットに当たる位置へ）
            if(GameSettings.GameSpeed == 1) await UniTask.Delay((int)fastSpeedAttackDelayMs);

            RangedAttackCommand attackCmd = new RangedAttackCommand(enemy, hitPos, gridManager);
            commandManager.ExecuteCommand(attackCmd);

            await UniTask.WaitUntil(() => !enemy.IsMoving); // PerformRangedAttackのアニメーション待ち
            return;
        }

        // 2. 軸が合っていない場合、軸が合う位置を目指して移動
        // 目標：プレイヤーのX軸またはY軸上の、最も近い歩行可能なマス

        Vector2Int targetPos = FindNearestAlignedPosition(currentPos, playerPos);

        if (targetPos == currentPos)
        {
            // 既に軸があってるはずだが、ここにくるということは何かおかしい or 移動不可？
            // 待機
            return;
        }

        List<Vector2Int> path = FindPath(currentPos, targetPos);

        if (path != null && path.Count > 0)
        {
            Vector2Int nextPos = path[0];
            Direction moveDirection = GetDirectionToTarget(currentPos, nextPos);

            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, nextPos, currentDirection, moveDirection);
            commandManager.ExecuteCommand(moveCommand);
            if(GameSettings.GameSpeed == 0) await UniTask.WaitUntil(() => !enemy.IsMoving);
        }
        else
        {
            // 移動できない
        }
    }

    // プレイヤーの十字方向直線を探索し、自分に最も近い歩行可能マスを探す
    private Vector2Int FindNearestAlignedPosition(Vector2Int start, Vector2Int playerPos)
    {
        // 簡易的に、プレイヤーと同じXのライン、同じYのライン上の全マスを候補とする

        Vector2Int cand1 = new Vector2Int(playerPos.x, start.y);
        Vector2Int cand2 = new Vector2Int(start.x, playerPos.y);

        bool cand1Valid = gridManager.IsCellWalkable(cand1) && !IsEnemyAtPosition(cand1);
        bool cand2Valid = gridManager.IsCellWalkable(cand2) && !IsEnemyAtPosition(cand2);

        if (cand1Valid && !cand2Valid) return cand1;
        if (!cand1Valid && cand2Valid) return cand2;
        if (!cand1Valid && !cand2Valid) return start; // どっちもダメなら動かない（暫定）

        // どっちも行けるなら近い方
        int dist1 = GetManhattanDistance(start, cand1);
        int dist2 = GetManhattanDistance(start, cand2);

        return dist1 <= dist2 ? cand1 : cand2;
    }

    // A* (共通化すべきだが一旦コピー)
    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int target)
    {
        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();
        openList.Add(new Node(start, null, 0, GetManhattanDistance(start, target)));

        int loopCount = 0;
        while (openList.Count > 0 && loopCount < 500)
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