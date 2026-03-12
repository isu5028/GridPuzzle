using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AStarPathfinder
{
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

    /// <summary>
    /// A*アルゴリズムを用いて経路を探索します。
    /// </summary>
    /// <param name="start">開始座標</param>
    /// <param name="target">標座標</param>
    /// <param name="isTraversable">指定した座標が通行可能かどうかを判定する関数</param>
    /// <param name="maxLoops">検索の最大ループ回数（無限ループ防止用）</param>
    /// <returns>経路の座標リスト（開始地点を含まず、ゴール地点を含む）。経路が見つからない場合はnull。</returns>
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int target, System.Func<Vector2Int, bool> isTraversable, int maxLoops = 500)
    {
        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();

        openList.Add(new Node(start, null, 0, GetManhattanDistance(start, target)));

        int loopCount = 0;

        while (openList.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;

            // F値が最小のノードを取得
            Node currentNode = openList.OrderBy(n => n.F).First();

            if (currentNode.Position == target)
            {
                // ゴール到達
                return ReconstructPath(currentNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.Position);

            // 上下左右の隣接マスをチェック
            // 優先順位: Right -> Left -> Up -> Down (左右移動を優先、詳細はUtilityで定義)

            foreach (Direction dir in Utility.SearchOrder)
            {
                Vector2Int neighborPos = currentNode.Position + Utility.GetVectorFromDirection(dir);

                // 既に確定済みならスキップ
                if (closedList.Contains(neighborPos)) continue;

                // 通行可能かチェック
                if (!isTraversable(neighborPos)) continue;

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

        // 経路が見つからなかった場合
        return null;
    }

    private static List<Vector2Int> ReconstructPath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        while (node.Parent != null) // Startノード自体はリストに含めない
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse(); // Startに近い順先頭へ
        return path;
    }

    private static int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
