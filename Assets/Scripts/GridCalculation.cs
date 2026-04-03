using UnityEngine;

public static class GridCalculation
{
    public enum WarpDirection
    {
        Right,
        Down,
        Left,
        Up,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Same
    }

    public static WarpDirection DetermineWarpDirection(Vector2Int from, Vector2Int to)
    {
        // Decompose position into Layer and Column
        int fromLayer = from.x / 3;
        int toLayer = to.x / 3;
        int fromCol = from.x % 3;
        int toCol = to.x % 3;

        // Visual X Movement: Change in column within the view
        int dx = toCol - fromCol;

        // Visual Y Movement: Combine Floor Change (y) AND Layer Depth Change
        // Layer change (Depth) is perceived as vertical movement (Diving/Surfacing)
        // Sign: Layer Increase (Deeper) -> Down (+), Layer Decrease -> Up (-)
        // User reports "Back->Front Animation is Reversed" with previous (from-to) logic.
        // So we revert to (to-from).
        // 0->2 (Back->Front) => Positive => Down => Moves UP (Incoming from Bottom).
        int layerChange = toLayer - fromLayer;
        int floorChange = to.y - from.y;

        int dy = floorChange + (layerChange * 10);

        // 斜め方向
        if (dy > 0 && dx < 0) return WarpDirection.DownLeft;   // 下の階・左
        if (dy > 0 && dx > 0) return WarpDirection.DownRight;  // 下の階・右
        if (dy < 0 && dx < 0) return WarpDirection.UpLeft;     // 上の階・左
        if (dy < 0 && dx > 0) return WarpDirection.UpRight;    // 上の階・右

        // 垂直方向のみ
        if (dy > 0) return WarpDirection.Down;
        if (dy < 0) return WarpDirection.Up;

        // 水平方向のみ
        if (dx > 0) return WarpDirection.Right;
        if (dx < 0) return WarpDirection.Left;

        return WarpDirection.Same;
    }

    /// <summary>
    /// 階段移動用のワープ方向を決定
    /// </summary>
    public static WarpDirection DetermineStairWarpDirection(StageSO fromStage, StageSO toStage, bool goingDown)
    {
        if (fromStage == null || toStage == null)
        {
            return goingDown ? WarpDirection.Down : WarpDirection.Up;
        }

        // 階段の列（StairColumn）を使用して水平方向を決定
        int fromColumn = fromStage.StairColumn;
        int toColumn = toStage.StairColumn;

        // ハブ（中央列=1）からの移動の場合
        if (fromStage.MapPosition.y == 0)
        {
            // ハブ→ダンジョン
            if (toColumn == 0) return WarpDirection.DownLeft;   // 左の階段
            if (toColumn == 2) return WarpDirection.DownRight;  // 右の階段
            return WarpDirection.Down;  // 中央の階段
        }

        // ダンジョン→ハブの場合
        if (toStage.MapPosition.y == 0)
        {
            if (fromColumn == 0) return WarpDirection.UpRight;  // 左列からハブへ
            if (fromColumn == 2) return WarpDirection.UpLeft;   // 右列からハブへ
            return WarpDirection.Up;  // 中央列からハブへ
        }

        // 同じ列内での移動（通常の階段移動）
        if (fromColumn == toColumn)
        {
            return goingDown ? WarpDirection.Down : WarpDirection.Up;
        }

        // 異なる列への移動（通常はないが念のため）
        int dx = toColumn - fromColumn;
        if (goingDown)
        {
            if (dx < 0) return WarpDirection.DownLeft;
            if (dx > 0) return WarpDirection.DownRight;
            return WarpDirection.Down;
        }
        else
        {
            if (dx < 0) return WarpDirection.UpLeft;
            if (dx > 0) return WarpDirection.UpRight;
            return WarpDirection.Up;
        }
    }

    public static Vector2Int FindStairUpPosition(StageSO stage)
    {
        if (stage == null) return Vector2Int.zero;

        // ステージ内でStairUpセルを探す
        for (int y = 0; y < stage.Height; y++)
        {
            for (int x = 0; x < stage.Width; x++)
            {
                // Note: This relies on StageSO's static data directly.
                // It does NOT consider dynamically changes (destroyed walls) unless GetWallAt logic is replicated.
                // Assuming StairUp are static map features.
                if (stage.GetWallAt(x, y) == CellType.StairUp || stage.GetWallAt(x, y) == CellType.BrokenStairUp)
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        // 見つからなければ初期位置
        return stage.PlayerStartPosition;
    }

    public static Vector2Int CalculateEntryPosition(StageSO nextStage, Direction fromDirection, Vector2Int currentPos)
    {
        int targetX = 0;
        int targetY = 0;

        switch (fromDirection)
        {
            case Direction.Right: // 左端に出る
                targetX = 0;
                targetY = Mathf.Clamp(currentPos.y, 0, nextStage.Height - 1);
                break;
            case Direction.Left: // 右端に出る
                targetX = nextStage.Width - 1;
                targetY = Mathf.Clamp(currentPos.y, 0, nextStage.Height - 1);
                break;
            case Direction.Up: // 下端に出る
                targetX = Mathf.Clamp(currentPos.x, 0, nextStage.Width - 1);
                targetY = 0;
                break;
            case Direction.Down: // 上端に出る
                targetX = Mathf.Clamp(currentPos.x, 0, nextStage.Width - 1);
                targetY = nextStage.Height - 1;
                break;
        }
        return new Vector2Int(targetX, targetY);
    }

    /// <summary>
    /// 隣接ステージへの遷移オフセットを計算（GridParentの移動量）
    /// 次のステージを現在ステージの隣に配置し、そこから中心（0,0）へ移動させるためのベクトル
    /// </summary>
    public static Vector3 CalculateTransitionOffset(StageSO currentStage, StageSO nextStage, Direction moveDirection, float cellSize)
    {
        float xDist = 0f;
        float yDist = 0f;

        switch (moveDirection)
        {
            case Direction.Right:
                // 現在ステージの右端(Width)と次ステージの左端(-1)を合わせる
                // 仮にcellSize=1 で current.Width=5 (+1 for wall = 6)
                // GridParentを -6 移動させると、+6 の位置にあったものが 0 に来る
                xDist = -(currentStage.Width + 2) * cellSize;
                break;

            case Direction.Left:
                // 現在ステージの左端(-1)と次ステージの右端(Width)を合わせる
                // 移動距離 = (Next.Width + 1) * CellSize (右へ移動するのでプラス)
                xDist = (nextStage.Width + 2) * cellSize;
                break;

            case Direction.Up:
                // 現在ステージの上端(Height)と次ステージの下端(-1)を合わせる
                // 移動距離 = (Current.Height + 1) * CellSize (下へ移動するのでマイナス)
                // Up means current moves Down, so Next comes from Top.
                yDist = -(currentStage.Height + 2) * cellSize;
                break;

            case Direction.Down:
                // 現在ステージの下端(-1)と次ステージの上端(Height)を合わせる
                // 移動距離 = (Next.Height + 1) * CellSize (上へ移動するのでプラス)
                // Down means current moves Up, so Next comes from Bottom.
                yDist = (nextStage.Height + 2) * cellSize;
                break;
        }

        return new Vector3(xDist, yDist, 0f);
    }

    /// <summary>
    /// ワープ遷移用のオフセット計算
    /// </summary>
    /// <summary>
    /// ワープ遷移用のオフセット計算
    /// </summary>
    public static Vector3 CalculateWarpTransitionOffset(GridCalculation.WarpDirection warpDir, Vector2 tilemapOffset, StageSO currentStage = null, StageSO nextStage = null, float cellSize = 16f)
    {
        // 垂直・水平方向の場合は隣接計算を行う（ステージサイズ考慮）
        if (currentStage != null && nextStage != null)
        {
            switch (warpDir)
            {
                case WarpDirection.Right:
                    return CalculateTransitionOffset(currentStage, nextStage, Direction.Right, cellSize);
                case WarpDirection.Left:
                    return CalculateTransitionOffset(currentStage, nextStage, Direction.Left, cellSize);
                case WarpDirection.Up:
                    return CalculateTransitionOffset(currentStage, nextStage, Direction.Up, cellSize);
                case WarpDirection.Down:
                    return CalculateTransitionOffset(currentStage, nextStage, Direction.Down, cellSize);
            }
        }

        // 斜め方向またはステージ情報がない場合は従来通り固定オフセット（画面サイズ基準）
        float xOffset = tilemapOffset.x * 2;
        float yOffset = tilemapOffset.y * 2;
        float xStage = 0f;
        float yStage = 0f;

        // 斜め移動の場合もステージサイズを考慮しないと位置がずれる可能性があるが、
        // 階段移動（斜め）は物理的な隣接ではないため、画面外へ飛ばすだけで良い場合が多い。
        // ここでは従来の挙動を維持しつつ、必要なら拡張できるようにする。

        return warpDir switch
        {
            // Cardinal directions (Generic) - Matched to CalculateTransitionOffset logic
            // Down (Deeper) -> Parent moves Up (+Y) so Content from Bottom comes in
            GridCalculation.WarpDirection.Down => new Vector3(0, yOffset + yStage, 0),

            // Up (Shallower) -> Parent moves Down (-Y) so Content from Top comes in
            GridCalculation.WarpDirection.Up => new Vector3(0, -yOffset - yStage, 0),

            // Right -> Parent moves Left (-X) so Content from Right comes in
            GridCalculation.WarpDirection.Right => new Vector3(-xOffset - xStage, 0, 0),

            // Left -> Parent moves Right (+X) so Content from Left comes in
            GridCalculation.WarpDirection.Left => new Vector3(xOffset + xStage, 0, 0),

            // Diagonals
            // DownRight -> Down (+Y) + Right (-X)
            // DownRight
            GridCalculation.WarpDirection.DownRight => new Vector3(xOffset, -yOffset, 0),

            // DownLeft
            GridCalculation.WarpDirection.DownLeft => new Vector3(-xOffset, -yOffset, 0),

            // UpRight
            GridCalculation.WarpDirection.UpRight => new Vector3(xOffset, yOffset, 0),

            // UpLeft
            GridCalculation.WarpDirection.UpLeft => new Vector3(-xOffset, yOffset, 0),

            _ => new Vector3(0, yOffset, 0)
        };
    }

    public static Vector3 CalculateWarpInitialOffset(GridCalculation.WarpDirection warpDir, Vector2 tilemapOffset, StageSO currentStage = null, StageSO nextStage = null, float cellSize = 16f)
    {
        // 移動前（TransitionMapの初期位置）のオフセット
        // CalculateWarpTransitionOffset の逆方向
        return -CalculateWarpTransitionOffset(warpDir, tilemapOffset, currentStage, nextStage, cellSize);
    }

}
